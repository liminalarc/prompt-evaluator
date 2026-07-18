# LitmusAI

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
- **Identity is a bounded context inside the `Api` (4.1).** Cookie-session auth over ASP.NET Core
  Identity lives in Infrastructure behind two ports — `IUserDirectory` (credentials, reset tokens,
  org-membership grants) and `ICurrentUser` — so `Domain` stays framework-free and the identity
  store is a separate `AppIdentityDbContext` (its own migration history) on the same Postgres. The
  **organization is the permission boundary**: access is a user↔org membership resolved from
  `Prompt.OrganizationId`, enforced on every data endpoint. The **exceptions are workspace-wide
  management surfaces** — the Model Catalog (1.13), the admin user surface (4.3), and org-entity
  management (4.4: list-all/create/rename/delete orgs + manage any org's members) — all gated by a
  workspace-level **global-admin flag**
  (`AppUser.IsAdmin`, read via `IUserDirectory.IsGlobalAdminAsync` / `OrgAccess.IsGlobalAdminAsync`),
  distinct from the per-org `OrgRole`. That flag gates **management only**: a global admin can manage
  any org, but org *content* (prompts/datasets/runs) stays membership-gated — an admin reaches an
  org's content only by adding themselves as a member. Alongside the global-admin surfaces, an org's
  own **Owner** manages that org's membership from its own detail page (4.5: `/organizations/{id}`,
  list/add-by-email/set-role/remove) — an **owner-or-admin, per-org** gate
  (`OrgAccess.CanManageOrgMembersAsync`) on the member-scoped `/api/organizations/{id}/members`
  endpoints, distinct from 4.4's global-admin-only `/api/admin/organizations`. A last-owner guard
  keeps every org with ≥1 Owner (a global admin can still override via the 4.4 surface). The
  eval-runner authenticates as an **internal trusted service** (shared `X-Service-Token`), never with
  user credentials. Password-reset email is an `IEmailSender` seam (dev logs; real provider at
  deploy — spec 3.2).
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
- **Claude is the *default* judge + synthetic-data model + subject provider.** The eval-runner
  is multi-provider behind `IEvaluationRunner` (Anthropic default + OpenAI; routed by model id
  in `eval-runner/app/providers.py` — see spec 1.5). Domain/Application stay provider-agnostic.
  Default to the latest capable Claude models (Opus 4.8 / Sonnet 5). Model ids and API usage:
  consult the `claude-api` skill — never answer LLM/model questions from memory.

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

`/flow-ship` reads this section for the release mechanism. There is now a hosted **dev**
environment on **AWS App Runner** (spec 3.2; infra in `infra/`, modeled on Prism). CI
**deploys to dev continuously on every push to `main`** (the `deploy-dev` job — build+push both
images to ECR, `start-deployment` on both App Runner services, then a post-deploy smoke of the
deployed URL). A tagged release is still a **tagged, verified build** and the version marker; there
is **no prod deploy target yet** (staging/prod is future work under 3.2's "Out"). So on this
project, "the running dev app" tracks `main`, and the tag names the version stamped into images.

**Version — git-tag-derived, single source of truth is the git tag.** There is **no version
string to bump** anywhere in the tree (matching our other apps, e.g. Stormboard). The build
stamps the version from `git describe --tags` into each service; `/version` reads it back:

- **API** — `Api/Dockerfile` passes `-p:Version=$APP_VERSION`; `VersionEndpoints.cs` reads the
  assembly's `InformationalVersion` (trimming any `+<sha>`). It serves the aggregated `GET /version`
  (API + eval-runner + db) **and** a flat `GET /api/version` — the SPA-facing payload
  `{version, commit, buildTime, environment, channel}` (3.3).
- **eval-runner** — `Dockerfile` bakes `APP_VERSION` as an env var (like `GIT_COMMIT`);
  `app/main.py` reads `os.environ["APP_VERSION"]`.
- **web** — surfaces the running version via the flat `GET /api/version` (3.3): a `VersionService`
  signal loaded by an app initializer drives a footer **build chip** + a topbar **env badge**.
  `web/package.json` `version` stays an unused `0.0.0` placeholder (not the source of truth).
- **Deploy channel (`DEPLOY_CHANNEL`) — the reliable dev/prod discriminator (3.3).** `ASPNETCORE_-
  ENVIRONMENT` is *not* trusted for this (a host can report `Production` everywhere), so the UI keys
  off `channel`: CI derives `APP_CHANNEL` from the git-ref (a `v*` tag → `prod`, else `dev`) → Docker
  `ENV DEPLOY_CHANNEL` → the `/api/version` payload; a plain local build is `local`. The AWS dev
  deploy (3.2) builds on `main`, so its channel is `dev`. Upper environments (a real staging/prod
  deploy target that sets `prod`) remain future work.
- CI (`compose-smoke`) computes `APP_VERSION` from `git describe` and `APP_CHANNEL` from the git-ref,
  passing both as build-args. Local/non-CI builds are `0.0.0-dev` / `local`. **Tests assert the
  version's (and channel's) shape, never a literal** — so a release never breaks a test.

The bump is commit-derived (`feat:` → minor, `fix:` → patch, breaking → major) and confirmed
with the user before tagging — it decides the **tag name**, nothing else.

**Pre-ship validation (all must hold):**

- On `main`, clean tree, up to date with origin.
- **No unreconciled deferrals** — every closed spec's in-scope gaps were re-homed per the
  deferral protocol (see *No silent deferrals*).
- Every spec in the release is `DONE` in `SPECIFICATIONS.md`.
- The four CI gates are green on the release commit: `backend`, `eval-runner`, `web`,
  `compose-smoke`. Locally: `cd src && dotnet test`; `cd eval-runner && pytest`;
  `cd web && npm run test:ci && npm run build`; optionally `docker compose up --build --wait`.
- (Dev deploy is continuous on `main` via the `deploy-dev` job — it runs after the four gates and
  is **not** a pre-ship gate itself, but a red `deploy-dev` means the running dev app is stale.)

**Cut the release:**

1. Update `CHANGELOG.md` from the commits since the last tag (link `[#id]` → `specs/archive/<id>.md`);
   commit `chore: release vX.Y.Z`. (No version strings to bump — the tag is the version.)
2. Annotated tag: `git tag -a vX.Y.Z -m "…"` listing the specs shipped; push `main` + the tag.
3. CI runs on the pushed commit and stamps `X.Y.Z` into the built images from the tag.
4. **Deploy:** the push to `main` already ran `deploy-dev`, rolling the dev App Runner services to
   this build and smoking the deployed URL. Confirm `GET /api/version` on the dev URL shows the new
   commit/build time. (Provisioning/first-time setup + a prod target: see `infra/README.md` / 3.2.)

`--dry-run` prints the computed version, changelog, and tag without writing or pushing.

## Project Structure

```
litmus-ai/
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
