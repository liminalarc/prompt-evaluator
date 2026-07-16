"""Synthetic fixture generation tests. The provider registry is mocked at the boundary —
no live API calls."""

from fastapi.testclient import TestClient

from app.generation import (
    FIXTURES_SCHEMA,
    GenerateFixturesRequest,
    GenerationGuidance,
    SeedExample,
    build_prompt,
)
from app.main import app, get_provider_registry


class FakeProvider:
    """Records structured() calls and returns canned fixtures."""

    name = "fake"

    def __init__(self, fixtures: list[dict]):
        self._fixtures = fixtures
        self.calls: list[dict] = []

    def structured(self, *, model, prompt, schema, max_tokens):
        self.calls.append({"model": model, "prompt": prompt, "schema": schema})
        return {"fixtures": [dict(f) for f in self._fixtures]}

    def complete(self, **kwargs):  # not used by the generation path
        raise AssertionError("generation must not call complete()")


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


def test_generates_slm_shaped_fixtures_tagged_and_linked_to_seeds():
    provider = FakeProvider(
        [
            {
                "input": "summarize this other thread",
                "upstream_context": "slm-shaped output",
                "expected_output": None,
                "seed_index": 0,
            }
        ]
    )
    resp = client_with(provider).post(
        "/generate-fixtures",
        json={
            "seed_examples": [
                {"input": "summarize this thread", "upstream_context": "raw slm output"}
            ],
            "count": 1,
        },
    )

    assert resp.status_code == 200
    fixture = resp.json()["fixtures"][0]
    assert fixture["input"] == "summarize this other thread"
    assert fixture["upstream_context"] == "slm-shaped output"
    assert fixture["seed_index"] == 0


def test_seed_examples_anchor_the_prompt_and_structured_output_is_requested():
    provider = FakeProvider([])
    client_with(provider).post(
        "/generate-fixtures",
        json={"seed_examples": [{"input": "captured example input"}], "count": 3},
    )

    call = provider.calls[0]
    assert "captured example input" in call["prompt"]
    # Structured output is requested (native schema), not free-text parsing.
    assert call["schema"] == FIXTURES_SCHEMA


def test_operator_guidance_is_reflected_in_the_request():
    provider = FakeProvider([])
    client_with(provider).post(
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

    prompt = provider.calls[0]["prompt"]
    assert "cover multi-language inputs" in prompt
    assert "empty and adversarial prompts" in prompt
    assert "keep under 200 tokens" in prompt


def test_defaults_to_claude_generator_but_honors_a_chosen_model():
    default_provider = FakeProvider([])
    client_with(default_provider).post(
        "/generate-fixtures", json={"seed_examples": [{"input": "seed"}], "count": 1}
    )
    assert default_provider.calls[0]["model"] == "claude-opus-4-8"

    chosen_provider = FakeProvider([])
    client_with(chosen_provider).post(
        "/generate-fixtures",
        json={"seed_examples": [{"input": "seed"}], "count": 1, "model": "gpt-4o"},
    )
    assert chosen_provider.calls[0]["model"] == "gpt-4o"


def test_stub_mode_returns_deterministic_fixtures_without_a_registry(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    app.dependency_overrides.clear()  # exercise the real get_provider_registry (stub branch)
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
