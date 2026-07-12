"""Synthetic fixture generation tests. The Anthropic client is mocked at the boundary —
no live API calls."""

import json
from types import SimpleNamespace

from fastapi.testclient import TestClient

from app.generation import (
    GenerateFixturesRequest,
    GenerationGuidance,
    SeedExample,
    build_prompt,
)
from app.main import app, get_anthropic_client


class FakeMessages:
    """Records the create() call and returns a canned structured-output response."""

    def __init__(self, fixtures: list[dict]):
        self._fixtures = fixtures
        self.last_kwargs: dict | None = None

    def create(self, **kwargs):
        self.last_kwargs = kwargs
        payload = json.dumps({"fixtures": self._fixtures})
        return SimpleNamespace(content=[SimpleNamespace(type="text", text=payload)])


class FakeClient:
    def __init__(self, fixtures: list[dict]):
        self.messages = FakeMessages(fixtures)


def client_with(fake: FakeClient) -> TestClient:
    app.dependency_overrides[get_anthropic_client] = lambda: fake
    return TestClient(app)


def teardown_function():
    app.dependency_overrides.clear()


def test_generates_slm_shaped_fixtures_tagged_and_linked_to_seeds():
    fake = FakeClient(
        [
            {
                "input": "summarize this other thread",
                "upstream_context": "slm-shaped output",
                "expected_output": None,
                "seed_index": 0,
            }
        ]
    )
    resp = client_with(fake).post(
        "/generate-fixtures",
        json={
            "seed_examples": [
                {"input": "summarize this thread", "upstream_context": "raw slm output"}
            ],
            "count": 1,
        },
    )

    assert resp.status_code == 200
    body = resp.json()
    fixture = body["fixtures"][0]
    assert fixture["input"] == "summarize this other thread"
    assert fixture["upstream_context"] == "slm-shaped output"
    # Linked back to the captured seed it was derived from.
    assert fixture["seed_index"] == 0


def test_seed_examples_anchor_the_prompt():
    fake = FakeClient([])
    client_with(fake).post(
        "/generate-fixtures",
        json={
            "seed_examples": [{"input": "captured example input"}],
            "count": 3,
        },
    )

    prompt = fake.messages.last_kwargs["messages"][0]["content"]
    assert "captured example input" in prompt
    # Structured output is requested, not free-text parsing.
    assert fake.messages.last_kwargs["output_config"]["format"]["type"] == "json_schema"


def test_operator_guidance_is_reflected_in_the_request():
    fake = FakeClient([])
    client_with(fake).post(
        "/generate-fixtures",
        json={
            "seed_examples": [{"input": "seed"}],
            "guidance": {
                "coverage_goals": "cover multi-language inputs",
                "edge_cases": "empty and adversarial prompts",
                "constraints": "keep under 200 tokens",
            },
            "count": 2,
        },
    )

    prompt = fake.messages.last_kwargs["messages"][0]["content"]
    assert "cover multi-language inputs" in prompt
    assert "empty and adversarial prompts" in prompt
    assert "keep under 200 tokens" in prompt


def test_stub_mode_returns_deterministic_fixtures_without_a_client(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    app.dependency_overrides.clear()  # exercise the real get_anthropic_client (stub branch)
    resp = TestClient(app).post(
        "/generate-fixtures",
        json={"seed_examples": [{"input": "captured seed"}], "count": 2},
    )

    assert resp.status_code == 200
    fixtures = resp.json()["fixtures"]
    assert len(fixtures) == 2
    assert all(f["input"].startswith("[synthetic] ") for f in fixtures)
    assert all(f["seed_index"] == 0 for f in fixtures)


def test_build_prompt_omits_guidance_section_when_empty():
    request = GenerateFixturesRequest(
        seed_examples=[SeedExample(input="seed")],
        guidance=GenerationGuidance(),
        count=1,
    )
    prompt = build_prompt(request)
    assert "OPERATOR GUIDANCE" not in prompt
    assert "seed" in prompt
