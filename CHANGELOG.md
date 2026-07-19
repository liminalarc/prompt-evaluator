# Changelog

All notable changes to this project are documented here. Versions follow one unified product
SemVer (pre-1.0 `0.x`) across the API, web, and eval-runner. A release is a tagged, verified build;
as of `0.13.0` it also deploys to a hosted **dev** environment on every push to `main` (spec 3.2).
There is no prod target yet.

## [0.15.0] — 2026-07-18

Model-catalog fidelity for onboarding, plus the groundwork surfaced while dogfooding real prompts (5.1).

### Added

- **[#1.19] Model catalog — current Anthropic models** ([detail](specs/archive/1.19.md)): seed **Claude
  Sonnet 4.6**, **Opus 4.7**, and **Opus 4.6** (Anthropic, all roles, priced) so an eval can baseline on
  the model an app actually runs — Cortex Golf runs several prompts on Sonnet 4.6, which wasn't selectable.
  Data-only EF migration (leaves the 1.13 seed untouched); Fable 5 held (thinking-always-on / retention / cost).

### Dogfooding (5.1 — ongoing)

- **[#5.1] Adopt LitmusAI across Cortex Golf & Stormboard** ([detail](specs/5.1/5.1.md)): daily-briefing
  improvement **backported to Cortex Golf** (eval v1 0.55 → v2 0.88); run book refreshed to the live 2.8 UI;
  findings **F1/F2/F3** promoted to specs **[#1.16]** (version status & backport lifecycle),
  **[#1.17]** (multimodal / image fixtures), **[#1.18]** (tool-augmented eval); DoD re-scoped to *walkable*
  prompts, with 9 blocked Golf prompts re-homed to 1.17/1.18; round-debrief prepped for the next walk.

## [0.14.0] — 2026-07-18

Round 2 of the eval-loop UX, driven by the 5.1 dogfood findings: the loop now fails **loudly**, stays
**org-scoped**, and offers a **consistent add/edit-with-metadata** surface across versions, datasets,
fixtures, and scorers.

### Added

- **[#2.8] Eval-loop UX round 2** ([detail](specs/archive/2.8.md)):
  - **Loud failures** — a failed run surfaces the eval-runner's reason (e.g. `eval-runner: Anthropic
    not configured`) as a `502 {error}` banner instead of a bare 500; run/scorer errors show the
    server message.
  - **Run scoping** — the dataset run form is fixed to the dataset's owning prompt (pick a version
    only), removing the cross-org prompt leak; runs can also be triggered from the prompt workspace.
  - **Editable metadata, inline** — version **label**, fixture **label/description**, and scorers
    (reconfigure / remove) edit via expand-to-edit rows; content + target model (versions) and
    input/origin/seed (fixtures) stay immutable. Adds `Fixture.Label`/`Description` (migration).
  - **Progressive disclosure** — version history, fixtures, scorers, and eval-run results collapse to
    summary rows that expand to detail; the runs table shows version · model · scorers and the compare
    table labels fixtures by scenario (not GUID); type-prefixed headers (`Prompt:` / `Dataset:`).
  - **Fixtures & forms** — manual entry can be marked **Synthetic**; create-dataset gains a
    Description field; new-version seeds from the latest; create-prompt lands on the new workspace.

### Fixed

- **[#2.8]** LLM-judge no longer 500s on thinking-on-by-default judge models (Sonnet 5 / Fable 5) —
  the judge's output budget is sized so the verdict JSON isn't truncated (B6).
- **[#2.8]** Fixture redactor no longer scrubs ISO dates as phone numbers — `2026-07-12` survives
  ingest intact; the phone matcher now requires ≥10 digits (B7).

### Notes

- Also landed on `main` since 0.13.0, **docs only (no app code)**: backlog spec drafts **[#2.7]**
  (AI Prompt Authoring Assistant, [detail](specs/2.7.md)) and **[#2.9]** (Weighted composite scoring,
  [detail](specs/2.9.md)), and **[#5.1]** dogfooding logs (prompt inventory, run book, T2 shakeout —
  [detail](specs/5.1/5.1.md)). Those specs remain open.

## [0.13.0] — 2026-07-18

Ships the first hosted deployment: LitmusAI now runs on an AWS **dev** environment, deployed
automatically on every push to `main`. Adds admin-created user accounts and polishes the topbar.

### Added

- **[#3.2] Production Deployment** ([detail](specs/archive/3.2.md)) — a hosted **dev** environment on
  AWS App Runner + ECR + RDS, Terraform-managed (`infra/`, modeled on Prism; reuses the shared
  account's GitHub OIDC provider + `stormboard-dev` VPC connector, owns its own RDS Postgres). The API
  now serves the Angular SPA from one combined image (single origin, no nginx); the eval-runner runs
  as a second, token-protected service. CI's `deploy-dev` job builds + pushes both images and rolls
  the App Runner services on every push to `main`, with a post-deploy smoke. Auth hardened for a
  multi-replica deploy: a password reset invalidates live sessions (SignInManager +
  SecurityStampValidator) and Data-Protection keys persist to Postgres (cookie valid across replicas).
- **[#4.6] Admin-created users** ([detail](specs/archive/4.6.md)) — admins can create user accounts
  directly from the **Users** page (email + display name + password, no email required) via
  `POST /api/admin/users`; the new user then gets org/role granted with the existing per-user controls.

### Fixed

- Topbar **Manage** and user-name are now legible control chips in both light and dark themes (were
  dimmed white on the dark bar), and the **Admin** menu now closes on outside-click / Escape /
  item-select instead of sticking open.

### Notes

- **Dev-only** — there is no prod deploy target yet (future work under 3.2). Git tags are the version
  marker; the dev environment deploys continuously from `main`.
- **Invite-by-email onboarding** and SSO remain out of scope → spec 4.2 (which also owns the
  transactional-email provider).

## [0.12.0] — 2026-07-17

Rounds out organization management: a global-admin surface to manage the orgs themselves, and an
owner-facing surface so an org's own Owner can manage its membership — without the workspace-admin
flag.

### Added

- **[#4.4] Organization management (admin)** ([detail](specs/archive/4.4.md)) — an admin
  **Organizations** page (`/admin/organizations`, global-admin gated) that lists every org with
  member counts and supports create / rename / delete (delete cascades folders/prompts/datasets/runs
  behind a type-the-org-name-to-confirm dialog), plus a drill-in to manage any org's members. Backed
  by admin-gated `/api/admin/organizations` endpoints. The global-admin flag gates **management
  only** — org *content* stays membership-gated.
- **[#4.5] Org-owner member management** ([detail](specs/archive/4.5.md)) — an owner-facing org
  detail page (`/organizations/:id`, reached via a topbar **Manage** link) where an org's **Owner**
  (or a global admin) lists members, adds by email, sets roles, and removes them — an
  **owner-or-admin, per-org** gate on the member-scoped `/api/organizations/{id}/members` endpoints,
  distinct from 4.4's global-admin-only surface. A **last-owner guard** keeps every org with at least
  one owner. The switcher payload now carries the caller's per-org role for UI-gating.

### Notes

- Members are added **by email** (users self-register — an owner can't enumerate the admin-gated
  user directory). Inviting non-existent users / email delivery and SSO remain out of scope (4.2).
- Deployable artifact is still the compose stack (local + CI only). Hosted deployment remains
  spec 3.2 — a release is a tagged, verified build, not a deploy.

## [0.11.0] — 2026-07-16

Adds a managed **Model Catalog** that drives the target/judge model droplists (no more free-text
ids), and an **admin surface for user & access management** — introducing the app's first
role-based gate: a workspace-level global-admin flag.

### Added

- **[#1.13] Model Catalog + admin management** ([detail](specs/archive/1.13.md)) — a workspace-wide
  Model Catalog (Postgres/EF, seeded with the supported Claude + GPT models; provider, roles, and
  display-only pricing) served by `GET /api/models`. The **target-model** (prompt-detail) and
  **judge-model** (dataset-detail) inputs become catalog-fed droplists filtered by role; the
  eval-runner's `GET /providers` drives per-model **availability** (unavailable models are marked,
  not offered). A global-admin-gated page (`/admin/models`) adds/edits/deactivates entries. Legacy
  free-text target models still display and run.
- **[#4.3] Admin user & access management** ([detail](specs/archive/4.3.md)) — an **Admin** nav
  folder (Users + Models) gated by a new workspace-level **global-admin flag** (`AppUser.IsAdmin`,
  the app's first role-based gate). `/admin/users` lists users and manages their admin flag, org
  membership + role, and passwords; the **last admin cannot be demoted**. Any signed-in user can
  change their own password from `/account`. No email; account creation stays self-service.

### Notes

- Org-entity management (list-all / create / rename / delete orgs) split to spec **4.4**; live model
  discovery and per-org catalogs re-homed to **1.14** / **1.15** (all `NOT STARTED`).
- Deployable artifact is still the compose stack (local + CI only). Hosted deployment remains
  spec 3.2 — a release is a tagged, verified build, not a deploy.

## [0.10.0] — 2026-07-16

Makes the eval-runner multi-provider: the judge, synthetic-data generation, and subject execution
all route by model id, so evaluations can run against OpenAI models alongside the Claude default —
with the Domain/Application layers staying provider-agnostic.

### Added

- **[#1.5] Multi-Provider Model Support** ([detail](specs/archive/1.5.md)) — a **provider-routing
  abstraction** in `eval-runner/app/providers.py` (Anthropic default + OpenAI), selected by model id.
  The **judge and synthetic-data generation** route through the provider registry; **subject
  execution** runs across providers, with a captured-output path for prompts fed by an upstream
  model. **Per-provider credentials** are wired via environment (never committed), covered by a
  config test. `IEvaluationRunner` and the Domain/Application layers remain provider-agnostic.
- **[#1.6]** ([detail](specs/archive/1.6.md)) — `samples/prompts.json` (+ `samples/README.md`) as a
  ready-to-use fixture for the bulk prompt importer shipped in 0.9.0.

### Notes

- **[#1.13]** ([detail](specs/1.13.md)) — Model Catalog + admin management (droplists, no free-text
  model ids) authored as a spec this cycle; backlog only, not yet implemented (`NOT STARTED`).
- Multi-provider re-homed the Modal SLM adapter to its own spec **1.12** (`NOT STARTED`); the 1.5
  archive names the split-out.
- Deployable artifact is still the compose stack (local + CI only). Hosted deployment remains
  spec 3.2.

## [0.9.0] — 2026-07-15

Lets prompt owners load prompt content from files instead of hand-pasting — a single file into
the add-version form, or many prompts at once from a JSON file.

### Added

- **[#1.6] Prompt Import (file / bulk)** ([detail](specs/archive/1.6.md)) — completes 1.1's split-out
  "import from a file" deferral. **Single-file import**: a file picker in the add-version form reads a
  text file (`FileReader`) into the existing content signal and copies it in through the unchanged 1.1
  POST; a pure `validateImportFile` helper rejects empty / oversized (>1 MB) / non-text files with a
  clear message. **Bulk import**: an "Import prompts" action on `/prompts` reads a JSON array of prompts
  (each with an optional description + `versions[]`) and orchestrates the import **client-side** by
  looping the existing create/add-version POSTs into the org + folder in view — sequential, with a
  per-row success/error report; a failing row never stops later prompts.

### Notes

- **No API or domain change** — bulk import is client orchestration of the existing 1.1 POSTs; a
  server-side batch endpoint was considered and rejected (*No premature abstractions*). No `Prompt`
  aggregate change. Web-only diff; backend and eval-runner are unchanged since 0.8.0.
- No deferrals — both single-file and bulk were in scope and built.
- Deployable artifact is still the compose stack (local + CI only). CI gates: `backend`,
  `eval-runner`, `web`, `compose-smoke`. Hosted deployment remains spec 3.2.

## [0.8.0] — 2026-07-15

Surfaces the running build in the app: a version badge + build chip in the UI, and an
environment-channel signal that stays honest across upper environments.

### Added

- **[#3.3] Version display in the web UI + deploy-channel plumbing** ([detail](specs/archive/3.3.md))
  — a flat, SPA-facing `GET /api/version` (`{version, commit, buildTime, environment, channel}`),
  distinct from the aggregated `GET /version`. The web surfaces it via a root `VersionService` signal
  loaded by an app initializer: a **footer build chip** (channel-keyed — `v<ver> · <sha>` in prod,
  `dev · <sha>` in dev, `local` locally; full detail on hover) and a topbar **environment badge**
  (`DEV`/`STAGING`/`LOCAL`, none in prod). A failed/absent fetch renders nothing.
- **Deploy channel (`DEPLOY_CHANNEL`) as the reliable dev/prod discriminator** — `ASPNETCORE_-
  ENVIRONMENT` isn't trusted (a host can report `Production` everywhere), so the UI keys off
  `channel`: CI derives `APP_CHANNEL` from the git-ref (`v*` tag → `prod`, else `dev`) → Docker
  `ENV DEPLOY_CHANNEL` → the payload; a plain local build is `local`. `channel` is an open string, so
  staging/prod deploy targets drop in with spec 3.2. `compose-smoke` now asserts `/api/version`
  carries the channel through the web proxy (shape, never a literal).

### Notes

- Follows the Stormboard pattern (channel over environment; footer chip + env badge). Mirrored here
  by user decision; the flat endpoint matches Stormboard exactly while the aggregated `/version` is
  left unchanged.
- Deployable artifact is still the compose stack (local + CI only). CI gates: `backend`,
  `eval-runner`, `web`, `compose-smoke`. Hosted deployment remains spec 3.2.

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
