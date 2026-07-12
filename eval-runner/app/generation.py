"""Synthetic fixture generation.

Given N captured example fixtures plus operator guidance (coverage goals, edge-case /
adversarial targets, constraints), ask Claude to generate additional SLM-*shaped* inputs that
fill coverage gaps. Generation is **guided, not fixed**: the operator steers *what* gets
generated, seeded by the captured distribution so it doesn't drift off-distribution.

The model call uses structured output (never free-text parsing). The Anthropic client is
injected so tests can mock it at the boundary — no live API calls in the suite.
"""

import json
import os

from pydantic import BaseModel

# Default to the latest capable model; overridable via env for cost/latency tuning.
DEFAULT_MODEL = "claude-opus-4-8"


class SeedExample(BaseModel):
    input: str
    upstream_context: str | None = None
    expected_output: str | None = None


class GenerationGuidance(BaseModel):
    coverage_goals: str | None = None
    edge_cases: str | None = None
    constraints: str | None = None


class GenerateFixturesRequest(BaseModel):
    seed_examples: list[SeedExample]
    guidance: GenerationGuidance = GenerationGuidance()
    count: int = 5


class GeneratedFixture(BaseModel):
    input: str
    upstream_context: str | None = None
    expected_output: str | None = None
    # Index into the request's seed_examples that this fixture was derived from.
    seed_index: int | None = None


class GenerateFixturesResponse(BaseModel):
    fixtures: list[GeneratedFixture]


def model_name() -> str:
    return os.environ.get("EVAL_RUNNER_MODEL", DEFAULT_MODEL)


# JSON-schema for structured output. Nullable fields use anyOf (type-arrays and numeric/string
# constraints are not supported by structured outputs); every property is required and
# additionalProperties is false, as strict json_schema demands.
_NULLABLE_STRING = {"anyOf": [{"type": "string"}, {"type": "null"}]}
FIXTURES_SCHEMA = {
    "type": "object",
    "properties": {
        "fixtures": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "input": {"type": "string"},
                    "upstream_context": _NULLABLE_STRING,
                    "expected_output": _NULLABLE_STRING,
                    "seed_index": {"anyOf": [{"type": "integer"}, {"type": "null"}]},
                },
                "required": ["input", "upstream_context", "expected_output", "seed_index"],
                "additionalProperties": False,
            },
        }
    },
    "required": ["fixtures"],
    "additionalProperties": False,
}


def build_prompt(request: GenerateFixturesRequest) -> str:
    """Assemble the guided generation prompt. Seed examples anchor the distribution;
    operator guidance steers what gets generated on top of them."""
    lines: list[str] = [
        "You generate synthetic test fixtures for evaluating an LLM/SLM prompt.",
        "The fixtures must be shaped like the CAPTURED examples below — same kind of input, "
        "same upstream-context style — so the evaluation distribution matches production. "
        "Do not invent a new distribution; extrapolate from these seeds.",
        "",
        f"Generate {request.count} new fixture(s). For each, set seed_index to the 0-based "
        "index of the captured example it was derived from.",
        "",
        "CAPTURED EXAMPLES:",
    ]
    for i, seed in enumerate(request.seed_examples):
        lines.append(f"[{i}] input: {seed.input}")
        if seed.upstream_context is not None:
            lines.append(f"    upstream_context: {seed.upstream_context}")
        if seed.expected_output is not None:
            lines.append(f"    expected_output: {seed.expected_output}")

    guidance = request.guidance
    if any([guidance.coverage_goals, guidance.edge_cases, guidance.constraints]):
        lines.append("")
        lines.append("OPERATOR GUIDANCE — steer generation toward this:")
        if guidance.coverage_goals:
            lines.append(f"- Coverage goals: {guidance.coverage_goals}")
        if guidance.edge_cases:
            lines.append(f"- Edge cases / adversarial variants to target: {guidance.edge_cases}")
        if guidance.constraints:
            lines.append(f"- Constraints: {guidance.constraints}")

    return "\n".join(lines)


def generate_fixtures(client, request: GenerateFixturesRequest) -> GenerateFixturesResponse:
    """Call Claude with structured output and return the generated fixtures."""
    response = client.messages.create(
        model=model_name(),
        max_tokens=8192,
        output_config={"format": {"type": "json_schema", "schema": FIXTURES_SCHEMA}},
        messages=[{"role": "user", "content": build_prompt(request)}],
    )

    # Structured output guarantees the first text block is JSON matching FIXTURES_SCHEMA.
    text = next(block.text for block in response.content if block.type == "text")
    return GenerateFixturesResponse.model_validate(json.loads(text))
