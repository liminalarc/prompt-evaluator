# Changelog

All notable changes to this project are documented here. Versions follow one unified product
SemVer (pre-1.0 `0.x`) across the API, web, and eval-runner. A release is a tagged, verified
**compose-stack build** — there is no hosted deployment yet (production deploy is spec 3.2).

## [0.2.0] — 2026-07-12

First aligned release: the API, web, and eval-runner version strings are unified at `0.2.0`
(previously drifting at `0.1.0` / `0.0.0` / `0.1.0`).

### Added

- **[#0.1] Walking Skeleton** ([detail](specs/archive/0.1.md)) — runnable compose stack (Angular
  SPA + .NET DDD API + Python FastAPI eval-runner + PostgreSQL) with an end-to-end round-trip and
  `GET /health` + aggregated `GET /version` across services.
- **[#1.1] Prompt Registry** ([detail](specs/archive/1.1.md)) — `Prompt` aggregate with an
  append-only `PromptVersion` history (content immutable per version) and a per-version target
  model; `IPromptRepository` (the Zatomic seam) on EF Core / Postgres; `/api/prompts` create /
  add-version / browse; Angular routed prompt list, version history, and version diff.
- **[#1.2] Datasets & Fixtures** ([detail](specs/archive/1.2.md)) — `Dataset` aggregate owning
  `Fixture`s tagged `captured` | `synthetic`; documented capture-ingestion schema + endpoint that
  lands app-emitted tuples as fixtures, with PII redaction at ingest; eval-runner guided synthetic
  generation (seeded from captured examples, operator guidance, structured output) wired over HTTP
  and persisted linked to seeds; Angular dataset browse with origin filter, capture, and generate.

### Notes

- Deployable artifact is the compose stack (local + CI only). CI gates: `backend`,
  `eval-runner`, `web`, `compose-smoke`.
