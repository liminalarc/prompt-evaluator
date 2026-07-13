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

## Routing (from 1.7)

- `/prompts` is a **folder browse**: a folder-tree sidebar (a synthetic `Root` = unfiled prompts)
  filters the list, with a breadcrumb, create-folder, and a per-row move-prompt select. Folder tree
  model + `buildFolderTree` in `folder.ts`; `FoldersApiService` in `folders/`.
- `/prompts/:id` is the **unified prompt workspace** — the prompt's versions, **its datasets**
  (list + create, via `/api/prompts/:id/datasets`), and **its analytics** (dataset picker →
  `TrendChart`) all on one page. Datasets belong to a prompt (1.7), so `DatasetsApiService.createDataset`
  takes a `promptId` and posts to the nested route.
- `/datasets` is now **browse-only** (cross-prompt list); datasets are created in the workspace.

## E2e that needs a model (from 1.2)

- An e2e that would trigger a live model call (synthetic generation) runs against a **stubbed
  eval-runner**: `docker-compose.e2e.yml` sets `EVAL_RUNNER_STUB=1`, and the spec self-skips
  unless `E2E_EVAL_RUNNER_STUB` is set. Keep such specs skipped by default so the normal e2e run
  never hits the API — see `e2e/datasets-generate.spec.ts`.
