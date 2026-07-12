# Prompt Evaluator

Evaluation harness and regression tracker for the LLM/SLM prompts embedded across our
applications. Pull prompts into a versioned registry, run them against curated datasets,
score them (deterministic + LLM-judge + human), and track score movement across
improvements. An advisory layer (prompt-engineering advice) comes later.

## Architecture

- **DDD .NET backbone.** `src/` is a Clean/DDD layering: `Domain` (aggregates, value
  objects — zero dependencies) → `Application` (use cases + ports) → `Infrastructure`
  (adapters: EF Core, HTTP) → `Api` (ASP.NET Core, composition root). Dependencies point
  inward only; `Domain` references nothing.
- **Ports keep the volatile parts swappable.** The three seams that matter:
  - `IPromptRepository` — prompts are **copied into** our registry now; the port lets
    [Zatomic](#) (our own prompt/version tool) become the backing store later with no
    domain change.
  - `IEvaluationRunner` — LLM-judge scoring and synthetic-fixture generation run in the
    Python `eval-runner` service; .NET calls it over HTTP. Python is an execution detail,
    not a domain authority.
  - `IScorer` — one abstraction, three implementations: deterministic (in-process),
    LLM-judge (→ eval-runner), human (→ Angular review queue). Scorers compose per dataset.
- **Capture-first fixtures.** For prompts whose input is an upstream SLM's output, real
  test data is *captured* from the apps (the ground-truth corpus) and *synthetic* data
  only fills coverage gaps — seeded from captured examples so the distribution matches.
  A fixture is a fixture regardless of origin; see `specs/1.2.md`.
- **Score identity is `Prompt × Version × Dataset × Scorer`.** Every run is persisted;
  nothing is overwritten. Regression detection compares versions over the same dataset.
- **Angular SPA** for dashboards (score trends, regression flags), prompt/version
  browsing, and the human-review labeling UI.
- **PostgreSQL** via EF Core (Npgsql). JSONB stores fixtures and raw model outputs; scores
  and run metadata are relational for trend queries.
- **Claude is the judge + synthetic-data model.** Default to the latest capable models
  (Opus 4.8 / Sonnet 5). Model ids and API usage: consult the `claude-api` skill — never
  answer LLM/model questions from memory.

## Development Rules

- **TDD is mandatory.** Write the failing test first, then the code. Backend: xUnit.
  Python: pytest. Angular: Jasmine/Karma (unit) + Playwright (e2e).
- **Thin vertical slices.** Each slice is runnable and testable end-to-end; commit per
  slice. No big-bang layers.
- **No premature abstractions.** Introduce a port/interface when a second implementation
  or a test seam actually demands it — not speculatively. (The three named ports above are
  the deliberate exceptions; they exist because the concept requires them.)
- **Domain purity.** No EF Core attributes, HTTP types, or framework leakage in `Domain`.
  Persistence is configured in `Infrastructure` (EF fluent config / separate config classes).
- **Conventional commits**, tagged with the spec id: `feat: [#1.2] add fixture capture
  ingestion`. Types: `feat` / `fix` / `chore` / `docs` / `refactor` / `test`.
- **Secrets never committed.** API keys via environment variables / user-secrets; see README.

## Spec Status Vocabulary

`NOT STARTED` · `IN PROGRESS` · `PARTIAL` · `DONE` · `SUPERSEDED`

Status lives **only** in `SPECIFICATIONS.md` (the index) — never in a `specs/<id>.md` file.

## Feature Completion Checklist

When a spec is done:

1. **Tests green** — backend (xUnit), eval-runner (pytest), web (Karma/Playwright) as touched.
2. **Update the index** — set the spec `DONE` in `SPECIFICATIONS.md`; move its entry to
   `## Archive` and relocate the detail file to `specs/archive/<id>.md` (id never reused).
3. **Update the detail file** — tick AC checkboxes, append Decisions/Verification, add a
   Progress-log entry with the commit SHA(s).
4. **Update this CLAUDE.md** (or a subdir one) if a new convention was introduced.
5. **README** stays runnable — if setup/commands changed, update it.

## Project Structure

```
prompt-evaluator/
├── CLAUDE.md              # this file — root conventions (always loaded)
├── README.md             # clone → running locally
├── SPECIFICATIONS.md     # the index — single source of truth for status
├── specs/                # one detail file per spec (specs/<id>.md); archive/ for done
├── src/                  # .NET DDD backbone (see src/CLAUDE.md)
│   ├── Domain/           # aggregates, value objects — no dependencies
│   ├── Application/      # use cases + ports (IPromptRepository, IEvaluationRunner, IScorer)
│   ├── Infrastructure/   # EF Core (Postgres), HTTP client → eval-runner, adapters
│   └── Api/              # ASP.NET Core Web API — composition root
├── eval-runner/          # Python FastAPI: LLM-judge scoring + synthetic fixture generation
│   └── CLAUDE.md
└── web/                  # Angular SPA — dashboards, browsing, human-review UI
    └── CLAUDE.md
```

## See Also

- `src/CLAUDE.md` — .NET / DDD layer conventions
- `eval-runner/CLAUDE.md` — Python eval-runner service conventions
- `web/CLAUDE.md` — Angular conventions
- `SPECIFICATIONS.md` — the backlog index; detail in `specs/<id>.md`
