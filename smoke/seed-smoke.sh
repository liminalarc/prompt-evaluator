#!/usr/bin/env bash
#
# Seeds an in-app "Smoke Test" environment in a running LitmusAI stack — dogfoods the folders +
# organizations features so there's always a realistic workspace to click through by hand:
#
#   Smoke Test (organization)          the top-level container / permission boundary (1.9)
#     Summarization/                   a folder in the org
#       Smoke Summarizer               a prompt, filed here, with a v1
#         Smoke Summaries              a dataset owned by the prompt, 1 captured fixture
#
# Idempotent by name — safe to re-run; existing items are reused, not duplicated.
#
# Usage:
#   ./smoke/seed-smoke.sh                          # against http://localhost:4240
#   BASE_URL=http://host:port ./smoke/seed-smoke.sh
#
# Requires the stack to be up:  docker compose up -d --build --wait
set -euo pipefail

BASE="${BASE_URL:-http://localhost:4240}"

# Extract a field from a JSON object on stdin, e.g.  echo "$resp" | jget "['id']"
jget() { python -c "import sys,json;print(json.load(sys.stdin)$1)"; }

# First id whose name == <value> from a JSON array on stdin (empty string if none).
find_by_name() { python -c "import sys,json;n='''$1''';print(next((x['id'] for x in json.load(sys.stdin) if x.get('name')==n),''))"; }

curl -sf "$BASE/health" >/dev/null 2>&1 || {
  echo "x API not reachable at $BASE — is the stack up? (docker compose up -d --build --wait)" >&2
  exit 1
}

# --- "Smoke Test" organization (the top-level boundary) ---
org_id=$(curl -s "$BASE/api/organizations" | find_by_name "Smoke Test")
if [ -z "$org_id" ]; then
  org_id=$(curl -sf -X POST "$BASE/api/organizations" -H 'Content-Type: application/json' \
    -d '{"name":"Smoke Test"}' | jget "['id']")
  echo "+ created organization 'Smoke Test'"
else
  echo ". organization 'Smoke Test' already present"
fi

# --- "Summarization" folder in the org ---
folder_id=$(curl -s "$BASE/api/organizations/$org_id/folders" | find_by_name "Summarization")
if [ -z "$folder_id" ]; then
  folder_id=$(curl -sf -X POST "$BASE/api/organizations/$org_id/folders" -H 'Content-Type: application/json' \
    -d '{"name":"Summarization","parentId":null}' | jget "['id']")
  echo "+ created folder 'Summarization'"
else
  echo ". folder 'Summarization' already present"
fi

# --- "Smoke Summarizer" prompt (with a v1), filed into the folder ---
prompt_id=$(curl -s "$BASE/api/organizations/$org_id/prompts" | find_by_name "Smoke Summarizer")
if [ -z "$prompt_id" ]; then
  prompt_id=$(curl -sf -X POST "$BASE/api/organizations/$org_id/prompts" -H 'Content-Type: application/json' \
    -d '{"name":"Smoke Summarizer","description":"Seeded smoke-test prompt"}' | jget "['id']")
  curl -sf -X POST "$BASE/api/prompts/$prompt_id/versions" -H 'Content-Type: application/json' \
    -d '{"content":"Summarize the following thread:\n{input}","targetModel":"claude-sonnet-5","label":"baseline","sourceApp":null}' >/dev/null
  echo "+ created prompt 'Smoke Summarizer' (v1)"
else
  echo ". prompt 'Smoke Summarizer' already present"
fi
# Ensure it's filed into the folder (idempotent).
curl -sf -X POST "$BASE/api/prompts/$prompt_id/move" -H 'Content-Type: application/json' \
  -d "{\"folderId\":\"$folder_id\"}" >/dev/null

# --- "Smoke Summaries" dataset under the prompt, with a captured fixture ---
dataset_id=$(curl -s "$BASE/api/prompts/$prompt_id/datasets" | find_by_name "Smoke Summaries")
if [ -z "$dataset_id" ]; then
  dataset_id=$(curl -sf -X POST "$BASE/api/prompts/$prompt_id/datasets" -H 'Content-Type: application/json' \
    -d '{"name":"Smoke Summaries","description":"Seeded smoke-test dataset"}' | jget "['id']")
  curl -sf -X POST "$BASE/api/datasets/$dataset_id/fixtures/capture" -H 'Content-Type: application/json' \
    -d '{"tuples":[{"promptInput":"A long thread about release planning across three teams.","input":null,"slmOutput":"raw upstream slm output","downstreamResult":"A concise summary of the release plan."}]}' >/dev/null
  echo "+ created dataset 'Smoke Summaries' (1 captured fixture)"
else
  echo ". dataset 'Smoke Summaries' already present"
fi

echo
echo "OK  Smoke Test environment ready at $BASE"
echo "    Prompts -> [org: Smoke Test] -> Summarization -> Smoke Summarizer"
echo "    Open the prompt to see its versions, datasets, and analytics together."
echo "    prompt workspace: $BASE/prompts/$prompt_id"
