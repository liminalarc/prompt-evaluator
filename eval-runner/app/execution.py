"""Prompt execution — run a prompt version's content against a fixture input on the prompt's
target (subject) model, capturing the output plus the latency and cost of producing it.

The model call is a plain text completion (no structured output): the prompt content is the
system prompt and the fixture input is the user turn. The Anthropic client is injected so tests
can mock it at the boundary — no live API calls in the suite.
"""

import time

from pydantic import BaseModel

DEFAULT_SUBJECT_MODEL = "claude-opus-4-8"

# Per-1M-token (input, output) USD pricing, sourced from the claude-api skill's model table
# (never from memory). Unknown models yield cost_usd = None rather than a wrong number.
_PRICING: dict[str, tuple[float, float]] = {
    "claude-fable-5": (10.0, 50.0),
    "claude-opus-4-8": (5.0, 25.0),
    "claude-opus-4-7": (5.0, 25.0),
    "claude-sonnet-5": (3.0, 15.0),
    "claude-haiku-4-5": (1.0, 5.0),
}


class ExecutePromptRequest(BaseModel):
    prompt: str  # the prompt version's content (used as the system prompt)
    model: str  # the target/subject model to run the prompt on
    input: str  # the fixture input (the user turn)
    upstream_context: str | None = None
    max_tokens: int = 4096


class ExecutePromptResponse(BaseModel):
    output: str
    latency_ms: int
    input_tokens: int
    output_tokens: int
    cost_usd: float | None = None


def build_user_message(request: ExecutePromptRequest) -> str:
    """The fixture input, prefixed with any upstream SLM context it was derived from."""
    if request.upstream_context:
        return f"{request.upstream_context}\n\n{request.input}"
    return request.input


def estimate_cost(model: str, input_tokens: int, output_tokens: int) -> float | None:
    rates = _PRICING.get(model)
    if rates is None:
        return None
    in_rate, out_rate = rates
    return round(input_tokens / 1_000_000 * in_rate + output_tokens / 1_000_000 * out_rate, 6)


def stub_execute(request: ExecutePromptRequest) -> ExecutePromptResponse:
    """Deterministic, model-free execution for e2e (EVAL_RUNNER_STUB). Echoes the input so the
    run flow can be exercised in CI without a live model call."""
    return ExecutePromptResponse(
        output=f"[executed:{request.model}] {request.input}",
        latency_ms=0,
        input_tokens=0,
        output_tokens=0,
        cost_usd=0.0,
    )


def execute_prompt(client, request: ExecutePromptRequest) -> ExecutePromptResponse:
    start = time.perf_counter()
    response = client.messages.create(
        model=request.model,
        max_tokens=request.max_tokens,
        system=request.prompt,
        messages=[{"role": "user", "content": build_user_message(request)}],
    )
    latency_ms = int((time.perf_counter() - start) * 1000)

    output = next((block.text for block in response.content if block.type == "text"), "")
    input_tokens = getattr(response.usage, "input_tokens", 0)
    output_tokens = getattr(response.usage, "output_tokens", 0)

    return ExecutePromptResponse(
        output=output,
        latency_ms=latency_ms,
        input_tokens=input_tokens,
        output_tokens=output_tokens,
        cost_usd=estimate_cost(request.model, input_tokens, output_tokens),
    )
