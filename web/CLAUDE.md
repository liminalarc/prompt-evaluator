# web/ — Angular SPA

Additive to root `CLAUDE.md`. Only layer-specific rules here.

## Purpose

The operator-facing UI:
- **Dashboards** — score trends per `Prompt × Version × Dataset`, regression flags.
- **Browsing** — prompts, versions, datasets, individual eval runs and their outputs.
- **Human review** — a labeling/calibration queue where a reviewer scores a sampled run;
  these scores feed back as an `IScorer` result and calibrate the LLM judge.

## Conventions

- Latest Angular; **standalone components** (no NgModules), typed reactive forms, the
  `inject()` function over constructor injection for new code.
- Server state via a typed API client generated from / matched to the .NET API DTOs. Keep
  a single `ApiService` per bounded area; components don't call `HttpClient` directly.
- State: signals for local/component state; a small store service only where state is
  genuinely shared. No heavyweight state library until one is warranted.
- **Testing:** Jasmine/Karma for component/service unit tests (TDD — test first);
  Playwright for the critical e2e flows (run a prompt, view its score, label a review item).
- Presentational vs. container split: charts and tables are dumb inputs-in/events-out;
  data fetching lives in container components/services.
- Charts follow the `dataviz` skill's guidance — read it before writing chart code,
  choosing colors, or building stat tiles / trend lines.

## Skeleton conventions (from 0.1)

- **Angular 20**, not the latest, while the toolchain is on Node 20 (Angular 22 needs
  Node 22+). Bump together when Node moves.
- **Styling is brand-tokens only** — import `brand-tokens/css` + `/components` in
  `styles.css`, theme via `data-theme`, reference `--sb-*` custom properties; no hardcoded
  hex. `.sb-*` classes are the component layer (`.sb-btn`, `.sb-card`, `.sb-field`, …).
- **API calls are relative (`/api/...`)** — the ng-serve proxy (`proxy.conf.json`) and the
  compose nginx both route them to the API. No per-environment base URL in the client.
- **Playwright e2e runs against a running stack** (compose), not an `ng serve` webServer;
  point it with `E2E_BASE_URL`. Unit tests use `npm run test:ci` (Karma headless).

## Routing (from 1.1)

- The app is **routed** (`provideRouter` in `app.config.ts`, routes in `app.routes.ts`). `App`
  is the shell (topbar nav + `<router-outlet>`); each page is its own standalone component.
  `Home` holds the skeleton round-trip at `/`.
- **One typed API client per bounded area**: `ApiService` (eval-runs), `PromptsApiService`
  (prompts), `DatasetsApiService` (datasets) — components never touch `HttpClient`.
  DTO-mirroring models live next to them (`prompt.ts`, `eval-run.ts`, `dataset.ts`).

## Routing (from 1.2)

- `/datasets` (list) + `/datasets/:id` (fixture browse with origin filter, capture form, generate
  trigger). Feature components in `datasets/`; they reuse `prompts/prompts.css` via `styleUrl`.

## Routing (from 1.3)

- `/eval-runs/:id` — a run's per-fixture results (output, latency/cost, one row per scorer).
  Feature components + client in `eval-runs/` (`EvalRunsApiService`, `eval-run.ts` DTO mirrors).
  `/datasets/:id` also hosts the scorer-config form, the prompt/version run trigger, and the
  runs list (which link to `/eval-runs/:id`).
- The 0.1 skeleton round-trip now posts to `/api/echo` (`ApiService.echo` + `echo.ts`), not an
  eval-run — `EvalRun` is the real evaluation aggregate.

## Routing (from 1.4)

- `/analytics` — the score-tracking dashboard: prompt + dataset selectors, a trend chart, a
  regression list, and a version-vs-version comparison. Feature components + client in `analytics/`
  (`AnalyticsApiService`, DTO mirrors in `analytics.ts`). Presentational `TrendChart` /
  `VersionComparison` are dumb (data in); the dashboard container fetches.
- **Charts use `@swimlane/ngx-charts`** (SVG, added with `@angular/cdk` + `@angular/animations` +
  `@angular/platform-browser-dynamic` pinned to Angular 20; `provideAnimations()` is wired in
  `app.config.ts`). Per the `dataviz` skill: **source series colors from the `--sb-*` brand tokens at
  runtime** (never hardcode hex) so light/dark tracks `data-theme`; the categorical order is
  primary → accent → ai → info (status tokens are reserved, never a series color); one y-axis only.
  Validate any categorical palette with the skill's `validate_palette.js` before shipping.

## Routing (from 1.7, reworked by 1.9)

- `/prompts` is an **org + folder browse** (1.9): an **organization dropdown** (`OrganizationsApiService`)
  selects the org; the main area is a **file-explorer** — a breadcrumb (`Org › … › folder`), the current
  folder's **subfolders** (click to descend), and the **prompts in the current folder**. Create-org /
  create-folder / create-prompt are **collapsible** actions; move-prompt is a per-row select scoped to the
  org. No left sidebar tree. Folders + prompts are fetched **per org** and bucketed client-side by
  `parentId` / `folderId`. `folder.ts` / `organization.ts` models; `FoldersApiService` (org-scoped),
  `OrganizationsApiService`.
- `/prompts/:id` is the **unified prompt workspace** — the prompt's versions, **its datasets**
  (list + create, via `/api/prompts/:id/datasets`), and **its analytics** (dataset picker →
  `TrendChart`) all on one page. Datasets belong to a prompt (1.7), so `DatasetsApiService.createDataset`
  takes a `promptId` and posts to the nested route.
- `/datasets` is now **browse-only** (cross-prompt list); datasets are created in the workspace.

## App shell, shared kit & org context (from 2.4)

- **Shared UI kit** lives in `web/src/app/shared/` (barrel `index.ts`): `PageHeader`,
  `Breadcrumb`, `LoadingState`, `EmptyState`, `ErrorState`, `StatusBadge`, `Chip`, plus pure
  status mappers in `status.ts` (`passBadge`, `originBadge`). **Use these for page chrome and
  status** instead of hand-rolling `.panel__head`/`.title`/`.empty`/`.error-box`. `ErrorState`
  renders the brand `.sb-field--error` and keeps `data-testid="error"` (existing selectors still
  work). Every page shows a `LoadingState` before data arrives — no more blank-until-loaded.
- **Status is a brand primitive, never a raw glyph.** Pass/fail, fixture origin, regression
  severity → `StatusBadge` (`.sb-badge--*`); scorer kind, target/judge model → `Chip` (`.sb-chip`).
  No emoji for status. Colors come only from `--sb-*` tokens (CI-adjacent hex-scan stays clean).
- **Organization is a global context**, not a per-page picker. `OrgContextStore`
  (`shared/org-context.store.ts`, root signals) owns `organizations` / `currentOrgId` /
  `currentOrg`; the **persistent left org rail** (`shared/org-rail.ts`, W39 — replaced the topbar
  `<select>`) writes it via `select()`. Selection persists to **localStorage
  and a `?org=` query param** (resolved `?org=` → localStorage → first org). Pages read
  `orgStore.currentOrgId()` and rescope in an `effect()` on switch; datasets/analytics with no
  org-scoped endpoint intersect the cross-prompt list by the org's prompt ids (no API change).
- **Topbar nav is `Dashboard · Prompts · Analytics`.** `/datasets` is demoted (route kept for
  deep-links, reached from the workspace/dashboard). `/` is the product `Dashboard`
  (`dashboard/`), assembled by `DashboardFacade` from existing read APIs (bounded per-dataset
  fan-out). The 0.1 echo skeleton lives at `/_skeleton` as a wiring smoke test only.
- **Progressive disclosure:** on the long workspace pages, data tables + the primary CTA stay
  visible; creation forms reveal behind `+` toggles (`toggle-capture`, `toggle-generate`,
  `toggle-add-scorer`, `toggle-add-version`, `toggle-create-dataset`) that stay open after submit.
  e2e opens a form once before driving it.

## Row-level disclosure, loud errors & run scoping (from 2.8)

- **The 2.4 toggle pattern extends to data _rows_.** Version history, fixtures, and scorers render
  as summary rows that expand to an inline editor (`version-row`/`fixture-row`/`scorer-row` →
  `*-detail`); eval-run fixtures collapse to a summary row (`fixture-run-summary`) that expands to
  the full output. Editable = metadata only: version **label**, fixture **label/description**,
  scorer **reconfigure/remove** — content/target-model (versions) and input/origin/seed (fixtures)
  stay immutable, matching the domain.
- **Loud failures.** API errors surface the server `{error}` body (e.g. `eval-runner: … not
  configured`) via a shared `serverError(err)` helper, not a generic string. A failed run is a
  banner, never a silent no-op.
- **The dataset run form is fixed to the dataset's owning prompt** (`Dataset.PromptId`) — version
  pick only, no cross-org prompt droplist. Runs can also be triggered from the prompt workspace
  ("+ Run a version"). Create-prompt navigates to the new prompt's workspace. Page headers are
  **type-prefixed** (`Prompt:` / `Dataset:`); `getByRole('heading', { name })` still matches by
  substring, so e2e heading assertions are unaffected.

## Markdown editor (from 2.10)

- **Markdown-bearing fields use the shared `MarkdownEditor`** (`shared/markdown-editor.ts`): a source
  `<textarea>` + an **Edit ⇄ Preview** toggle. Wired into the version **Content** field and the
  **LlmJudge Rubric** (add + reconfigure). Regex/JsonSchema scorer configs are patterns/schemas, not
  prose, so they stay plain textareas — the editor is gated on `isJudge()`/`editIsJudge()`.
- **Preview is sanitized, XSS-safe by construction:** `marked` renders → `DOMPurify` sanitizes → the
  string is bound via `[innerHTML]` so **Angular's sanitizer also runs** (defense in depth; no
  `bypassSecurityTrustHtml`). Never render user markdown any other way.
- The editor takes `[value]`/`(valueChange)` (not `ngModel`) and forwards `inputId`/`name`/`testid`
  onto its source textarea, so existing selectors (`#content`, `scorer-config`) and e2e `page.fill`
  keep working. **Source is authoritative — preview never mutates it**; Content stays
  immutable-by-add (the editor only appears on add/new-version, never in place).

## Cancel on reveal/expand forms (from 2.11)

- **Every inline reveal/expand form has a Cancel paired with its submit.** Cancel discards the form's
  unsaved field input and collapses back to the summary row / closed toggle. There is **no shared
  wrapper component** — the forms are hand-rolled (`show*` signal + footer toggle). "Shared" is a
  convention: wrap the submit + a `sb-btn--ghost` Cancel in a `<div class="form-actions">` (the pairing
  row lives in `prompts/prompts.css`, reused by all eval-loop screens via `styleUrl`), add a
  `cancelX()` handler that resets the form's signals + flips the `show*`/`expanded*Id` signal, and put
  `(keydown.escape)="cancelX()"` on the `<form>`. Cancel sits **after** the primary submit.
- Expand-to-edit **rows** (version label, fixture meta, scorer reconfigure) re-seed their editor on
  open, so their Cancel just closes the row (`expanded*Id.set(null)`).
- A new reveal surface inherits Cancel by copying that two-line pattern — don't ship a reveal form
  without one.

## Tabbed surfaces & drawers (from 2.19)

- **The prompt workspace (`/prompts/:id`) is a tabbed hub** — Versions · Datasets · Analytics · Runs.
  Panels stay in the DOM; a `[hidden]="tab() !== 'x'"` binding toggles visibility (so unit tests can
  query any panel, and a tab switch is instant). Run is an elevated header action; Compare opens the
  shared drawer.
- **Tab state lives in the URL — the global convention for any tabbed surface.** Sync the active tab to
  an **optional `?tab=` query param** so the tab is deep-linkable and, crucially, the browser Back
  button / a breadcrumb from a child page (e.g. a dataset detail) returns to the **tab you left from**,
  not the default. The pattern: a `tab` signal defaulted to the first tab; `selectTab(t)` sets the
  signal **and** navigates **explicitly** (`router.navigate(['/prompts', id], { queryParams: { tab },
  replaceUrl: true })` — a relative `navigate([])` can dedupe to a no-op on a `:id` route and never
  write the param); `ngOnInit` restores it from `route.snapshot.queryParamMap.get('tab')` (the
  component is recreated on cross-route Back, so the snapshot read is enough). A child page's breadcrumb
  crumb back to a tabbed parent carries the tab via `Crumb.queryParams` (e.g. `{ tab: 'datasets' }`).
- **Heavy/focused/occasional surfaces use the shared `Drawer`** (`shared/drawer.ts`) — right-side
  slide-over, Esc/scrim close, focus-trap, responsive. Homes scorer-edit, user↔org management, and the
  unified **Compare** (Content · Scores · Rationale off one From→To). *Not* for small inline add/edit
  forms (those keep the 2.11 reveal + Cancel pattern) and *not* for always-on primary data.

## Native `<select>` with dynamic options (from 2.23)

- **Drive the selection with `[selected]` on each `<option>`, never `[value]` on the `<select>`.** A
  `[value]` bound on a native `<select>` whose `<option>`s render from `@for` is assigned *before* the
  options mount, so the box silently falls back to its first option (this shipped a Compare drawer that
  compared v1→v2 but displayed "v1" in the To picker — U23). `[ngModel]` did **not** reflect
  synchronously under the app's change detection either. The reliable pattern is `(change)` on the
  select + `[selected]="opt.id === signal()"` on each option — Angular re-evaluates `[selected]` per
  option every CD, so the rendered choice always tracks the signal. Applies to any signal-driven select
  with `@for` options.

## E2e that needs a model (from 1.2)

- An e2e that would trigger a live model call (synthetic generation) runs against a **stubbed
  eval-runner**: `docker-compose.e2e.yml` sets `EVAL_RUNNER_STUB=1`, and the spec self-skips
  unless `E2E_EVAL_RUNNER_STUB` is set. Keep such specs skipped by default so the normal e2e run
  never hits the API — see `e2e/datasets-generate.spec.ts`.
