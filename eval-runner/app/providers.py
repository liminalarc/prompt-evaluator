"""Provider abstraction: route a model id to the right LLM provider adapter.

Each app we evaluate may run its prompts on a different provider — Anthropic, OpenAI, and
later others (a Modal-hosted SLM; see spec 1.12). A `Provider` adapter wraps one vendor SDK
and exposes the two capabilities the eval-runner needs:

- ``complete`` — plain text completion (subject execution).
- ``structured`` — structured output (judge verdict / guided generation), using that
  provider's *native* mechanism (Anthropic ``output_config.format``, OpenAI
  ``response_format`` json_schema) — never free-text parsing.

Routing is by model id: ``resolve_provider(model)`` picks the provider *name*; a
``ProviderRegistry`` holds the (injected, mockable) adapter per provider. Claude stays the
default judge/generator (see the DEFAULT_* model ids in the judging/generation modules); this
layer only routes.

Adding a provider (e.g. Modal): add a prefix rule to ``resolve_provider`` and register an
adapter with the same two methods. The vendor client is injected into the adapter, so tests
mock at the boundary with no live calls.
"""

import json
from dataclasses import dataclass
from typing import Protocol, runtime_checkable

from pydantic import BaseModel

PROVIDER_ANTHROPIC = "anthropic"
PROVIDER_OPENAI = "openai"

# Model-id prefixes that identify each provider. Kept deliberately small and explicit — an
# unrecognized model id is an error (fail clearly), not a silent default.
_OPENAI_PREFIXES = ("gpt-", "o1", "o3", "o4", "chatgpt")


class UnknownProviderError(ValueError):
    """No provider is registered/configured for a given model id."""


def resolve_provider(model: str) -> str:
    """Return the provider *name* for a model id, or raise UnknownProviderError."""
    m = model.lower()
    if m.startswith("claude-"):
        return PROVIDER_ANTHROPIC
    if any(m.startswith(p) for p in _OPENAI_PREFIXES):
        return PROVIDER_OPENAI
    raise UnknownProviderError(f"No provider is registered for model id '{model}'.")


class UsageBlock(BaseModel):
    """The full usage block for one model call (6.1), echoed to .NET's AI-usage ledger.

    ``model`` is the model the provider reports (falls back to the requested id). Cache tokens are 0
    for providers/models without prompt caching. ``status`` is ``success`` | ``refusal`` | ``error``.
    """

    model: str
    input_tokens: int
    output_tokens: int
    cache_creation_input_tokens: int = 0
    cache_read_input_tokens: int = 0
    request_id: str | None = None
    status: str = "success"
    max_tokens: int = 0


@dataclass
class Completion:
    """A plain text completion plus normalized token usage (provider-agnostic)."""

    text: str
    input_tokens: int
    output_tokens: int
    # Full usage block for the ledger (6.1). Older callers still read input_tokens/output_tokens.
    usage: UsageBlock | None = None


@dataclass
class StructuredResult:
    """A parsed structured-output object plus the usage of the call that produced it (6.1)."""

    data: dict
    usage: UsageBlock


@runtime_checkable
class Provider(Protocol):
    name: str

    def complete(self, *, model: str, system: str, user: str, max_tokens: int) -> Completion:
        """Run a plain text completion: `system` prompt + a single `user` turn."""
        ...

    def structured(
        self, *, model: str, prompt: str, schema: dict, max_tokens: int
    ) -> StructuredResult:
        """Run a structured-output completion; return the parsed object + usage matching `schema`."""
        ...


class AnthropicProvider:
    """Adapter over the Anthropic SDK (``client.messages.create``)."""

    name = PROVIDER_ANTHROPIC

    def __init__(self, client):
        self._client = client

    @staticmethod
    def _usage(response, requested_model: str, max_tokens: int) -> UsageBlock:
        usage = getattr(response, "usage", None)
        status = "refusal" if getattr(response, "stop_reason", None) == "refusal" else "success"
        return UsageBlock(
            model=getattr(response, "model", None) or requested_model,
            input_tokens=getattr(usage, "input_tokens", 0) or 0,
            output_tokens=getattr(usage, "output_tokens", 0) or 0,
            cache_creation_input_tokens=getattr(usage, "cache_creation_input_tokens", 0) or 0,
            cache_read_input_tokens=getattr(usage, "cache_read_input_tokens", 0) or 0,
            request_id=getattr(response, "id", None),
            status=status,
            max_tokens=max_tokens,
        )

    def complete(self, *, model: str, system: str, user: str, max_tokens: int) -> Completion:
        response = self._client.messages.create(
            model=model,
            max_tokens=max_tokens,
            system=system,
            messages=[{"role": "user", "content": user}],
        )
        text = next((b.text for b in response.content if b.type == "text"), "")
        usage = self._usage(response, model, max_tokens)
        return Completion(
            text=text,
            input_tokens=usage.input_tokens,
            output_tokens=usage.output_tokens,
            usage=usage,
        )

    def structured(
        self, *, model: str, prompt: str, schema: dict, max_tokens: int
    ) -> StructuredResult:
        response = self._client.messages.create(
            model=model,
            max_tokens=max_tokens,
            output_config={"format": {"type": "json_schema", "schema": schema}},
            messages=[{"role": "user", "content": prompt}],
        )
        # Defensive extraction (mirrors complete()): a refusal carries no text block. Without a
        # default this raised StopIteration → opaque 500; surface a clear, typed error instead so the
        # caller sees why (the structured path can't produce a verdict from a refusal).
        text = next((b.text for b in response.content if b.type == "text"), None)
        if text is None:
            stop = getattr(response, "stop_reason", None)
            raise ValueError(f"structured call returned no text block (stop_reason={stop!r}).")
        return StructuredResult(data=json.loads(text), usage=self._usage(response, model, max_tokens))


class OpenAIProvider:
    """Adapter over the OpenAI SDK (``client.chat.completions.create``).

    The Anthropic ``system``/``user`` split maps to two chat messages; OpenAI reports
    ``prompt_tokens``/``completion_tokens``, normalized here to input/output. Structured
    output uses OpenAI's strict ``response_format`` json_schema (our schemas already set
    ``additionalProperties: false`` and mark every field required, which strict mode requires).
    """

    name = PROVIDER_OPENAI

    def __init__(self, client):
        self._client = client

    @staticmethod
    def _usage(response, requested_model: str, max_tokens: int) -> UsageBlock:
        usage = getattr(response, "usage", None)
        # OpenAI reports cached prompt tokens under usage.prompt_tokens_details.cached_tokens.
        details = getattr(usage, "prompt_tokens_details", None)
        cached = getattr(details, "cached_tokens", 0) or 0 if details is not None else 0
        # OpenAI's prompt_tokens INCLUDES the cached subset, whereas the ledger prices input_tokens at
        # the full input rate and cache_read_input_tokens at the cache-read rate. Report only the
        # NON-cached prompt tokens as input so the cached portion isn't billed twice (finding: cost
        # double-charge). Anthropic already separates the two, so its mapping is unchanged.
        prompt_tokens = getattr(usage, "prompt_tokens", 0) or 0
        return UsageBlock(
            model=getattr(response, "model", None) or requested_model,
            input_tokens=max(prompt_tokens - cached, 0),
            output_tokens=getattr(usage, "completion_tokens", 0) or 0,
            cache_read_input_tokens=cached,
            request_id=getattr(response, "id", None),
            status="success",
            max_tokens=max_tokens,
        )

    def complete(self, *, model: str, system: str, user: str, max_tokens: int) -> Completion:
        response = self._client.chat.completions.create(
            model=model,
            max_tokens=max_tokens,
            messages=[
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
        )
        usage = self._usage(response, model, max_tokens)
        return Completion(
            text=response.choices[0].message.content or "",
            input_tokens=usage.input_tokens,
            output_tokens=usage.output_tokens,
            usage=usage,
        )

    def structured(
        self, *, model: str, prompt: str, schema: dict, max_tokens: int
    ) -> StructuredResult:
        response = self._client.chat.completions.create(
            model=model,
            max_tokens=max_tokens,
            response_format={
                "type": "json_schema",
                "json_schema": {"name": "result", "schema": schema, "strict": True},
            },
            messages=[{"role": "user", "content": prompt}],
        )
        data = json.loads(response.choices[0].message.content)
        return StructuredResult(data=data, usage=self._usage(response, model, max_tokens))


class ProviderRegistry:
    """Holds the configured provider adapters and routes a model id to the right one."""

    def __init__(self, providers: dict[str, Provider]):
        self._providers = providers

    def for_model(self, model: str) -> Provider:
        name = resolve_provider(model)
        provider = self._providers.get(name)
        if provider is None:
            raise UnknownProviderError(
                f"Provider '{name}' for model '{model}' is not configured "
                f"(missing credentials?)."
            )
        return provider
