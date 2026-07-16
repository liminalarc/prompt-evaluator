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


@dataclass
class Completion:
    """A plain text completion plus normalized token usage (provider-agnostic)."""

    text: str
    input_tokens: int
    output_tokens: int


@runtime_checkable
class Provider(Protocol):
    name: str

    def complete(self, *, model: str, system: str, user: str, max_tokens: int) -> Completion:
        """Run a plain text completion: `system` prompt + a single `user` turn."""
        ...

    def structured(self, *, model: str, prompt: str, schema: dict, max_tokens: int) -> dict:
        """Run a structured-output completion and return the parsed object matching `schema`."""
        ...


class AnthropicProvider:
    """Adapter over the Anthropic SDK (``client.messages.create``)."""

    name = PROVIDER_ANTHROPIC

    def __init__(self, client):
        self._client = client

    def complete(self, *, model: str, system: str, user: str, max_tokens: int) -> Completion:
        response = self._client.messages.create(
            model=model,
            max_tokens=max_tokens,
            system=system,
            messages=[{"role": "user", "content": user}],
        )
        text = next((b.text for b in response.content if b.type == "text"), "")
        return Completion(
            text=text,
            input_tokens=getattr(response.usage, "input_tokens", 0),
            output_tokens=getattr(response.usage, "output_tokens", 0),
        )

    def structured(self, *, model: str, prompt: str, schema: dict, max_tokens: int) -> dict:
        response = self._client.messages.create(
            model=model,
            max_tokens=max_tokens,
            output_config={"format": {"type": "json_schema", "schema": schema}},
            messages=[{"role": "user", "content": prompt}],
        )
        text = next(b.text for b in response.content if b.type == "text")
        return json.loads(text)


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

    def complete(self, *, model: str, system: str, user: str, max_tokens: int) -> Completion:
        response = self._client.chat.completions.create(
            model=model,
            max_tokens=max_tokens,
            messages=[
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
        )
        usage = response.usage
        return Completion(
            text=response.choices[0].message.content or "",
            input_tokens=getattr(usage, "prompt_tokens", 0),
            output_tokens=getattr(usage, "completion_tokens", 0),
        )

    def structured(self, *, model: str, prompt: str, schema: dict, max_tokens: int) -> dict:
        response = self._client.chat.completions.create(
            model=model,
            max_tokens=max_tokens,
            response_format={
                "type": "json_schema",
                "json_schema": {"name": "result", "schema": schema, "strict": True},
            },
            messages=[{"role": "user", "content": prompt}],
        )
        return json.loads(response.choices[0].message.content)


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
