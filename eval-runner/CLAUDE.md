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
- Anthropic SDK for model calls. Default to the latest capable models (Opus 4.8 /
  Sonnet 5). For judge calls that must return structured verdicts, use tool-use /
  structured output — never parse free text. Consult the `claude-api` skill for model ids
  and usage; do not answer model questions from memory.
- **pytest** for tests. Mock the Anthropic client at the boundary — no live API calls in
  the test suite. A thin set of contract tests asserts the request/response shapes match
  what .NET sends.
- Config via environment variables (`ANTHROPIC_API_KEY`, model overrides). No secrets in code.
- Keep endpoints thin: validate → call model → shape response. Business decisions
  (which scorer, which dataset) are made on the .NET side and passed in.
