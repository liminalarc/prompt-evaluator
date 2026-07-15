# Changelog

All notable changes to this project are documented here. Versions follow one unified product
SemVer (pre-1.0 `0.x`) across the API, web, and eval-runner. A release is a tagged, verified
**compose-stack build** — there is no hosted deployment yet (production deploy is spec 3.2).

## [0.7.0] — 2026-07-15

Sharpens the eval loop end-to-end: a cohesive branded UI, deletion/lifecycle for registry
entities, and clearer regression flagging when a dataset is too small to confirm a drop.

### Added

- **[#2.5] Eval-loop UI/UX overhaul** ([detail](specs/archive/2.5.md)) — a top-to-bottom pass
  driven by live dogfooding: self-hosted **Inter** (offline woff2, `--sb-font-ui` now resolves),
  a shared **Card** and layout kit (`.panel--wide`, `.card-grid`, `.form-stack`), brand tables
  everywhere, and intent variants on every button. A **navy hero topbar** (LiminalArc chrome) with
  a blaze litmus-drop logo, on-dark controls, and blaze active-nav accent; dashboard prompt cards
  gain a primary left rail. Eval-run detail now **pretty-prints** model output (fence-stripped) with
  labeled latency/cost and shows **input/output token counts** (the eval-runner already returned
  them; now threaded through `PromptExecution` → `FixtureRun`/`EvalRun` → DTO with an EF migration).
  A **dark-mode toggle** (`ThemeService`, persisted). Redesigned prompts/dataset/eval-run screens;
  capture form gains an optional expected-output field; Runs list refreshes after a run.
- **[#1.10] Deletion & lifecycle for registry entities** ([detail](specs/archive/1.10.md)) —
  `DELETE` for prompt / dataset / folder (org-scoped via `OrgAccess`: 403 non-member, 404 missing,
  204 ok). Prompt/dataset deletes cascade (datasets/versions/fixtures via FK; eval_runs/
  scorer_configs explicitly in a transaction); folder delete reparents its children to the parent
  (org root if top-level). Web: a shared tokenized `ConfirmService`/`ConfirmDialog` and delete
  affordances on the prompt row/workspace, dataset page, folder tile, and org header.
- **[#1.11] Unverified (small-sample) regression flagging** ([detail](specs/archive/1.11.md)) —
  a threshold-clearing drop that lacks statistical significance is no longer discarded but
  **classified**. New `RegressionConfidence { Confirmed, Unverified }`: `Confirmed` = drop>threshold
  AND p<alpha (unchanged); `Unverified` = drop>threshold but pValue null (n<2) or p≥alpha. Threaded
  through the handler and `RegressionFlagResponse`. Web renders unverified drops in a muted "Possible
  — not enough data to confirm (add more fixtures)" block; the "No regressions" empty state now shows
  only when there's no threshold-clearing drop at all.

### Fixed

- **[#1.10]** Org delete now also clears its orphan `eval_runs`/`scorer_configs`, completing a
  pre-existing 1.9 cascade gap.
- **[#2.5]** Switching org in the topbar didn't rescope `/prompts` (and analytics) — a stale-response
  race; responses whose `orgId` no longer matches the current org are now dropped.

### Changed

- Content links adopt LiminalArc navy (`--sb-primary`), replacing the browser-default blue/purple.

### Notes

- **New backlog from 2.5's deferral** (reconciled): **[#2.6]** upstreaming the local `--sb-hero-*`
  stopgap tokens into the shared brand-tokens package (cross-repo) — tracked, not scheduled.
- Deployable artifact is still the compose stack (local + CI only). CI gates: `backend`,
  `eval-runner`, `web`, `compose-smoke`. Hosted deployment remains spec 3.2.

## [0.6.0] — 2026-07-14

Adds authentication and multi-user access: users sign in and their access is scoped to the
organizations they belong to.

### Added

- **[#4.1] Authentication & Multi-User Access** ([detail](specs/archive/4.1.md)) —
  cookie-session auth over an **in-process Identity bounded context** (self-service registration,
  login/logout, and password forgot/reset behind an `IEmailSender` seam with enumeration-resistant
  responses). Credentials live in Infrastructure behind an `IUserDirectory` port (ASP.NET Core
  Identity over a separate `AppIdentityDbContext`), so `Domain` stays framework-free.
- **Per-organization authorization** enforced across every API data endpoint — the **organization
  is the permission boundary** (resolved from `Prompt.OrganizationId`): non-members get `403`,
  the org switcher lists only accessible orgs, and creating an org grants the creator ownership.
- **Angular auth**: `/login`, `/register`, `/forgot-password`, `/reset-password`, a route guard,
  and an HTTP interceptor (cookie credentials + 401→login); the shell shows the current user +
  logout only when authenticated.
- **eval-runner as an internal trusted service** — authenticated by a shared `X-Service-Token`
  (`EvalRunner__ServiceToken` ↔ `EVAL_RUNNER_SERVICE_TOKEN`), distinct from user credentials;
  enforced on its work endpoints, probes stay open.

### Notes

- **New backlog from 4.1's deferrals** (all reconciled): **[#4.2]** SSO / OAuth (new spec, not
  scheduled); **[#3.2]** gains the concrete hosted email provider plus two multi-instance auth-
  hardening items (immediate live-session invalidation on reset, Data-Protection key persistence).
- Deployable artifact is still the compose stack (local + CI only). CI gates: `backend`,
  `eval-runner`, `web`, `compose-smoke`. Hosted deployment remains spec 3.2.

## [0.5.0] — 2026-07-14

Makes the app coherent: one shell, one navigation model, a product dashboard, and the brand
system applied throughout — with no API or domain change.

### Added

- **[#2.4] UX Overhaul — App Shell, Navigation & Design-System Foundation**
  ([detail](specs/archive/2.4.md)) — a **persistent global organization context**
  (`OrgContextStore`, signals-based; persisted to localStorage **and** a `?org=` query param)
  that scopes prompts, datasets, analytics, and the dashboard from a topbar switcher; a redesigned
  topbar (`Dashboard · Prompts · Analytics`); a **landing dashboard** at `/` (built on a bounded
  facade over existing read APIs — org prompts + latest scores, recent runs, open regressions),
  retiring the echo skeleton from product nav (`/_skeleton`); a **shared UI kit** under
  `web/src/app/shared/` (PageHeader, Breadcrumb, Loading/Empty/Error, StatusBadge + origin/scorer/
  severity chips) applied to every page; eval runs made discoverable with a run linking back to its
  dataset **and** prompt.

### Changed

- Design-system pass: brand primitives (`.sb-badge`/`.sb-chip`/`.sb-card`, button variants,
  `.sb-field--error`) adopted app-wide, replacing raw emoji/plain-text status; **no hardcoded hex**
  (light + dark follow `data-theme` via `--sb-*` tokens by construction).
- IA reconciliation: the prompt workspace is the hub; `/datasets` demoted from the topbar to a
  deep-link browse; `/analytics` kept as an org-scoped cross-prompt destination. Long stacked pages
  grouped into setup / data / action with progressive disclosure.

### Notes

- **UI-only** — no prompts API/domain change and no new endpoints; dashboard aggregates are a
  client-side fan-out over existing read APIs (2.4's "no API change" deferral resolved to `built`).
- Deployable artifact is still the compose stack (local + CI only). CI gates: `backend`,
  `eval-runner`, `web`, `compose-smoke`. Hosted deployment remains spec 3.2.

## [0.4.0] — 2026-07-14

Organizes the registry: prompts group into folders under a top-level organization, with
everything about a prompt in one workspace.

### Added

- **[#1.7] Prompt Grouping (folders) + Unified Prompt Workspace** ([detail](specs/archive/1.7.md))
  — a `Folder` tree (top-level folders with optional subfolders); datasets now **belong to a
  prompt** (`Dataset.PromptId`, created under a prompt; a run rejects a dataset owned by another
  prompt); a **unified prompt workspace** showing a prompt's versions, its datasets, and its
  analytics on one page; folder API (CRUD, cycle-safe move, move-prompt, tree + by-folder listing).
- **[#1.9] Organizations (top-level container + permission boundary) + Prompts UX overhaul**
  ([detail](specs/archive/1.9.md)) — an `Organization` aggregate as the top of the hierarchy
  (`Organization › Folder tree › Prompt`) and the **permission boundary** spec 4.1 will grant
  access on (`Prompt.OrganizationId`, resolved directly); org-scoped API
  (list/create/rename/delete, org-nested folders + prompts; a seeded **Default** org with
  migration backfill); the Prompts screen reworked into an **organization switcher + main-area
  folder navigation** with collapsible create forms.

### Changed

- Datasets are created under a prompt (via the prompt's workspace); the global `/datasets` page is
  now a browse-only cross-prompt list.
- The permission boundary moved from the top-level folder (1.7) up to the **organization** (1.9);
  spec 4.1's detail was updated to consume `Prompt.OrganizationId`.

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
