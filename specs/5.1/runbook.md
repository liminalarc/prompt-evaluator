# 5.1 — Per-prompt Run Book (LitmusAI UI, click-by-click)

> The operational companion to the 10-step playbook in [5.1.md](5.1.md). Reusable for **every**
> prompt (all 54). Generic steps here; each prompt's copy-paste values live in a fill sheet
> (`fills/<name>.md`). All manual — no API automation (the maintainer learns the tool by driving it).
> **Refreshed 2026-07-18 against the live 2.8 UI (dev v0.14.0)** — field labels/order below are literal.
> **Dev now v0.16.0 (2026-07-19):** every reveal form has a **Cancel** (2.11); **Content** + LlmJudge
> **Rubric** are a markdown editor with an **Edit ⇄ Preview** toggle (2.10 — paste into *Edit*); add-version
> **defaults Target model to the latest version's** and **warns** if you change it (2.11/R5 — expected when a
> validation deliberately switches models); a run failure now shows a **loud banner** (R2); and heavy runs no
> longer time out at ~100s (R1 band-aid raised the limit to 5 min).
> **Dev now v0.21.0+ (2026-07-20) — backporting is tool-native:** the prompt workspace **Deployment** card
> tracks the **Current in source** marker + a single **Backport target** ranked by the **weighted composite**
> (1.16 + 2.9), and **`Prepare backport`** generates the drop-in — copy the exact prompt or download a markdown
> with the diff-vs-Current + score deltas (1.20). **Step 9 is rewritten around this** (supersedes the hand-copy
> process); **`Mark backported`** is now the in-tool record of what's live.
>
> **Dev:** `https://dytjmtfgmj.us-east-1.awsapprunner.com`
> **Score identity:** `Prompt × Version × Dataset × Scorer` — a **Version** is immutable (content +
> target model never change; add a new version instead — its label/description stay editable). A
> **Dataset** is the fixed yardstick you re-run every version against, so it's named for coverage,
> never a version.

## Step 0 — Session setup (once per session)
1. Log in.
2. **Topbar → `Org` switcher** (right side, left of your name) → select the app's org (`Cortex Golf` /
   `Stormboard`). Global context — persists via `?org=` + localStorage (`litmus.currentOrgId`); set it once.
   Topbar nav: `Dashboard · Prompts · Analytics`.

## Step 1 — Study (in the source repo, read-only)
Pull the row from [catalog.md](catalog.md): purpose · **subject model** (→ Step 2 target model) · input
shape · output shape · call site · capture feasibility. Note the output's **hard rules** (→ Step 4
rubric) and what the app actually runs it on (→ fidelity). Capture the values into the prompt's
`fills/<name>.md` as you go.

## Step 2 — Register v1 (baseline)
1. **Prompts** (topbar) → confirm org → **`+ New prompt`** (reveals the *New prompt* card):
   - **`Prompt name in <org>`** = the prompt's file name; **`Description (optional)`**. → **`Create prompt`**.
   - You land on the prompt workspace (`/prompts/<id>`) automatically (U1).
2. **`Version history`** card → **`+ Add version`**. Fields in order:
   - *(optional)* **`Import content from a file`** — or skip and paste.
   - **`Content`** (textarea) — paste the prompt text **verbatim** (becomes the *system prompt*).
   - **`Target model`** — the model the app uses (**fidelity**). *Baseline on the app's real model or the
     result won't transfer.*
   - **`Label (optional description)`** — e.g. `baseline`; the v-number is automatic.
   - **`Add version.`** Confirm the history row shows the right **Target model**.

## Step 3 — Dataset + fixtures
1. Workspace → **`Datasets`** card → **`+ New dataset`**: **`New dataset name`** (coverage, not a version)
   + **`Description (optional)`**. → **`Add dataset`**. Open it (`/datasets/<id>`).
2. **`Fixtures`** card → **`+ Add fixture`**, once per fixture. Fields in order:
   - **`Label (optional)`** — a short scenario name ("improving mid-handicapper"); shown in the table.
   - **`Description (optional)`**.
   - **`Origin`** — **`Captured (from real app traffic)`** for real inputs; **`Synthetic (hand-written)`**
     for ones you author. *(2.8/U8 fixed the old mislabel — pick the honest origin here.)*
   - **`Prompt input`** — one input (→ the user turn the model sees).
   - **`Upstream SLM output (optional)`** — blank unless the input is literally derived from an upstream
     model's output (then paste it; it's prepended as context).
   - **`Expected output (optional)`** — blank for freeform prompts (judged by rubric); fill only with a real
     reference answer (e.g. extraction ground-truth).
   - **`Add fixture.`** Repeat. Confirm the row count and the `Origin` filter.
> To grow coverage from a captured seed, use **`+ Generate synthetic`** (Coverage goals / Edge cases /
> Count → **Generate**) — it correctly stamps origin **Synthetic**.

## Step 4 — Scorers (compose per dataset; every run applies the set)
**`Scorers`** card → **`+ Add scorer`**. **`Scorer`** kinds: `Regex · JsonSchema · ExactMatch · FuzzyMatch ·
Latency · Cost · LlmJudge`. The config field relabels per kind — **`Rubric`** (LlmJudge), **`Config
(required)`** (Regex/JsonSchema), **`Config (optional)`** (others); it's a textarea. Pick per output family:
- **Freeform prose** → **LlmJudge** (primary): the **`Judge model`** field appears — choose one **stronger
  than the subject** (Sonnet 5 / Opus 4.8 — never the same tier), paste the multi-line **`Rubric`**.
  Optional **Regex** guardrail for a hard rule (e.g. `[0-9]` = "cites a number").
- **Strict JSON** → **JsonSchema** (+ **ExactMatch**/**FuzzyMatch** where a reference exists).
- **Ops interest** → **Latency**/**Cost** (not quality).
Config is **required** for Regex, JsonSchema, and LlmJudge (Add is disabled until valid). → **`Add scorer`**.

## Step 5 — Baseline run
Two entry points (same run):
- **From the workspace** — **`Run a version`** card → **`Version`** = `v1 · <target model>` → **`Dataset`** →
  **`Run evaluation`**.
- **From the dataset** — **`Run evaluation`** card: **`Prompt`** is fixed to the owning prompt (read-only,
  B3) → **`Version`** = `v1 · <target model>` (confirm it's the app's model) → **`Run evaluation`**.

Lands on `/eval-runs/<id>`: each fixture is a **summary row** (label + per-scorer badges) that **expands**
(U10) to output, latency/tokens/cost, and a score row per scorer. **Record the baseline** in the fill sheet —
judge value per fixture + aggregate, and which fixtures fail any guardrail. That's the number to beat. The
dataset's **`Runs`** table shows `Version · Model · Scorers · Fixtures` (U14).

## Step 6 — Diagnose
On the run page, expand the **low-scoring fixtures** and read output + judge detail. Form **one** hypothesis
(e.g. "over-runs the word limit on sparse data," "vague when only stats are present").

## Step 7 — Improve → v2
Workspace → **`Version history`** → **`+ Add version`**: **`Content`** is **pre-seeded from the latest
version** (U11) — edit it in place; **`Target model` = SAME** as v1 (hold the model constant — you're testing
the *prompt*); **`Label`** = the hypothesis only (e.g. `firm length + ban computed stats`) — the `v2` number
is automatic (U4), don't repeat it in the label. → **`Add version`**.

## Step 8 — Compare
**Analytics** (topbar → *Score Analytics*) → **`Prompt`** + **`Dataset`** (+ **`Regression threshold`**,
default 0.05). **`Score trend`** charts v1→v2 per scorer **plus a `weighted composite` line** (2.9) — one
overall-quality number blending the per-scorer means by their per-dataset weights, so a high-signal scorer
(LLM judge) outweighs a low-signal one (RegEx). **`Regressions`** flags drops (Confirmed + Possible);
**`Compare versions`** (**`From`**/**`To`**) gives per-fixture deltas — the **Fixture** column now shows your
fixture **label** (U7), not a GUID. Iterate **v3+** on the *same dataset + scorers* until satisfied or
diminishing returns.
> **The same composite drives the backport target.** Once you set a Current marker (Step 9), the workspace
> **Deployment** card names the single version to ship — ranked by this weighted composite over same-scorer-config
> series, so a mid-history rubric change can't mis-rank it (this is what now picks round-debrief **v7**, not v2).

## Step 9 — Backport (tool-native: Set Current → Prepare backport → Mark backported)
**Process (revised 2026-07-20 — now tool-native; supersedes the hand-copy step).** LitmusAI tracks deployment
state (1.16) and generates the artifact (1.20); it still **never commits into a source repo** — a source-repo
agent applies the drop-in.
1. **Mark what's live.** Workspace → **`Versions`** tab → **`Deployment`** card (or a version row) →
   **`Set as current in source`** on the version the app runs *today*. Exactly one Current per prompt
   (nullable until first set). *(The marker records **which version** is live — not a commit SHA. The
   optional `commitSha` field is plumbed in the API/domain but has **no UI input** and is deliberately **not
   tracked by hand** — it goes stale immediately and source git history + `backport/` is the provenance;
   auto-capture waits for [3.1]'s wired-in write. See finding **D2**.)*
2. **Read the recommendation.** With Current set, the Deployment card badges the single **`Backport target`** —
   the highest-scoring version above Current by the **weighted composite** (2.9; same-scorer-config, so a
   mid-history rubric change can't mis-rank it). **No badge = nothing beats Current → done** (decline/log).
3. **Generate the artifact.** Deployment card → **`Prepare backport`** (drawer opens). Two outputs:
   - **`Copy exact prompt`** — the target version's exact content → clipboard; paste straight into the source file.
   - **`Download markdown`** — a `.md` with `Current vN → target vM`, target model, Current's SHA, the full new
     content, the **diff vs Current**, the per-scorer **score deltas**, and an apply checklist.
4. **Record + apply.** Save the drop-in into **`specs/5.1/backport/<name>.md`** — now **produced by the tool**
   (Copy/Download), no longer hand-copied — and note best-version + evidence in **`backport/README.md`**. A
   **source-repo agent** applies it in the app's own process/commit convention — **or declines with a recorded
   reason** (marginal gain, model-specific, cost).
5. **Close the loop in-tool.** Once shipped, Deployment card → **`Mark backported → vN`**. This moves the Current
   marker to the shipped version and is now the **official in-tool record of what's live**; the target badge
   clears when Current is the top scorer.
> Target paths (README/agent): Golf → `server/src/AiService/AiService.WebApi/Prompts/<name>.md`; Stormboard
> `.md` → `StormBoard.Claude/Prompts/<name>.md`. The 2 Stormboard **inline** prompts (`wizard-prompts`,
> `asset-mapping`) also **extract to `Prompts/*.md`** via `FilePromptStore` (fixes the smell) — Stormboard's call; T4.
> **Before backporting, confirm on the *real* model + across runs:** hold the subject model (R5 — add-version
> defaults to the latest model + warns on change), and a single run lies — read the **rationale**, not just the
> number (R4/R7), and run a noisy fixture 2–3× (round-debrief F4 swung 0.82→0.72 on one run).
> **Why tool-native now:** F1 (no deployed marker) shipped as **[1.16]**; weighted target ranking as **[2.9]**;
> the artifact as **[1.20]**. Direct source-repo commits proved messy (an unrelated Golf commit rode along) and
> were reverted — the `backport/` file + the in-tool marker are the record; wired-in PR/registry write is **[3.1]**.

## Step 10 — Log
One line of learnings (what moved the needle). **Tick the prompt's row** in [T3](5.1.T3.md) (Golf) /
[T4](5.1.T4.md) (Stormboard) and record the backport decision — now **the in-tool `Mark backported` state**
(Current moved to target vN) **or** declined + reason. The `backport/<name>.md` file stays as the committed drop-in
the source-repo agent consumes.

---

## Fill sheets — one per prompt
Copy-paste form values (all fields in UI order + a baseline/iteration log) live in **`fills/<name>.md`**.
Template & first example: [fills/daily-briefing.md](fills/daily-briefing.md).
