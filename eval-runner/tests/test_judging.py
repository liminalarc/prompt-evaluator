"""LLM-judge tests. The provider registry is mocked at the boundary — no live API calls."""

from fastapi.testclient import TestClient

from app.judging import JUDGE_MAX_TOKENS, VERDICT_SCHEMA
from app.main import app, get_provider_registry


class FakeProvider:
    """Records structured() calls and returns a canned verdict."""

    name = "fake"

    def __init__(self, verdict: dict):
        self._verdict = verdict
        self.calls: list[dict] = []

    def structured(self, *, model, prompt, schema, max_tokens):
        self.calls.append(
            {"model": model, "prompt": prompt, "schema": schema, "max_tokens": max_tokens}
        )
        return dict(self._verdict)

    def complete(self, **kwargs):  # not used by the judge path
        raise AssertionError("judge must not call complete()")


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


def test_returns_structured_verdict():
    provider = FakeProvider({"score": 0.8, "passed": True, "rationale": "mostly accurate"})
    resp = client_with(provider).post(
        "/judge",
        json={
            "rubric": "Is the answer factually correct?",
            "input": "What is 2+2?",
            "output": "4",
            "expected": "4",
            "model": "claude-opus-4-8",
        },
    )

    assert resp.status_code == 200
    assert resp.json() == {"score": 0.8, "passed": True, "rationale": "mostly accurate"}
    # Structured output requested via the provider, using the verdict schema.
    assert provider.calls[0]["schema"] == VERDICT_SCHEMA
    assert provider.calls[0]["model"] == "claude-opus-4-8"


def test_requests_structured_output_and_includes_rubric_and_io():
    provider = FakeProvider({"score": 1.0, "passed": True, "rationale": "ok"})
    client_with(provider).post(
        "/judge",
        json={
            "rubric": "GRADE ON HELPFULNESS",
            "input": "the question",
            "output": "the answer",
            "model": "claude-haiku-4-5",
        },
    )

    call = provider.calls[0]
    assert call["model"] == "claude-haiku-4-5"
    prompt = call["prompt"]
    assert "GRADE ON HELPFULNESS" in prompt
    assert "the question" in prompt
    assert "the answer" in prompt


def test_judge_budgets_for_thinking_on_by_default_models():
    # Judge models whose thinking is ON by default (claude-sonnet-5, claude-fable-5) spend part of
    # the output budget on adaptive thinking before emitting the structured verdict. Too small a
    # budget truncates the verdict JSON mid-string (JSONDecodeError -> 500). The judge must request
    # enough headroom for thinking + the small verdict. Regression guard for finding 5.1/B6.
    provider = FakeProvider({"score": 0.7, "passed": True, "rationale": "ok"})
    client_with(provider).post(
        "/judge",
        json={"rubric": "r", "input": "i", "output": "o", "model": "claude-sonnet-5"},
    )
    assert provider.calls[0]["max_tokens"] == JUDGE_MAX_TOKENS
    assert JUDGE_MAX_TOKENS >= 4096  # room for adaptive thinking beyond the tiny verdict


def test_score_is_clamped_into_the_unit_interval():
    provider = FakeProvider({"score": 1.5, "passed": True, "rationale": "over"})
    resp = client_with(provider).post(
        "/judge",
        json={"rubric": "r", "input": "i", "output": "o", "model": "claude-opus-4-8"},
    )

    assert resp.json()["score"] == 1.0


def test_defaults_to_claude_judge_model_but_honors_a_chosen_model():
    # Default judge is Claude (ties to 1.3: model is part of Scorer identity).
    default_provider = FakeProvider({"score": 1.0, "passed": True, "rationale": "ok"})
    client_with(default_provider).post(
        "/judge", json={"rubric": "r", "input": "i", "output": "o"}
    )
    assert default_provider.calls[0]["model"] == "claude-opus-4-8"

    # A chosen model flows through verbatim — a distinct model is a distinct series.
    chosen_provider = FakeProvider({"score": 1.0, "passed": True, "rationale": "ok"})
    client_with(chosen_provider).post(
        "/judge", json={"rubric": "r", "input": "i", "output": "o", "model": "gpt-4o"}
    )
    assert chosen_provider.calls[0]["model"] == "gpt-4o"


def test_stub_mode_judges_without_a_registry(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    app.dependency_overrides.clear()
    client = TestClient(app)

    non_empty = client.post(
        "/judge", json={"rubric": "r", "input": "i", "output": "an answer"}
    ).json()
    empty = client.post(
        "/judge", json={"rubric": "r", "input": "i", "output": "   "}
    ).json()

    assert non_empty["passed"] is True
    assert non_empty["score"] == 1.0
    assert empty["passed"] is False
    assert empty["score"] == 0.0
