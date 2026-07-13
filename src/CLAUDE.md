# src/ — .NET DDD backbone

Additive to root `CLAUDE.md`. Only layer-specific rules here.

## Layering (dependencies point inward)

- **Domain** — aggregates, entities, value objects, domain events. References **nothing**
  (no EF, no ASP.NET, no HttpClient). Invariants live in the aggregate, not in services.
  Core aggregates: `Prompt` (with `PromptVersion` history), `Dataset` (of `Fixture`s),
  `EvalRun` (a prompt version scored over a dataset), `Score`, `Folder` (the prompt-organizing
  tree — 1.7).
- **Datasets belong to a prompt (1.7).** `Dataset.PromptId` is required; a dataset is created
  under one prompt and a run rejects a dataset owned by a different prompt. Folders organize
  prompts; the **top-level folder is the permission boundary** (4.1) — resolved by walking parents
  (a recursive CTE in `FolderRepository`), not a denormalized root ref, so folder-move stays a
  single-row update. Cross-prompt/shared datasets are deliberately out of scope — see spec 1.8.
- **Application** — use cases (one class per command/query) and the **ports**:
  `IPromptRepository`, `IEvaluationRunner`, `IScorer`, `IEvalRunRepository`. Depends on
  Domain only. No adapter code.
- **Infrastructure** — port implementations: EF Core (Npgsql) repositories, the HTTP
  client to `eval-runner`, the (future) Zatomic adapter. All EF mapping via
  `IEntityTypeConfiguration<T>` classes — keep the DbContext thin.
- **Api** — ASP.NET Core minimal APIs or controllers, DI wiring (composition root),
  request/response DTOs. Maps DTO ↔ domain at the edge; domain types never serialize directly.

## Conventions

- Aggregates are created through factory methods / constructors that enforce invariants;
  no public parameterless constructors except where EF requires (use a private ctor).
- Value objects are immutable `record`s; equality is by value.
- Prefer explicit `Result`/exception boundaries at the Application layer over leaking
  domain exceptions to the Api.
- One xUnit test project per layer (`Domain.Tests`, `Application.Tests`,
  `Infrastructure.Tests`). Domain tests are pure/fast; Infrastructure tests use a real
  Postgres via Testcontainers (or a disposable local db) — no mocking the DbContext.
- Migrations live in Infrastructure; never edit an applied migration — add a new one.

## Testing the ports

- `IScorer` implementations each get their own tests. Deterministic scorers are pure and
  fully unit-testable. The LLM-judge scorer is tested against a **faked** `IEvaluationRunner`
  in Application tests; the real HTTP adapter is tested separately in Infrastructure.
