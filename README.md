# LitmusAI

Evaluation harness and regression tracker for the LLM/SLM prompts embedded across our
applications. Copy prompts into a versioned registry, run them against curated datasets,
score them (deterministic + LLM-judge + human), and watch score movement across
improvements.

- **Backbone:** .NET (DDD) — `src/`
- **Eval service:** Python / FastAPI — `eval-runner/`
- **Front end:** Angular — `web/`
- **Database:** PostgreSQL

See `CLAUDE.md` for architecture and conventions, `SPECIFICATIONS.md` for the backlog.

## Prerequisites

- **.NET SDK** 10.0+ — <https://dotnet.microsoft.com/download>
- **Node.js** 20+ and npm — for the Angular app
- **Python** 3.12+ — for the eval-runner service
- **PostgreSQL** 16+ — locally or via Docker
- **Anthropic API key** — for LLM-judge scoring and synthetic fixture generation
  (get one at <https://console.anthropic.com/>)

## Environment Variables

| Variable | Used by | Purpose |
|---|---|---|
| `ConnectionStrings__Postgres` | `src/Api` | Postgres connection string |
| `EvalRunner__BaseUrl` | `src/Api` | Base URL of the Python eval-runner (e.g. `http://localhost:8000`) |
| `ANTHROPIC_API_KEY` | `eval-runner` | Claude API key for judge + synthetic generation |

For local .NET development, prefer user-secrets over environment variables for the
connection string and any keys:

```bash
cd src/Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=4243;Database=litmusai;Username=postgres;Password=postgres"
```

## Quick Start (Docker Compose)

The whole stack runs in containers on a shared network — this is the fastest path to a
running app and the one CI smoke-tests.

```bash
git clone <repo-url> litmus-ai
cd litmus-ai
cp .env.example .env          # set ANTHROPIC_API_KEY (not needed for the skeleton echo path)
docker compose up --build     # brings up db, eval-runner, api, web (healthy, in order)
docker compose ps             # all services should report healthy
```

Then open <http://localhost:4240>. Host ports are in a dedicated **4240–4243** block to
avoid colliding with other local apps:

| Service | Host port | In-container |
|---|---|---|
| web | **4240** | 80 |
| api | 4241 | 8080 |
| eval-runner | 4242 | 8000 |
| db (Postgres) | 4243 | 5432 |

Services still address each other by name over the compose network (`api` →
`eval-runner:8000`, `api` → `db:5432`) — those are the *in-container* ports, unaffected by
the host mappings. Postgres data persists in a named volume. `docker compose down` stops the
stack; add `-v` to also drop the data volume.

## Local Setup (per-process)

> The commands below run each service directly for day-to-day development. They describe
> the target layout once the Walking Skeleton (spec 0.1) lands; until then, treat this as
> the contract each layer builds to. For a full stack, prefer Docker Compose above.

1. **Clone and enter the repo**
   ```bash
   git clone <repo-url> litmus-ai
   cd litmus-ai
   ```

2. **Start PostgreSQL** (Docker option)
   ```bash
   docker run --name litmusai-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=litmusai -p 4243:5432 -d postgres:16
   ```

3. **Backend (.NET)**
   ```bash
   cd src
   dotnet restore
   dotnet run --project Api            # serves the API (default http://localhost:5080)
   ```
   The API applies EF Core migrations on startup when a connection string is configured
   (see user-secrets above), so no manual `dotnet ef database update` is needed. To scaffold
   a new migration: `dotnet ef migrations add <Name> --project Infrastructure --startup-project Infrastructure`.

4. **Eval-runner (Python)**
   ```bash
   cd eval-runner
   python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
   pip install -r requirements.txt
   export ANTHROPIC_API_KEY=sk-ant-...
   uvicorn app.main:app --reload --port 8000
   ```

5. **Front end (Angular)**
   ```bash
   cd web
   npm install
   npm start                            # serves the SPA (http://localhost:4240)
   ```

## Running the App

With all three running, open <http://localhost:4240>. The Angular app talks to the .NET
API, which delegates LLM-judge scoring and synthetic fixture generation to the eval-runner.

### Capture-ingestion schema (datasets)

Apps emit captured tuples to `POST /api/datasets/{id}/fixtures/capture` to land ground-truth
fixtures. The body is `{ "tuples": [ <tuple>, ... ] }`, each tuple in provenance order:

```jsonc
{
  "input":            "upstream input to the SLM (optional; provenance only, not stored)",
  "slmOutput":        "the SLM's output (optional) -> fixture upstreamContext",
  "promptInput":      "the prompt's actual input (required) -> fixture input",
  "downstreamResult": "optional reference/expected output -> fixture expectedOutput"
}
```

Every stored text field is run through a PII redaction pass at ingest (email/phone →
`[REDACTED-*]`). Synthetic fixtures are added separately via generation (spec 1.2, slice 4)
and always link back to the captured seed they were generated from.

### Evaluation runs (spec 1.3)

Scorers are configured **per dataset** and reused by every run, so version comparisons stay
like-for-like:

- `POST /api/datasets/{id}/scorers` — add a scorer: `{ "kind": "Regex|JsonSchema|ExactMatch|FuzzyMatch|Latency|Cost|LlmJudge", "config": "...", "judgeModel": "claude-opus-4-8" }`. `config` is the pattern / schema / threshold / rubric; `judgeModel` applies only to `LlmJudge` (default `claude-opus-4-8`, or `claude-sonnet-5` / `claude-haiku-4-5`) and is **part of the scorer's identity** — changing it starts a new score series.
- `POST /api/datasets/{id}/eval-runs` — run a prompt version over the dataset: `{ "promptId": "...", "promptVersionId": "..." }`. Each fixture is executed on the prompt's target model (via the eval-runner), then every configured scorer scores the output. Runs are append-only.
- `GET /api/eval-runs/{id}` — the run with per-fixture output, latency/cost, and one score per scorer. `GET /api/datasets/{id}/eval-runs` lists a dataset's runs.

In the UI, the dataset page configures scorers and triggers a run; `/eval-runs/:id` shows the
per-fixture scores.

### Ops endpoints

- `GET /health` — liveness (API and eval-runner).
- `GET /version` — the API returns its own `version` + build `commit` and aggregates its
  dependencies (a live eval-runner probe and the Postgres engine version). eval-runner
  exposes its own `GET /version`. In compose these are on `http://localhost:4241/version`
  (api) and `http://localhost:4242/version` (eval-runner). The build `commit` comes from the
  `GIT_COMMIT` build-arg (`dev` when unset).

## Running Tests

```bash
# Backend
cd src && dotnet test

# Eval-runner
cd eval-runner && pytest

# Front end
cd web && npm test          # unit (Karma)
cd web && npm run e2e       # end-to-end (Playwright)
```

The synthetic-generation e2e (`e2e/datasets-generate.spec.ts`) is skipped by default because
generation normally makes a live model call. To exercise the in-browser Generate flow
deterministically, bring the stack up with the eval-runner in stub mode and set the flag:

```bash
docker compose -f docker-compose.yml -f docker-compose.e2e.yml up -d --build
cd web && E2E_EVAL_RUNNER_STUB=1 npm run e2e
```

## Docker

Each service has its own `Dockerfile`; `docker-compose.yml` orchestrates all four (`db`,
`eval-runner`, `api`, `web`) on a shared bridge network with healthchecks, ordered
`depends_on`, and a named volume for Postgres. See **Quick Start (Docker Compose)** above.

## Deployment

Production orchestration (hosting/k8s) is out of scope for the skeleton. The compose stack
is the local + CI runtime; deployment is established later.
