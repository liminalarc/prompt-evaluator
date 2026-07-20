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
    StructuredResult,
    UnknownProviderError,
    resolve_provider,
)


# --- Anthropic fakes (native shape: content blocks + usage.input_tokens) ---
class FakeAnthropicMessages:
    def __init__(
        self,
        text: str,
        input_tokens: int = 11,
        output_tokens: int = 7,
        cache_creation_input_tokens: int = 0,
        cache_read_input_tokens: int = 0,
        response_id: str = "msg_fake",
        model: str = "claude-opus-4-8",
    ):
        self._text = text
        self._usage = SimpleNamespace(
            input_tokens=input_tokens,
            output_tokens=output_tokens,
            cache_creation_input_tokens=cache_creation_input_tokens,
            cache_read_input_tokens=cache_read_input_tokens,
        )
        self._response_id = response_id
        self._model = model
        self.last_kwargs: dict | None = None

    def create(self, **kwargs):
        self.last_kwargs = kwargs
        return SimpleNamespace(
            content=[SimpleNamespace(type="text", text=self._text)],
            usage=self._usage,
            id=self._response_id,
            model=self._model,
            stop_reason="end_turn",
        )


class FakeAnthropicClient:
    def __init__(self, text: str, **usage):
        self.messages = FakeAnthropicMessages(text, **usage)


# --- OpenAI fakes (native shape: choices[].message.content + usage.prompt_tokens) ---
class FakeOpenAICompletions:
    def __init__(
        self,
        text: str,
        prompt_tokens: int = 13,
        completion_tokens: int = 5,
        cached_tokens: int = 0,
        response_id: str = "chatcmpl_fake",
        model: str = "gpt-4o",
    ):
        self._text = text
        self._usage = SimpleNamespace(
            prompt_tokens=prompt_tokens,
            completion_tokens=completion_tokens,
            prompt_tokens_details=SimpleNamespace(cached_tokens=cached_tokens),
        )
        self._response_id = response_id
        self._model = model
        self.last_kwargs: dict | None = None

    def create(self, **kwargs):
        self.last_kwargs = kwargs
        message = SimpleNamespace(content=self._text)
        return SimpleNamespace(
            choices=[SimpleNamespace(message=message)],
            usage=self._usage,
            id=self._response_id,
            model=self._model,
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


def test_anthropic_complete_reports_the_full_usage_block():
    client = FakeAnthropicClient(
        "the answer",
        input_tokens=1000,
        output_tokens=500,
        cache_creation_input_tokens=40,
        cache_read_input_tokens=960,
    )
    provider = AnthropicProvider(client)

    result = provider.complete(
        model="claude-opus-4-8", system="s", user="u", max_tokens=256
    )

    usage = result.usage
    assert usage is not None
    assert usage.model == "claude-opus-4-8"
    assert usage.input_tokens == 1000
    assert usage.output_tokens == 500
    assert usage.cache_creation_input_tokens == 40
    assert usage.cache_read_input_tokens == 960
    assert usage.request_id == "msg_fake"
    assert usage.status == "success"
    assert usage.max_tokens == 256


def test_anthropic_structured_uses_native_json_schema_output_config():
    client = FakeAnthropicClient(json.dumps({"score": 0.5}))
    provider = AnthropicProvider(client)
    schema = {"type": "object", "properties": {"score": {"type": "number"}}}

    result = provider.structured(
        model="claude-opus-4-8", prompt="grade this", schema=schema, max_tokens=64
    )

    assert isinstance(result, StructuredResult)
    assert result.data == {"score": 0.5}
    # The call's usage rides alongside the parsed verdict (6.1).
    assert result.usage.model == "claude-opus-4-8"
    assert result.usage.max_tokens == 64
    assert result.usage.status == "success"
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
    client = FakeOpenAIClient(json.dumps({"score": 0.9}), cached_tokens=7)
    provider = OpenAIProvider(client)
    schema = {"type": "object", "properties": {"score": {"type": "number"}}}

    result = provider.structured(model="gpt-4o", prompt="grade this", schema=schema, max_tokens=64)

    assert isinstance(result, StructuredResult)
    assert result.data == {"score": 0.9}
    # OpenAI cached prompt tokens map to cache_read_input_tokens (6.1).
    assert result.usage.cache_read_input_tokens == 7
    assert result.usage.request_id == "chatcmpl_fake"
    assert result.usage.model == "gpt-4o"
    fmt = client.chat.completions.last_kwargs["response_format"]
    # Native OpenAI structured output (json_schema), never free-text parsing.
    assert fmt["type"] == "json_schema"
    assert fmt["json_schema"]["schema"] == schema
    assert fmt["json_schema"]["strict"] is True


def test_openai_usage_excludes_cached_tokens_from_input():
    # OpenAI's prompt_tokens includes the cached subset; report only the non-cached portion as input
    # so the ledger doesn't bill cached tokens at both the input and cache-read rate (cost fix).
    client = FakeOpenAIClient("ok", prompt_tokens=1000, completion_tokens=50, cached_tokens=200)
    provider = OpenAIProvider(client)

    result = provider.complete(model="gpt-4o", system="s", user="u", max_tokens=64)

    assert result.usage.input_tokens == 800  # 1000 prompt - 200 cached
    assert result.usage.cache_read_input_tokens == 200
    assert result.input_tokens == 800  # the flat field mirrors the usage block


class _RefusalAnthropicClient:
    """An Anthropic client whose response carries no text block (a refusal)."""

    class _Messages:
        last_kwargs: dict | None = None

        def create(self, **kwargs):
            self.last_kwargs = kwargs
            return SimpleNamespace(
                content=[], usage=SimpleNamespace(input_tokens=5, output_tokens=0),
                id="msg_refusal", model="claude-opus-4-8", stop_reason="refusal",
            )

    def __init__(self):
        self.messages = self._Messages()


def test_anthropic_structured_raises_a_clear_error_on_a_refusal():
    # A refusal has no text block; extraction must not raise a bare StopIteration (opaque 500).
    provider = AnthropicProvider(_RefusalAnthropicClient())

    with pytest.raises(ValueError, match="refusal"):
        provider.structured(
            model="claude-opus-4-8", prompt="p", schema={"type": "object"}, max_tokens=64
        )


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
