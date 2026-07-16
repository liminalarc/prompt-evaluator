"""Provider-routing tests. Vendor SDK clients are mocked at the boundary — no live calls.

Slice 1 of 1.5: a `Provider` protocol + a model-id → provider registry, with thin
per-provider adapters (Anthropic, OpenAI). The endpoints are rewired onto this in later
slices; here we test the abstraction in isolation.
"""

import json
from types import SimpleNamespace

import pytest

from app.providers import (
    AnthropicProvider,
    Completion,
    OpenAIProvider,
    ProviderRegistry,
    UnknownProviderError,
    resolve_provider,
)


# --- Anthropic fakes (native shape: content blocks + usage.input_tokens) ---
class FakeAnthropicMessages:
    def __init__(self, text: str, input_tokens: int = 11, output_tokens: int = 7):
        self._text = text
        self._usage = SimpleNamespace(input_tokens=input_tokens, output_tokens=output_tokens)
        self.last_kwargs: dict | None = None

    def create(self, **kwargs):
        self.last_kwargs = kwargs
        return SimpleNamespace(
            content=[SimpleNamespace(type="text", text=self._text)],
            usage=self._usage,
        )


class FakeAnthropicClient:
    def __init__(self, text: str, **usage):
        self.messages = FakeAnthropicMessages(text, **usage)


# --- OpenAI fakes (native shape: choices[].message.content + usage.prompt_tokens) ---
class FakeOpenAICompletions:
    def __init__(self, text: str, prompt_tokens: int = 13, completion_tokens: int = 5):
        self._text = text
        self._usage = SimpleNamespace(
            prompt_tokens=prompt_tokens, completion_tokens=completion_tokens
        )
        self.last_kwargs: dict | None = None

    def create(self, **kwargs):
        self.last_kwargs = kwargs
        message = SimpleNamespace(content=self._text)
        return SimpleNamespace(
            choices=[SimpleNamespace(message=message)],
            usage=self._usage,
        )


class FakeOpenAIClient:
    def __init__(self, text: str, **usage):
        self.chat = SimpleNamespace(completions=FakeOpenAICompletions(text, **usage))


# --- resolve_provider: routing by model id ---
@pytest.mark.parametrize(
    ("model", "expected"),
    [
        ("claude-opus-4-8", "anthropic"),
        ("claude-sonnet-5", "anthropic"),
        ("gpt-4o", "openai"),
        ("gpt-4o-mini", "openai"),
        ("o1-preview", "openai"),
    ],
)
def test_resolve_provider_routes_by_model_id(model, expected):
    assert resolve_provider(model) == expected


def test_resolve_provider_rejects_unknown_model():
    with pytest.raises(UnknownProviderError):
        resolve_provider("some-slm-v3")


# --- Anthropic adapter ---
def test_anthropic_complete_uses_system_and_user_and_reports_usage():
    client = FakeAnthropicClient("the answer", input_tokens=1000, output_tokens=500)
    provider = AnthropicProvider(client)

    result = provider.complete(
        model="claude-opus-4-8", system="SYSTEM", user="the input", max_tokens=256
    )

    assert isinstance(result, Completion)
    assert result.text == "the answer"
    assert result.input_tokens == 1000
    assert result.output_tokens == 500
    kwargs = client.messages.last_kwargs
    assert kwargs["model"] == "claude-opus-4-8"
    assert kwargs["system"] == "SYSTEM"
    assert kwargs["messages"][0]["content"] == "the input"
    assert kwargs["max_tokens"] == 256


def test_anthropic_structured_uses_native_json_schema_output_config():
    client = FakeAnthropicClient(json.dumps({"score": 0.5}))
    provider = AnthropicProvider(client)
    schema = {"type": "object", "properties": {"score": {"type": "number"}}}

    data = provider.structured(
        model="claude-opus-4-8", prompt="grade this", schema=schema, max_tokens=64
    )

    assert data == {"score": 0.5}
    kwargs = client.messages.last_kwargs
    # Native Anthropic structured output, never free-text parsing.
    assert kwargs["output_config"]["format"]["type"] == "json_schema"
    assert kwargs["output_config"]["format"]["schema"] == schema
    assert kwargs["messages"][0]["content"] == "grade this"


# --- OpenAI adapter ---
def test_openai_complete_maps_system_user_and_token_usage():
    client = FakeOpenAIClient("the answer", prompt_tokens=1000, completion_tokens=500)
    provider = OpenAIProvider(client)

    result = provider.complete(
        model="gpt-4o", system="SYSTEM", user="the input", max_tokens=256
    )

    assert result.text == "the answer"
    # OpenAI reports prompt/completion tokens; normalized to input/output.
    assert result.input_tokens == 1000
    assert result.output_tokens == 500
    kwargs = client.chat.completions.last_kwargs
    assert kwargs["model"] == "gpt-4o"
    roles = [m["role"] for m in kwargs["messages"]]
    assert roles == ["system", "user"]
    assert kwargs["messages"][0]["content"] == "SYSTEM"
    assert kwargs["messages"][1]["content"] == "the input"


def test_openai_structured_uses_native_json_schema_response_format():
    client = FakeOpenAIClient(json.dumps({"score": 0.9}))
    provider = OpenAIProvider(client)
    schema = {"type": "object", "properties": {"score": {"type": "number"}}}

    data = provider.structured(model="gpt-4o", prompt="grade this", schema=schema, max_tokens=64)

    assert data == {"score": 0.9}
    fmt = client.chat.completions.last_kwargs["response_format"]
    # Native OpenAI structured output (json_schema), never free-text parsing.
    assert fmt["type"] == "json_schema"
    assert fmt["json_schema"]["schema"] == schema
    assert fmt["json_schema"]["strict"] is True


# --- Registry: model id -> configured provider ---
def test_registry_routes_model_to_the_matching_provider():
    anthropic = AnthropicProvider(FakeAnthropicClient("a"))
    openai = OpenAIProvider(FakeOpenAIClient("b"))
    registry = ProviderRegistry({"anthropic": anthropic, "openai": openai})

    assert registry.for_model("claude-opus-4-8") is anthropic
    assert registry.for_model("gpt-4o") is openai


def test_registry_raises_when_provider_not_configured():
    # Model routes to openai, but no openai client is configured (missing creds).
    registry = ProviderRegistry({"anthropic": AnthropicProvider(FakeAnthropicClient("a"))})
    with pytest.raises(UnknownProviderError):
        registry.for_model("gpt-4o")
