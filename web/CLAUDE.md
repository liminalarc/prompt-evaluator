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
