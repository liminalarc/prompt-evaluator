# Prompt Evaluator

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
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Database=prompteval;Username=postgres;Password=postgres"
```

## Quick Start (Docker Compose)

The whole stack runs in containers on a shared network — this is the fastest path to a
running app and the one CI smoke-tests.

```bash
git clone <repo-url> prompt-evaluator
cd prompt-evaluator
cp .env.example .env          # set ANTHROPIC_API_KEY (not needed for the skeleton echo path)
docker compose up --build     # brings up db, eval-runner, api, web (healthy, in order)
docker compose ps             # all services should report healthy
```

Then open <http://localhost:4200>. Services address each other by name over the compose
network (`api` → `eval-runner:8000`, `api` → `db:5432`); Postgres data persists in a named
volume. `docker compose down` stops the stack; add `-v` to also drop the data volume.

## Local Setup (per-process)

> The commands below run each service directly for day-to-day development. They describe
> the target layout once the Walking Skeleton (spec 0.1) lands; until then, treat this as
> the contract each layer builds to. For a full stack, prefer Docker Compose above.

1. **Clone and enter the repo**
   ```bash
   git clone <repo-url> prompt-evaluator
   cd prompt-evaluator
   ```

2. **Start PostgreSQL** (Docker option)
   ```bash
   docker run --name prompteval-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=prompteval -p 5432:5432 -d postgres:16
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
   npm start                            # serves the SPA (default http://localhost:4200)
   ```

## Running the App

With all three running, open <http://localhost:4200>. The Angular app talks to the .NET
API, which delegates LLM-judge scoring and synthetic fixture generation to the eval-runner.

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

## Docker

Each service has its own `Dockerfile`; `docker-compose.yml` orchestrates all four (`db`,
`eval-runner`, `api`, `web`) on a shared bridge network with healthchecks, ordered
`depends_on`, and a named volume for Postgres. See **Quick Start (Docker Compose)** above.

## Deployment

Production orchestration (hosting/k8s) is out of scope for the skeleton. The compose stack
is the local + CI runtime; deployment is established later.
