# Changelog

All notable changes to this project are documented here. Versions follow one unified product
SemVer (pre-1.0 `0.x`) across the API, web, and eval-runner. A release is a tagged, verified
**compose-stack build** — there is no hosted deployment yet (production deploy is spec 3.2).

## [0.3.0] — 2026-07-13

Ships the rest of the core evaluation loop: the eval harness, the rename to LitmusAI, and score
tracking & analytics. All three version strings bump together to `0.3.0`.

### Added

- **[#1.3] Eval Harness** ([detail](specs/archive/1.3.md)) — the `EvalRun` aggregate (a prompt
  version scored over a dataset, append-only) owning per-fixture `ModelOutput` + latency/cost and one
  `Score` per scorer. One `IScorer` abstraction with deterministic (regex / JSON-schema / exact /
  fuzzy / latency / cost) and LLM-judge (structured verdict via the eval-runner) implementations,
  composed per dataset via a persisted `ScorerConfig`. The judge model is part of the scorer's
  identity (`Prompt × Version × Dataset × Scorer`). API to configure scorers, run, and fetch results;
  Angular scorer-config / run-trigger / per-fixture results view. Echo round-trip repointed to
  `POST /api/echo`.
- **[#1.4] Score Tracking & Analytics** ([detail](specs/archive/1.4.md)) — read-only analytics over
  the append-only run history: trend series per scorer across versions (latest run per version),
  regression detection (configurable threshold **and** a paired-t-test significance gate so noisy
  series don't false-flag), and version-vs-version comparison (per-fixture + aggregate deltas).
  `GET /api/analytics/{trends,regressions,comparison}`; Angular `/analytics` dashboard with a
  brand-token-themed ngx-charts trend chart, a regression list, and a comparison view.

### Changed

- **[#0.2] Rename → LitmusAI** ([detail](specs/archive/0.2.md)) — the product, docs, specs, web UI,
  eval-runner branding, compose project, and service/network ids renamed from Prompt Evaluator to
  LitmusAI.

### Fixed

- **[#1.4]** Multi-fixture run persistence — a latent 1.3 bug where one `ScorerDescriptor` instance
  was shared across a run's `Score`s (EF owned types can't share an owner), which crashed any run
  over a 2+ fixture dataset. Each `Score` now owns its scorer descriptor.

### Notes

- Deployable artifact is still the compose stack (local + CI only). CI gates: `backend`,
  `eval-runner`, `web`, `compose-smoke`. Hosted deployment remains spec 3.2.

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
