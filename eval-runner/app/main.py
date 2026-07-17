"""LitmusAI eval-runner.

A thin, stateless FastAPI service the .NET Infrastructure layer calls over HTTP. In the
walking skeleton it only echoes the prompt; later specs add LLM-judge scoring and synthetic
fixture generation. It holds no domain authority and no persistence.
"""

import os
from typing import Annotated

from anthropic import Anthropic
from fastapi import Depends, FastAPI, Header, HTTPException, Request, status
from fastapi.responses import JSONResponse
from openai import OpenAI
from pydantic import BaseModel

from app.execution import (
    ExecutePromptRequest,
    ExecutePromptResponse,
    captured_execution,
    execute_prompt,
    stub_execute,
)
from app.generation import (
    GenerateFixturesRequest,
    GenerateFixturesResponse,
    effective_model,
    generate_fixtures,
    stub_fixtures,
)
from app.judging import (
    JudgeRequest,
    JudgeResponse,
    judge,
    stub_judge,
)
from app.providers import (
    PROVIDER_ANTHROPIC,
    PROVIDER_OPENAI,
    AnthropicProvider,
    OpenAIProvider,
    Provider,
    ProviderRegistry,
    UnknownProviderError,
)

SERVICE_NAME = "eval-runner"
# Version is stamped at image build from the git tag (APP_VERSION build-arg → ENV, like GIT_COMMIT);
# "0.0.0-dev" for local/per-process runs. The git tag is the single source of the app version.
VERSION = os.environ.get("APP_VERSION", "0.0.0-dev")

app = FastAPI(title="LitmusAI eval-runner", version=VERSION)


class EchoRequest(BaseModel):
    prompt: str


class EchoResponse(BaseModel):
    output: str


class VersionResponse(BaseModel):
    service: str
    version: str
    commit: str


class ProvidersResponse(BaseModel):
    providers: list[str]


# The providers the eval-runner knows how to route to (spec 1.5). New providers (e.g. a Modal SLM,
# spec 1.12) are added alongside their adapter.
KNOWN_PROVIDERS = (PROVIDER_ANTHROPIC, PROVIDER_OPENAI)


def configured_providers() -> list[str]:
    """The providers with usable credentials — the authority .NET reflects in the catalog (1.13).

    In stub mode execution is model-free (no vendor client is built), so report every known
    provider; otherwise dev/e2e runs with no keys would mark every catalog model unavailable. When
    not stubbed, a provider is configured only when its API key is present (mirrors the registry).
    """
    if os.environ.get("EVAL_RUNNER_STUB"):
        return list(KNOWN_PROVIDERS)
    providers: list[str] = []
    if os.environ.get("ANTHROPIC_API_KEY"):
        providers.append(PROVIDER_ANTHROPIC)
    if os.environ.get("OPENAI_API_KEY"):
        providers.append(PROVIDER_OPENAI)
    return providers


def require_service_token(
    x_service_token: Annotated[str | None, Header()] = None,
) -> None:
    # eval-runner is an INTERNAL TRUSTED SERVICE (4.1), reached only by the .NET backend over a
    # shared service token — never by user credentials. When EVAL_RUNNER_SERVICE_TOKEN is set the
    # caller must present a matching X-Service-Token header (else 401). When it is UNSET
    # (dev/CI/tests) the endpoints stay open, preserving the walking-skeleton behaviour.
    # Read at request time (not import) so tests can set/clear the env with monkeypatch.
    expected = os.environ.get("EVAL_RUNNER_SERVICE_TOKEN")
    if not expected:
        return
    if x_service_token != expected:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or missing service token.",
        )


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/version", response_model=VersionResponse)
def version() -> VersionResponse:
    # GIT_COMMIT is baked in at image build time; "dev" for local/per-process runs.
    return VersionResponse(
        service=SERVICE_NAME,
        version=VERSION,
        commit=os.environ.get("GIT_COMMIT", "dev"),
    )


@app.get(
    "/providers",
    response_model=ProvidersResponse,
    dependencies=[Depends(require_service_token)],
)
def providers() -> ProvidersResponse:
    return ProvidersResponse(providers=configured_providers())


@app.post("/echo", response_model=EchoResponse, dependencies=[Depends(require_service_token)])
def echo(request: EchoRequest) -> EchoResponse:
    # Skeleton behaviour: prove the seam by echoing the prompt straight back.
    return EchoResponse(output=request.prompt)


def get_provider_registry() -> ProviderRegistry | None:
    # Builds the set of configured providers from the environment (composition root). Returns
    # None in stub mode (EVAL_RUNNER_STUB) so no vendor client is constructed. A provider is
    # registered only when its API key is present, so a request for a model whose provider has
    # no credentials fails clearly at routing time (UnknownProviderError -> 400) rather than
    # opaquely at call time. Injected as a dependency so tests mock at the boundary.
    if os.environ.get("EVAL_RUNNER_STUB"):
        return None
    providers: dict[str, Provider] = {}
    anthropic_key = os.environ.get("ANTHROPIC_API_KEY")
    if anthropic_key:
        providers[PROVIDER_ANTHROPIC] = AnthropicProvider(Anthropic(api_key=anthropic_key))
    openai_key = os.environ.get("OPENAI_API_KEY")
    if openai_key:
        providers[PROVIDER_OPENAI] = OpenAIProvider(OpenAI(api_key=openai_key))
    return ProviderRegistry(providers)


@app.exception_handler(UnknownProviderError)
def _unknown_provider_handler(request: Request, exc: UnknownProviderError) -> JSONResponse:
    # An unroutable model id or an unconfigured provider (missing credentials) is a clear 400,
    # not an opaque 500. The message names the model/provider and hints at missing credentials.
    return JSONResponse(status_code=status.HTTP_400_BAD_REQUEST, content={"detail": str(exc)})


@app.post(
    "/generate-fixtures",
    response_model=GenerateFixturesResponse,
    dependencies=[Depends(require_service_token)],
)
def generate_fixtures_endpoint(
    request: GenerateFixturesRequest,
    registry: Annotated[ProviderRegistry | None, Depends(get_provider_registry)],
) -> GenerateFixturesResponse:
    if registry is None:
        return stub_fixtures(request)
    provider = registry.for_model(effective_model(request))
    return generate_fixtures(provider, request)


@app.post(
    "/execute-prompt",
    response_model=ExecutePromptResponse,
    dependencies=[Depends(require_service_token)],
)
def execute_prompt_endpoint(
    request: ExecutePromptRequest,
    registry: Annotated[ProviderRegistry | None, Depends(get_provider_registry)],
) -> ExecutePromptResponse:
    # Capture-first (1.2): a captured output is scored as-is, no provider call, no creds needed.
    if request.captured_output is not None:
        return captured_execution(request)
    if registry is None:
        return stub_execute(request)
    provider = registry.for_model(request.model)
    return execute_prompt(provider, request)


@app.post("/judge", response_model=JudgeResponse, dependencies=[Depends(require_service_token)])
def judge_endpoint(
    request: JudgeRequest,
    registry: Annotated[ProviderRegistry | None, Depends(get_provider_registry)],
) -> JudgeResponse:
    if registry is None:
        return stub_judge(request)
    provider = registry.for_model(request.model)
    return judge(provider, request)
