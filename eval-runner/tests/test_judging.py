"""LLM-judge tests. The Anthropic client is mocked at the boundary — no live API calls."""

import json
from types import SimpleNamespace

from fastapi.testclient import TestClient

from app.main import app, get_anthropic_client


class FakeMessages:
    def __init__(self, verdict: dict):
        self._verdict = verdict
        self.last_kwargs: dict | None = None

    def create(self, **kwargs):
        self.last_kwargs = kwargs
        return SimpleNamespace(
            content=[SimpleNamespace(type="text", text=json.dumps(self._verdict))]
        )


class FakeClient:
    def __init__(self, verdict: dict):
        self.messages = FakeMessages(verdict)


def client_with(fake: FakeClient) -> TestClient:
    app.dependency_overrides[get_anthropic_client] = lambda: fake
    return TestClient(app)


def teardown_function():
    app.dependency_overrides.clear()


def test_returns_structured_verdict():
    fake = FakeClient({"score": 0.8, "passed": True, "rationale": "mostly accurate"})
    resp = client_with(fake).post(
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
    body = resp.json()
    assert body == {"score": 0.8, "passed": True, "rationale": "mostly accurate"}


def test_requests_structured_output_and_includes_rubric_and_io():
    fake = FakeClient({"score": 1.0, "passed": True, "rationale": "ok"})
    client_with(fake).post(
        "/judge",
        json={
            "rubric": "GRADE ON HELPFULNESS",
            "input": "the question",
            "output": "the answer",
            "model": "claude-haiku-4-5",
        },
    )

    kwargs = fake.messages.last_kwargs
    assert kwargs["model"] == "claude-haiku-4-5"
    assert kwargs["output_config"]["format"]["type"] == "json_schema"
    prompt = kwargs["messages"][0]["content"]
    assert "GRADE ON HELPFULNESS" in prompt
    assert "the question" in prompt
    assert "the answer" in prompt


def test_score_is_clamped_into_the_unit_interval():
    fake = FakeClient({"score": 1.5, "passed": True, "rationale": "over"})
    resp = client_with(fake).post(
        "/judge",
        json={"rubric": "r", "input": "i", "output": "o", "model": "claude-opus-4-8"},
    )

    assert resp.json()["score"] == 1.0


def test_stub_mode_judges_without_a_client(monkeypatch):
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
