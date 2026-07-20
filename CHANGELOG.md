# Changelog

All notable changes to this project are documented here. Versions follow one unified product
SemVer (pre-1.0 `0.x`) across the API, web, and eval-runner. A release is a tagged, verified build;
as of `0.13.0` it also deploys to a hosted **dev** environment on every push to `main` (spec 3.2).
There is no prod target yet.

## [0.22.0] — 2026-07-20

**Backport assistance.** Once LitmusAI flags the single backport target (1.16 / 2.9), the prompt
workspace can now **generate the artifact to ship it**. A **Prepare backport** action on the
Deployment summary assembles, for that target: the exact new prompt (copy-to-clipboard) or a
downloadable markdown carrying the full new content, a **diff vs the version live in source**, a
per-scorer **score-delta summary** (target vs Current), and an apply checklist. LitmusAI still
**signals only** — it produces the artifact; the human applies it in the source repo's own process
(wired-in PR / registry automation remains 3.1).

### Added

- **[#1.20] Backport assistance** ([detail](specs/archive/1.20.md)):
  - New **`GET /api/prompts/{id}/backport-artifact`** (org-gated) → `404` when the prompt has no
    backport target, else the assembled artifact: the target version's exact content, a line **diff
    vs Current's content**, the per-`(dataset × scorer)` **score deltas** (target − Current), a
    rendered markdown, and a suggested filename.
  - **`BackportArtifactHandler`** reuses the 1.16 `VersionStatusHandler` (resolve the single target +
    Current) and the analytics comparison (per-scorer deltas over every dataset the two share); a new
    pure **`LineDiff`** (LCS line diff, mirroring the web diff) computes the diff vs Current.
  - **Web:** a **Prepare backport** button on the Deployment summary — shown **only when a target
    exists** — opens a drawer with a markdown preview, **Copy exact prompt** (target content →
    clipboard), and **Download markdown** (`.md` blob).

### Docs

- **[#5.1]** Runbook **Step 8–10 rewritten for tool-native backporting** — the 1.16 *Current in
  source* marker, the 2.9 weighted-composite target ranking, and the 1.20 artifact supersede the
  hand-copy process; `daily-briefing` and `round-debrief` flagged for official **in-tool
  re-formalization** (Set Current → Prepare backport → Mark backported).

## [0.21.0] — 2026-07-20

**Weighted composite scoring.** A dataset's scorers now carry **weights** (default equal), and every
run gets one weight-blended **composite** — a single "overall quality" number instead of eyeballing
each scorer's series. The composite shows as its own trend line and a **version-over-version change
table** (per-scorer means + composite, with deltas), extending the two-version compare to N versions.
Crucially, the **backport signal** (1.16) is now keyed off the **normalized weighted composite vs
Current** for both eligibility and the single recommended target — so a high-signal scorer (LLM judge)
outweighs a low-signal one (RegEx), and a prompt whose rubric changed mid-history no longer
mis-recommends an old version that scored high on the old yardstick.

### Added

- **[#2.9] Weighted composite scoring** ([detail](specs/archive/2.9.md)):
  - **Per-dataset-scorer weights** on `ScorerConfig` (default `1.0` = equal; must be finite and
    positive; editable in the dataset scorer UI). Weight is deliberately **excluded from a scorer's
    identity**, so re-weighting never forks its score series. New `AddScorerWeight` migration.
  - **Weighted composite** per run/version — the weighted mean of each scorer's `[0,1]` value,
    renormalized over the scorers actually present (so adding/removing a scorer between versions is
    handled correctly). Surfaced as a new composite **trend series** (`GET /api/analytics/composite`)
    and a **version-over-version change table** on the Analytics dashboard.
  - **Backport signal rewired (1.16 → weighted).** Eligibility and the single backport target now
    derive from the normalized weighted composite vs Current
    (`Σ_shared w·(cand−cur) / Σ_Current w`), replacing the interim unweighted rule, with the
    same-scorer-config no-regression floor retained as a safety guard. Resolves the round-debrief
    scorer-config-change confound (covered by a regression test).

### Deferred

- Must-pass/veto gating and weight auto-tuning (from 2.9's Out) → new **[#2.9a]**
  ([detail](specs/2.9a.md)).

## [0.20.0] — 2026-07-20

An admin **AI Usage & Budget Tracker**: every model call the harness makes — subject runs, LLM-judge
scoring, synthetic generation — is now ledgered (tokens, model, cost, who/what/when/status) and a
workspace admin can slice that spend by date/model/feature/user/org, see it over time, drill into
individual calls, export CSV, and track it against budgets with over-threshold alerts. The Model
Catalog's displayed price now derives from the same authoritative pricing table the ledger charges
against, so catalog and ledger agree on one number.

### Added

- **[#6.1] AI Usage & Budget Tracker (admin)** ([detail](specs/archive/6.1/6.1.md)):
  - **Capture at the eval-runner seam** — an `AiUsageRecord` on every `EvalRunnerClient` call (subject
    execution, LLM-judge, synthetic generation) on **success and failure**, attributed to org + user
    via an ambient `AiUsageContext`. The eval-runner's judge/generate responses now surface the full
    usage block (model, input/output + cache tokens, request id, status). **Metadata + token counts
    only — never prompt/response content or secrets.**
  - **Immutable cost** — a versioned, config-backed per-model pricing table (input/output/cache-write/
    cache-read + rate version); cost is computed and **snapshotted** on each record, so a later price
    change never rewrites history. Unknown model → cost null + flagged, tokens still stored.
  - **Query API** (`/api/admin/ai-usage/*`, global-admin gated) — filter by any combination of date
    range / model / feature / user / org / status; aggregate by period / model / feature / user / org;
    per-slice metrics (total cost, token totals, call count, avg per call, success rate, latency
    p50/p95); time-series, breakdowns + top-N, a paginated calls drill-down, and CSV export.
  - **Admin → AI Usage** SPA area (`/admin/ai-usage`) — a filter bar that drives the whole surface,
    spend summary tiles, a spend-over-time chart, breakdown tables, the calls table, CSV export, and
    the budget surface. Gated by `[authGuard, adminGuard]` with an `isAdmin` Admin-menu link.
  - **Budgets** — a global (workspace) budget + optional scoped budgets (per model / feature / org),
    monthly, with spend-vs-budget tracking and Ok/Warning/Over threshold alerts (tracking + alerting
    only; no run-path enforcement).
- **[#6.2] Model Catalog price ← authoritative ledger table** ([detail](specs/archive/6.2.md)) — the
  catalog's **displayed** price is now `per-model override ?? authoritative pricing-table rate`, sourced
  from the same 6.1 table the ledger uses. The entry's price columns are kept as an optional override
  (author intent wins); a source badge shows override vs. table. Non-destructive — no migration.

### Fixed

Post-merge code-review + security-review hardening of the ledger (all part of [#6.1]):

- **CSV formula injection** — the export now neutralizes cells beginning with a spreadsheet formula
  trigger (`= + - @` / tab / CR), closing an org-member → admin-machine escalation via the exported file.
- **OpenAI cached tokens were double-charged** — input now excludes the cached subset (billed once at
  the cache-read rate), matching Anthropic's token semantics.
- **Ledger stored the provider-echoed (dated) model id** — nulling OpenAI cost snapshots and making
  model-scoped budgets match $0; it now records the requested catalog id the pricing table and budgets
  are keyed on.
- **Date-range `to` dropped the final day** — a date-only upper bound is now inclusive to end-of-day.
- **Anthropic structured refusal** raised an opaque `StopIteration` (500); it now surfaces cleanly.
- **Scoped budgets** with a mistyped feature/org value silently tracked the whole ledger — now rejected
  at creation.

### Backlog

- **[#6.3] AI-usage ledger accuracy & scale follow-ups** ([detail](specs/6.3.md)) — two residuals
  re-homed from the 6.1/6.2 review (phantom usage-less rows; in-memory aggregation at scale). Not yet
  implemented.

## [0.19.1] — 2026-07-19

A dogfooding fix to the [#1.16] backport lifecycle, plus two specs added to the backlog.

### Fixed

- **[#1.16] Single backport target** ([detail](specs/archive/1.16.md)) — when several versions score
  higher than Current, LitmusAI now recommends exactly **one** — the highest-scoring eligible version
  (interim unweighted rank; weighted ranking still tracked in [#2.9]). Only that version carries the
  **"Backport target"** badge and the Deployment summary / Mark-backported point at it (previously every
  better-than-Current version was badged, which pointed the action at the earliest, not the best).
- **[#1.16]** version-history table — fixed a layout glitch where the status cell's `display:flex` broke
  table-cell rendering on rows without a badge.
- **[#2.21]** e2e — create the first org via the org rail (not the removed zero-org header button), fixing
  the `compose-smoke` gate after the no-org onboarding change.

### Added

- **[#1.20] Backport assistance** ([detail](specs/1.20.md)) — spec for generating the artifact to ship
  the backport target (a copy-paste prompt or a downloadable markdown with the diff + score deltas);
  wired-in automation deferred to [#3.1]. Backlog only — not yet implemented.
- **[#6.1] AI Usage & Budget Tracker** ([detail](specs/6.1.md)) — spec for an admin AI usage/cost/budget
  tracker, captured at the eval-runner seam. Backlog only — not yet implemented.

## [0.19.0] — 2026-07-19

Organization lifecycle plus a prompt **backport lifecycle**: a user in no org now gets real
onboarding (create or **request to join**), the placeholder "Default" org is fully deletable, and
every prompt version can be flagged as running-in-source with a **backport-eligible** signal when a
better version exists.

### Added

- **[#2.21] Org lifecycle — deletable Default org, no-org onboarding, request-to-join**
  ([detail](specs/archive/2.21.md)):
  - **No-org onboarding** — a user in zero organizations sees a real first-run surface (Dashboard +
    Prompts) offering two paths: **create an organization** or **request to join** one. Sign-up no
    longer auto-assigns a placeholder org.
  - **Request-to-join access** — the pull counterpart to 4.5's add-by-email push: request access to
    an org from a workspace directory; the org's **Owner** (or an admin) reviews a **Requests** tab
    and approves (granting membership at a role) or denies. Domain rules hold — no duplicate open
    request, can't request an org you're in, idempotent approve.
  - **Deletable "Default" org** — the seeded Default org deletes like any other; deleting an org now
    revokes its memberships (no orphaned rows) and the bootstrap seeder no longer resurrects it.
- **[#1.16] Version status & backport lifecycle** ([detail](specs/archive/1.16.md)):
  - **"Current in source" marker** — mark which version your source app runs (optional commit SHA);
    one per prompt, nullable until set.
  - **Backport-eligible signal** — derived when a version scores higher than Current on a shared
    dataset (same-scorer-config comparison, never blended); **mark-as-backported** moves the marker
    and the flag re-derives. Weighted eligibility is future work ([#2.9]).
  - **Version-status badges** — `Current` · `Backport-eligible` · `Regressed` on each version row,
    plus a compact **Deployment summary** in the prompt workspace. LitmusAI signals only — it never
    writes to a source repo.

### Changed

- **Marketing / positioning pass** — the README leads with value and audience, a new `MARKETING.md`
  is the positioning source of truth, and the login / register / onboarding copy leads with the
  outcome (score prompts, catch regressions) rather than restating the page heading.

## [0.18.0] — 2026-07-19

Deferred UX polish from the cohesion pass ([#2.20]), led by a new **persistent left organization
rail** that replaces the topbar org dropdown, plus a tabbed org page, roomier source editors, and a
couple of workspace fixes.

### Added

- **[#2.20] Deferred UX polish** ([detail](specs/archive/2.20.md)):
  - **Organization rail** — a persistent left rail lists every org you can access (one active at a
    time), replacing the cramped topbar `<select>`. **Collapsible** to a slim strip of initials
    (persists), with **inline create-org** and a **settings gear** on the active org.
  - **Organization detail is a tabbed page** — `/organizations/:id` is now **Overview · Members**
    (reached via the rail's gear); the page follows org-switches instead of getting stuck.
  - **Roomy monospace source editors** — the version **Content** and **rubric/config** editors are
    monospace, tall, and drag-resizable (exact source fidelity for prompts).
  - **Roomier eval-run output** — the model output (the primary artifact) gets more height and is
    resizable.
  - **Un-run versions named on the trend** — versions that have never run are listed beneath the
    score trend, so the gap is explicit.

### Fixed

- **[#2.19]** ([detail](specs/archive/2.19.md)): the prompt-workspace **Runs tab** now lists every run
  across the prompt's datasets (was empty until you opened the run form), and the active **tab persists
  in the URL** so Back / breadcrumb from a dataset returns to the tab you left from.

### Deferred

- **[#2.21]** ([detail](specs/2.21.md)): make the auto-assigned "Default" org deletable and give a user
  with **no organizations** a real create-your-first-org onboarding instead of a placeholder.

## [0.17.0] — 2026-07-19

A **UI/UX cohesion pass** that makes the whole app read as one connected product, plus
**score-stability & rationale-first comparison**. The prompt workspace becomes a tabbed hub, a
shared right-side Drawer homes the heavy surfaces, Compare is unified (content · scores · rationale
off one From→To), headline scores stop being inflated by pass/fail scorers, and "fixtures" are
renamed to **test cases**.

### Added

- **[#2.19] UI/UX cohesion pass** ([detail](specs/archive/2.19.md)):
  - **Tabbed workspace hub** — `/prompts/:id` is now Versions · Datasets · Analytics · Runs, with
    **Run** elevated to a header primary action and **Compare** opening a drawer; Datasets/Analytics
    each get a canonical tab instead of stacked partial-duplicates.
  - **Shared `Drawer` primitive** — one right-side slide-over (Esc/scrim close, focus-trap, responsive)
    homes scorer-edit, user↔org management, and Compare.
  - **Unified Compare drawer** — pick From→To once, then tab **Content** (text diff) · **Scores**
    (per-scorer deltas) · **Rationale** (judge "why"), with cross-model + within-noise warnings.
  - **Meaningful headline scores** — dashboard cards, run headers, and Runs/Recent-activity tables show
    the LLM-judge mean (not a 1.00 inflated by always-pass deterministic scorers).
  - **Summarize-then-reveal (D3)** — version content caps height, the content diff collapses unchanged
    runs (GitHub-style), the rubric CONFIG cell shows a one-line summary.
  - **Variance clarity** — Score-stability focuses on stochastic scorers; deterministic/stale scorer
    identities fold away and are labelled by config.
  - **Fixtures → "Test cases"** relabelled app-wide (domain aggregate unchanged); the dataset model
    (test cases × scorers) is stated on the page.
  - **Attention-centered dashboard** — Needs attention → Prompts → Recent activity (with scores).
  - **Consistency** — human date format + chip-lists everywhere; simplified topbar nav (Dashboard ·
    Prompts · Admin); brand orange-droplet favicon. Absorbs **[#2.18]** (run-failure visibility, run
    labels, dark dropdown).
- **[#2.14] Score stability & rationale-first comparison** ([detail](specs/archive/2.14.md)): a
  variance view (mean ± spread over repeated runs) with a within-noise flag, and a rationale-diff that
  shows the judge's reasoning on each side of a comparison.

### Changed

- **[#2.12] Eval-loop round 3** ([detail](specs/archive/2.12.md)) closed out (reliability quick-fixes;
  heavy slices promoted to 2.14–2.18).

### Deferred

- **[#2.20] Deferred UX polish** ([detail](specs/2.20.md)) homes the 2.19 findings not delivered here:
  a source/code editor for prompt content + rubric (W3/W21), a roomier eval-run output view (W28), a
  trend gap for un-run versions (W31), and an org switcher as a left drawer/rail (W39).

## [0.16.0] — 2026-07-19

Authoring & reliability polish for the eval loop: a **Cancel** on every reveal/expand form, a
**sanitized markdown editor** for prompt/rubric content, and the round-3 dogfood quick-fixes
(prompt-scoped analytics, subject-model hold, loud run failures).

### Added

- **[#2.11] Cancel on every reveal / expand-to-edit surface** ([detail](specs/archive/2.11.md)): every
  inline reveal/expand form (new org/folder/prompt/import, add-version, edit version label, run-a-version,
  create-dataset, add-fixture, generate-synthetic, edit-fixture, add-scorer, edit-scorer) now has a
  **Cancel** paired with its submit that **discards unsaved input and collapses** back to the summary row /
  closed toggle, plus **Esc-to-cancel**. Consistent label + placement via a shared `.form-actions` row.
- **[#2.10] Markdown editor with sanitized preview** ([detail](specs/archive/2.10.md)): a reusable
  **Edit ⇄ Preview** markdown editor wired into the version **Content** field and the **LlmJudge Rubric**
  (add + reconfigure). Preview renders via `marked` → `DOMPurify` → Angular's `[innerHTML]` sanitizer
  (defense in depth) — a `<script>`/`onerror`/`javascript:` payload is stripped and never executes. Source
  text stays authoritative; content remains immutable-by-add.

### Changed

- **[#2.12] Eval-loop round 3 — reliability & fair scoring** (in progress; [detail](specs/2.12.md)):
  quick-fix slices shipped —
  - **[B8]** the Analytics dataset picker is now **prompt-scoped** (`Dataset.PromptId`), not just org —
    no more foreign-prompt datasets leaking into a prompt's analytics.
  - **[R5]** add-version **defaults the Target model to the latest version's** (holding the subject model
    is the default) and **warns on a change**; Analytics **Compare flags a cross-model comparison** — so a
    prompt-vs-prompt score delta isn't silently confounded by a model swap.
  - **[R2]** any run failure is now **loud** — a timeout or non-JSON gateway 5xx (no structured `{error}`
    body) shows a clear banner instead of failing silently.

  Heavy slices remain (R1 async runs, R3 data-conditional rubric scoring, R4 variance/rationale-diff).

### Dogfooding (5.1 — ongoing)

- **[#5.1] Adopt LitmusAI across Cortex Golf & Stormboard** ([detail](specs/5.1/5.1.md)): **round-debrief**
  walked (T3 **2/6 walkable**) — v2 wins on a data-conditional rubric (avg 0.79 → 0.88; sparse fixture
  0.60 → 0.90), **backported to Cortex Golf** (`9ba2ad3c`). Surfaced findings B8/R1–R5 (all homed in 2.12;
  B8/R2/R5 shipped here). Fill sheet condensed to its durable record; a **v4 real-model (Sonnet 4.6)
  validation** remains owed. Spec enrichments logged for **[#1.16]** and **[#2.2]**.

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
