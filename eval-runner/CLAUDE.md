# eval-runner/ — Python evaluation service

Additive to root `CLAUDE.md`. Only layer-specific rules here.

## Purpose

A small FastAPI service the .NET `Infrastructure` layer calls over HTTP. It owns the two
jobs that are cheapest in Python:

1. **LLM-judge scoring** — given a prompt output + a rubric, return a structured score.
2. **Synthetic fixture generation** — given captured example fixtures, generate more
   SLM-*shaped* inputs to fill coverage gaps (seeded from the examples so the distribution
   matches; never invents a distribution from scratch).

This service is a stateless execution detail. It holds no domain authority and no
persistence — .NET owns the data and decides what to store.

## Conventions

- FastAPI + Pydantic models for every request/response. The Pydantic schema **is** the
  contract with .NET; keep it in sync with the DTOs on the Infrastructure client.
- **Multi-provider behind `app/providers.py`.** A `Provider` protocol + a model-id → provider
  registry route each request (subject execution, judge, generation) to the right vendor
  adapter (Anthropic default, OpenAI; a Modal SLM adapter is spec 1.12). Each adapter exposes
  `complete()` (plain completion) and `structured()` (native json_schema output — never
  free-text parsing). Vendor clients are injected, so tests mock at the boundary. Add a
  provider = a new adapter + a prefix rule in `resolve_provider`; don't leak provider choice
  into the endpoints beyond routing.
- Default to the latest capable models (Opus 4.8 / Sonnet 5) for the default (Claude)
  provider. Consult the `claude-api` skill for Claude model ids and usage; do not answer model
  questions from memory.
- **pytest** for tests. Mock the Anthropic client at the boundary — no live API calls in
  the test suite. A thin set of contract tests asserts the request/response shapes match
  what .NET sends.
- Config via environment variables. **Per-provider credentials**: `ANTHROPIC_API_KEY` (default),
  `OPENAI_API_KEY` (optional); a provider is registered only when its key is present, so a
  request for an unconfigured provider fails clearly (400). Also `EVAL_RUNNER_MODEL`. No
  secrets in code.
- **Stub mode** (`EVAL_RUNNER_STUB`) makes generation model-free and deterministic for e2e — the
  Anthropic client is never constructed. Test-only; enabled via `docker-compose.e2e.yml`.
- Keep endpoints thin: validate → call model → shape response. Business decisions
  (which scorer, which dataset) are made on the .NET side and passed in.
