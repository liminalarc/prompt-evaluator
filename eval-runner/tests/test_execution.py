"""Prompt-execution tests. The Anthropic client is mocked at the boundary — no live API calls."""

from types import SimpleNamespace

from fastapi.testclient import TestClient

from app.execution import estimate_cost
from app.main import app, get_anthropic_client


class FakeMessages:
    def __init__(self, text: str, input_tokens: int, output_tokens: int):
        self._text = text
        self._usage = SimpleNamespace(input_tokens=input_tokens, output_tokens=output_tokens)
        self.last_kwargs: dict | None = None

    def create(self, **kwargs):
        self.last_kwargs = kwargs
        return SimpleNamespace(
            content=[SimpleNamespace(type="text", text=self._text)],
            usage=self._usage,
        )


class FakeClient:
    def __init__(self, text: str, input_tokens: int = 1000, output_tokens: int = 500):
        self.messages = FakeMessages(text, input_tokens, output_tokens)


def client_with(fake: FakeClient) -> TestClient:
    app.dependency_overrides[get_anthropic_client] = lambda: fake
    return TestClient(app)


def teardown_function():
    app.dependency_overrides.clear()


def test_executes_prompt_and_returns_output_latency_and_cost():
    fake = FakeClient("the summary", input_tokens=1000, output_tokens=500)
    resp = client_with(fake).post(
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
    fake = FakeClient("out")
    client_with(fake).post(
        "/execute-prompt",
        json={
            "prompt": "SYSTEM RULES",
            "model": "claude-sonnet-5",
            "input": "the fixture input",
            "upstream_context": "raw slm output",
        },
    )

    kwargs = fake.messages.last_kwargs
    assert kwargs["system"] == "SYSTEM RULES"
    assert kwargs["model"] == "claude-sonnet-5"
    user_message = kwargs["messages"][0]["content"]
    assert "the fixture input" in user_message
    assert "raw slm output" in user_message  # upstream context is prepended


def test_cost_is_null_for_an_unknown_model():
    fake = FakeClient("out")
    resp = client_with(fake).post(
        "/execute-prompt",
        json={"prompt": "p", "model": "some-slm-v3", "input": "in"},
    )

    assert resp.json()["cost_usd"] is None


def test_stub_mode_executes_without_a_client(monkeypatch):
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
    assert estimate_cost("unknown", 10, 10) is None
