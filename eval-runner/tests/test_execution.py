"""Prompt-execution tests. The provider registry is mocked at the boundary — no live calls."""

from fastapi.testclient import TestClient

from app.execution import estimate_cost
from app.main import app, get_provider_registry
from app.providers import Completion, ProviderRegistry


class FakeProvider:
    """Records complete() calls and returns a canned completion."""

    name = "fake"

    def __init__(self, completion: Completion):
        self._completion = completion
        self.calls: list[dict] = []

    def complete(self, *, model, system, user, max_tokens):
        self.calls.append(
            {"model": model, "system": system, "user": user, "max_tokens": max_tokens}
        )
        return self._completion

    def structured(self, **kwargs):  # not used by the execution path
        raise AssertionError("execution must not call structured()")


class RaisingProvider:
    """Fails on any call — proves the captured-output path makes no provider call."""

    name = "raising"

    def complete(self, **kwargs):
        raise AssertionError("captured-output path must not call complete()")

    def structured(self, **kwargs):
        raise AssertionError("captured-output path must not call structured()")


class FakeRegistry:
    def __init__(self, provider):
        self._provider = provider

    def for_model(self, model):
        return self._provider


def client_with(provider) -> TestClient:
    app.dependency_overrides[get_provider_registry] = lambda: FakeRegistry(provider)
    return TestClient(app)


def teardown_function():
    app.dependency_overrides.clear()


def test_executes_prompt_and_returns_output_latency_and_cost():
    provider = FakeProvider(Completion(text="the summary", input_tokens=1000, output_tokens=500))
    resp = client_with(provider).post(
        "/execute-prompt",
        json={"prompt": "You summarize text.", "model": "claude-opus-4-8", "input": "long article"},
    )

    assert resp.status_code == 200
    body = resp.json()
    assert body["output"] == "the summary"
    assert body["input_tokens"] == 1000
    assert body["output_tokens"] == 500
    # Opus 4.8: 1000/1e6*5 + 500/1e6*25 = 0.005 + 0.0125 = 0.0175
    assert body["cost_usd"] == 0.0175
    assert body["latency_ms"] >= 0


def test_prompt_content_is_the_system_and_input_is_the_user_turn():
    provider = FakeProvider(Completion(text="out", input_tokens=1, output_tokens=1))
    client_with(provider).post(
        "/execute-prompt",
        json={
            "prompt": "SYSTEM RULES",
            "model": "claude-sonnet-5",
            "input": "the fixture input",
            "upstream_context": "raw slm output",
        },
    )

    call = provider.calls[0]
    assert call["model"] == "claude-sonnet-5"
    assert call["system"] == "SYSTEM RULES"
    assert "the fixture input" in call["user"]
    assert "raw slm output" in call["user"]  # upstream context is prepended


def test_executes_on_a_non_claude_provider_selected_by_model_id():
    # A gpt-* model routes to the OpenAI provider (proven end-to-end via the fake).
    provider = FakeProvider(Completion(text="openai output", input_tokens=10, output_tokens=4))
    resp = client_with(provider).post(
        "/execute-prompt",
        json={"prompt": "p", "model": "gpt-4o", "input": "in"},
    )

    assert resp.status_code == 200
    assert resp.json()["output"] == "openai output"
    assert provider.calls[0]["model"] == "gpt-4o"


def test_captured_output_is_scored_with_no_live_call():
    # Capture-first (1.2): a captured output is returned as-is; the provider is never called.
    resp = client_with(RaisingProvider()).post(
        "/execute-prompt",
        json={
            "prompt": "p",
            "model": "claude-opus-4-8",
            "input": "in",
            "captured_output": "the real captured output",
        },
    )

    assert resp.status_code == 200
    body = resp.json()
    assert body["output"] == "the real captured output"
    assert body["input_tokens"] == 0
    assert body["output_tokens"] == 0
    assert body["cost_usd"] == 0.0
    assert body["latency_ms"] == 0


def test_cost_is_null_for_a_routable_but_unpriced_model():
    provider = FakeProvider(Completion(text="out", input_tokens=10, output_tokens=10))
    resp = client_with(provider).post(
        "/execute-prompt",
        json={"prompt": "p", "model": "gpt-4-turbo", "input": "in"},
    )

    assert resp.json()["cost_usd"] is None


def test_unroutable_model_fails_clearly():
    # The real registry routes by model id; an unrecognized id is a clear 400, not a 500.
    app.dependency_overrides[get_provider_registry] = lambda: ProviderRegistry({})
    resp = TestClient(app).post(
        "/execute-prompt",
        json={"prompt": "p", "model": "some-slm-v3", "input": "in"},
    )

    assert resp.status_code == 400
    assert "some-slm-v3" in resp.json()["detail"]


def test_stub_mode_executes_without_a_registry(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    app.dependency_overrides.clear()
    resp = TestClient(app).post(
        "/execute-prompt",
        json={"prompt": "p", "model": "claude-opus-4-8", "input": "hello"},
    )

    assert resp.status_code == 200
    body = resp.json()
    assert body["output"] == "[executed:claude-opus-4-8] hello"
    assert body["cost_usd"] == 0.0


def test_estimate_cost_unit():
    assert estimate_cost("claude-haiku-4-5", 1_000_000, 1_000_000) == 6.0
    assert estimate_cost("gpt-4o", 1_000_000, 1_000_000) == 12.5
    assert estimate_cost("unknown", 10, 10) is None
