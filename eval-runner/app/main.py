"""Prompt Evaluator eval-runner.

A thin, stateless FastAPI service the .NET Infrastructure layer calls over HTTP. In the
walking skeleton it only echoes the prompt; later specs add LLM-judge scoring and synthetic
fixture generation. It holds no domain authority and no persistence.
"""

import os
from typing import Annotated

from anthropic import Anthropic
from fastapi import Depends, FastAPI
from pydantic import BaseModel

from app.execution import (
    ExecutePromptRequest,
    ExecutePromptResponse,
    execute_prompt,
    stub_execute,
)
from app.generation import (
    GenerateFixturesRequest,
    GenerateFixturesResponse,
    generate_fixtures,
    stub_fixtures,
)
from app.judging import (
    JudgeRequest,
    JudgeResponse,
    judge,
    stub_judge,
)

SERVICE_NAME = "eval-runner"
VERSION = "0.2.0"

app = FastAPI(title="Prompt Evaluator eval-runner", version=VERSION)


class EchoRequest(BaseModel):
    prompt: str


class EchoResponse(BaseModel):
    output: str


class VersionResponse(BaseModel):
    service: str
    version: str
    commit: str


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


@app.post("/echo", response_model=EchoResponse)
def echo(request: EchoRequest) -> EchoResponse:
    # Skeleton behaviour: prove the seam by echoing the prompt straight back.
    return EchoResponse(output=request.prompt)


def get_anthropic_client() -> Anthropic | None:
    # Resolves credentials from the environment (ANTHROPIC_API_KEY / profile). Injected as a
    # dependency so tests can override it with a mock — no live API calls in the suite. Returns
    # None in stub mode (EVAL_RUNNER_STUB) so the client is never constructed without a key.
    if os.environ.get("EVAL_RUNNER_STUB"):
        return None
    return Anthropic()


@app.post("/generate-fixtures", response_model=GenerateFixturesResponse)
def generate_fixtures_endpoint(
    request: GenerateFixturesRequest,
    client: Annotated[Anthropic | None, Depends(get_anthropic_client)],
) -> GenerateFixturesResponse:
    if client is None:
        return stub_fixtures(request)
    return generate_fixtures(client, request)


@app.post("/execute-prompt", response_model=ExecutePromptResponse)
def execute_prompt_endpoint(
    request: ExecutePromptRequest,
    client: Annotated[Anthropic | None, Depends(get_anthropic_client)],
) -> ExecutePromptResponse:
    if client is None:
        return stub_execute(request)
    return execute_prompt(client, request)


@app.post("/judge", response_model=JudgeResponse)
def judge_endpoint(
    request: JudgeRequest,
    client: Annotated[Anthropic | None, Depends(get_anthropic_client)],
) -> JudgeResponse:
    if client is None:
        return stub_judge(request)
    return judge(client, request)
