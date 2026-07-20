"""LLM-judge scoring — given a rubric plus the fixture input, the model output, and an optional
expected output, ask the judge model to return a structured verdict (score, pass/fail, rationale).

The verdict uses structured output (json_schema) via the selected provider — never free-text
parsing. Structured outputs don't support numeric range constraints, so the score is clamped to
[0, 1] after parsing. The provider (Anthropic/OpenAI/…) is resolved from the request's judge
model and injected so tests can mock it at the boundary — no live API calls in the suite. Claude
stays the default judge (DEFAULT_JUDGE_MODEL); choosing another model/provider is a distinct
Scorer series (1.3).
"""

from pydantic import BaseModel

from app.providers import Provider, UsageBlock

DEFAULT_JUDGE_MODEL = "claude-opus-4-8"

# Output budget for the judge call. Sized to leave room for models whose thinking is ON by default
# (claude-sonnet-5, claude-fable-5): adaptive thinking spends output tokens before the structured
# verdict is emitted, so too small a budget truncates the verdict JSON mid-string (JSONDecodeError
# -> 500 — finding 5.1/B6). The verdict itself is tiny; this headroom is for the thinking. Kept
# model-agnostic on purpose: we don't send per-model `thinking`/`effort` flags, because some models
# 400 on `thinking:{type:"disabled"}` (Fable 5) or on `effort` (Haiku 4.5) — giving budget is safe
# for every provider and model.
JUDGE_MAX_TOKENS = 8192


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
    # Full usage block for the AI-usage ledger (6.1). None on the stub path (no live call).
    usage: UsageBlock | None = None


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


def judge(provider: Provider, request: JudgeRequest) -> JudgeResponse:
    result = provider.structured(
        model=request.model,
        prompt=build_judge_prompt(request),
        schema=VERDICT_SCHEMA,
        max_tokens=JUDGE_MAX_TOKENS,
    )
    data = result.data
    # Structured outputs can't enforce numeric ranges, so clamp defensively.
    data["score"] = max(0.0, min(1.0, float(data["score"])))
    verdict = JudgeResponse.model_validate(data)
    verdict.usage = result.usage
    return verdict
