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
- **No silent deferrals.** Anything a spec put *in scope* but you don't deliver — plus any
  "future/noted/out-of-scope" pointer that doesn't already name a spec — must be re-homed
  into a new or existing spec. **Each re-homing is a user decision**, surfaced at plan-time
  and again at close-out; never narrow scope unilaterally. A spec is not `DONE` until its
  deferrals are reconciled (see the checklist). When work is split out, cross-link both ways
  (the closing spec names the new one; the new one names its origin).
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

1. **Deferrals reconciled** — every in-scope item you didn't deliver (and every unhomed
   "future" pointer) has a spec home the **user chose**. Cross-link origin ↔ new spec. This
   gates `DONE` (see *No silent deferrals*).
2. **Tests green** — backend (xUnit), eval-runner (pytest), web (Karma/Playwright) as touched.
3. **Update the index** — set the spec `DONE` in `SPECIFICATIONS.md`; move its entry to
   `## Archive` and relocate the detail file to `specs/archive/<id>.md` (id never reused).
4. **Update the detail file** — tick AC checkboxes, append Decisions/Verification, add a
   Progress-log entry with the commit SHA(s).
5. **Update this CLAUDE.md** (or a subdir one) if a new convention was introduced.
6. **README** stays runnable — if setup/commands changed, update it.

## Releasing (flow-ship)

`/flow-ship` reads this section for the release mechanism. Today the deployable artifact is
the **compose stack** (local + CI only — there is no hosted environment yet; **production
deployment is spec 3.2**). A release here is a **tagged, verified build**, not a deploy.

**Version — one unified product SemVer** (pre-1.0 `0.x`), bumped together in all three places
so `/version` and the git tag agree:

- API — `ServiceVersionNumber` in `src/Api/Version/VersionEndpoints.cs`
- web — `version` in `web/package.json`
- eval-runner — its version string under `eval-runner/`

The bump is commit-derived (`feat:` → minor, `fix:` → patch, breaking → major) and confirmed
with the user before tagging. (The strings currently drift — api `0.1.0`, web `0.0.0` — so the
first release aligns all three.)

**Pre-ship validation (all must hold):**

- On `main`, clean tree, up to date with origin.
- **No unreconciled deferrals** — every closed spec's in-scope gaps were re-homed per the
  deferral protocol (see *No silent deferrals*).
- Every spec in the release is `DONE` in `SPECIFICATIONS.md`.
- The four CI gates are green on the release commit: `backend`, `eval-runner`, `web`,
  `compose-smoke`. Locally: `cd src && dotnet test`; `cd eval-runner && pytest`;
  `cd web && npm run test:ci && npm run build`; optionally `docker compose up --build --wait`.

**Cut the release:**

1. Bump the three version strings to `X.Y.Z`; commit `chore: release vX.Y.Z`.
2. Update `CHANGELOG.md` from the commits since the last tag (link `[#id]` → `specs/archive/<id>.md`).
3. Annotated tag: `git tag -a vX.Y.Z -m "…"` listing the specs shipped; push `main` + the tag.
4. CI runs on the pushed commit. **Stop here** — no deploy step until spec 3.2 lands.

`--dry-run` prints the computed version, changelog, and tag without writing or pushing.

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
