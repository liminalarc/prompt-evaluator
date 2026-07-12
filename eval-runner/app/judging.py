"""LLM-judge scoring — given a rubric plus the fixture input, the model output, and an optional
expected output, ask Claude to return a structured verdict (score, pass/fail, rationale).

The verdict uses structured output (json_schema) — never free-text parsing. Structured outputs
don't support numeric range constraints, so the score is clamped to [0, 1] after parsing. The
Anthropic client is injected so tests can mock it at the boundary — no live API calls in the suite.
"""

import json

from pydantic import BaseModel

DEFAULT_JUDGE_MODEL = "claude-opus-4-8"


class JudgeRequest(BaseModel):
    rubric: str
    input: str
    output: str
    expected: str | None = None
    model: str = DEFAULT_JUDGE_MODEL


class JudgeResponse(BaseModel):
    score: float  # normalized to [0, 1]
    passed: bool
    rationale: str


VERDICT_SCHEMA = {
    "type": "object",
    "properties": {
        "score": {"type": "number"},
        "passed": {"type": "boolean"},
        "rationale": {"type": "string"},
    },
    "required": ["score", "passed", "rationale"],
    "additionalProperties": False,
}


def build_judge_prompt(request: JudgeRequest) -> str:
    lines = [
        "You are an impartial evaluator scoring the output of an LLM/SLM prompt.",
        "Apply the RUBRIC to the OUTPUT and return a normalized score in [0, 1], a pass/fail, "
        "and a one- or two-sentence rationale.",
        "",
        "RUBRIC:",
        request.rubric,
        "",
        "PROMPT INPUT:",
        request.input,
        "",
        "MODEL OUTPUT:",
        request.output,
    ]
    if request.expected is not None:
        lines += ["", "EXPECTED / REFERENCE OUTPUT:", request.expected]
    return "\n".join(lines)


def stub_judge(request: JudgeRequest) -> JudgeResponse:
    """Deterministic, model-free judging for e2e (EVAL_RUNNER_STUB): passes any non-empty output."""
    passed = bool(request.output.strip())
    return JudgeResponse(
        score=1.0 if passed else 0.0,
        passed=passed,
        rationale="stubbed judge: non-empty output" if passed else "stubbed judge: empty output",
    )


def judge(client, request: JudgeRequest) -> JudgeResponse:
    response = client.messages.create(
        model=request.model,
        max_tokens=1024,
        output_config={"format": {"type": "json_schema", "schema": VERDICT_SCHEMA}},
        messages=[{"role": "user", "content": build_judge_prompt(request)}],
    )

    text = next(block.text for block in response.content if block.type == "text")
    data = json.loads(text)
    # Structured outputs can't enforce numeric ranges, so clamp defensively.
    data["score"] = max(0.0, min(1.0, float(data["score"])))
    return JudgeResponse.model_validate(data)
