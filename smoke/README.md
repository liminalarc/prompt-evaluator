# smoke/ — manual smoke-test environment

Quick tooling to stand up a realistic, click-through-able **Smoke Test** workspace inside a
running LitmusAI stack. It dogfoods the 1.7 folders feature so there's always something to
exercise by hand — separate from the automated gates (`compose-smoke` in CI + the Playwright
e2e suite).

## Use it

```bash
# 1. bring the stack up (from the repo root)
docker compose up -d --build --wait

# 2. seed the in-app Smoke Test environment (idempotent — safe to re-run)
./smoke/seed-smoke.sh
# or point it elsewhere:
BASE_URL=http://localhost:4240 ./smoke/seed-smoke.sh

# 3. click through it in the browser at http://localhost:4240
#    Prompts -> Smoke Test -> Summarization -> Smoke Summarizer
```

## What it creates

```
Smoke Test/                 top-level folder (the 4.1 permission boundary)
  Summarization/            subfolder
    Smoke Summarizer        prompt (filed here) with a v1
      Smoke Summaries       dataset owned by the prompt, 1 captured fixture
```

Open **Smoke Summarizer** to see the unified workspace — its versions, its datasets, and its
analytics on one page.

## Notes

- Idempotent by name: re-running reuses existing items instead of duplicating them.
- Needs `curl` + `python` on PATH (both ship with the dev environment / Git Bash).
- Starting from a **fresh** DB? `docker compose down -v` first, then `up`, then seed — see the
  migration note in `specs/archive/1.7.md`.
- This is manual/exploratory tooling. Automated smoke lives in CI (`.github/workflows/ci.yml`
  → `compose-smoke`) and the Playwright specs in `web/e2e/`.
```
