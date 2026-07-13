#!/usr/bin/env bash
#
# Seeds an in-app "Smoke Test" environment in a running LitmusAI stack — dogfoods the 1.7
# folders feature so there's always a realistic workspace to click through by hand:
#
#   Smoke Test/                 (top-level folder = the 4.1 permission boundary)
#     Summarization/            (subfolder)
#       Smoke Summarizer        (prompt, filed here, with a v1)
#         Smoke Summaries       (dataset owned by the prompt, 1 captured fixture)
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

# First id whose <field> == <value> from a JSON array on stdin (empty string if none).
find_id() { python -c "import sys,json;print(next((x['id'] for x in json.load(sys.stdin) if x.get('$1')=='''$2'''),''))"; }

curl -sf "$BASE/health" >/dev/null 2>&1 || {
  echo "x API not reachable at $BASE — is the stack up? (docker compose up -d --build --wait)" >&2
  exit 1
}

# --- "Smoke Test" top-level folder ---
folder_id=$(curl -s "$BASE/api/folders" | find_id name "Smoke Test")
if [ -z "$folder_id" ]; then
  folder_id=$(curl -sf -X POST "$BASE/api/folders" -H 'Content-Type: application/json' \
    -d '{"name":"Smoke Test","parentId":null}' | jget "['id']")
  echo "+ created folder 'Smoke Test'"
else
  echo ". folder 'Smoke Test' already present"
fi

# --- "Summarization" subfolder under it ---
sub_id=$(curl -s "$BASE/api/folders" \
  | python -c "import sys,json;p='$folder_id';print(next((f['id'] for f in json.load(sys.stdin) if f['name']=='Summarization' and f['parentId']==p),''))")
if [ -z "$sub_id" ]; then
  sub_id=$(curl -sf -X POST "$BASE/api/folders" -H 'Content-Type: application/json' \
    -d "{\"name\":\"Summarization\",\"parentId\":\"$folder_id\"}" | jget "['id']")
  echo "+ created subfolder 'Summarization'"
else
  echo ". subfolder 'Summarization' already present"
fi

# --- "Smoke Summarizer" prompt (with a v1), filed into the subfolder ---
prompt_id=$(curl -s "$BASE/api/prompts" | find_id name "Smoke Summarizer")
if [ -z "$prompt_id" ]; then
  prompt_id=$(curl -sf -X POST "$BASE/api/prompts" -H 'Content-Type: application/json' \
    -d '{"name":"Smoke Summarizer","description":"Seeded smoke-test prompt"}' | jget "['id']")
  curl -sf -X POST "$BASE/api/prompts/$prompt_id/versions" -H 'Content-Type: application/json' \
    -d '{"content":"Summarize the following thread:\n{input}","targetModel":"claude-sonnet-5","label":"baseline","sourceApp":null}' >/dev/null
  echo "+ created prompt 'Smoke Summarizer' (v1)"
else
  echo ". prompt 'Smoke Summarizer' already present"
fi
# Ensure it's filed into the subfolder (idempotent).
curl -sf -X POST "$BASE/api/prompts/$prompt_id/move" -H 'Content-Type: application/json' \
  -d "{\"folderId\":\"$sub_id\"}" >/dev/null

# --- "Smoke Summaries" dataset under the prompt, with a captured fixture ---
dataset_id=$(curl -s "$BASE/api/prompts/$prompt_id/datasets" | find_id name "Smoke Summaries")
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
echo "    Prompts -> Smoke Test -> Summarization -> Smoke Summarizer"
echo "    Open the prompt to see its versions, datasets, and analytics together."
echo "    prompt workspace: $BASE/prompts/$prompt_id"
