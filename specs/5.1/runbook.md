# 5.1 — Per-prompt Run Book (LitmusAI UI, click-by-click)

> The operational companion to the 10-step playbook in [5.1.md](5.1.md). Reusable for **every**
> prompt (all 54). Generic steps here; each prompt's copy-paste values live in a fill sheet
> (`fills/<name>.md`). All manual — no API automation (the maintainer learns the tool by driving it).
> **Refreshed 2026-07-18 against the live 2.8 UI (dev v0.14.0)** — field labels/order below are literal.
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
default 0.05). **`Score trend`** charts v1→v2 per scorer; **`Regressions`** flags drops (Confirmed +
Possible); **`Compare versions`** (**`From`**/**`To`**) gives per-fixture deltas — the **Fixture** column now
shows your fixture **label** (U7), not a GUID. Iterate **v3+** on the *same dataset + scorers* until satisfied
or diminishing returns.

## Step 9 — Backport (LitmusAI signals; the source app executes)
LitmusAI **does not** edit source repos — it only tells you a better version exists. Closing the loop is a
**manual action in the source app's own process** (which may not be our flow/spec system):
copy the **winning version's text back into the source app** and commit there, **or decline with a recorded
reason** (marginal gain, model-specific, cost). Use the source repo's own commit convention.
> Golf → `AiService.WebApi/Prompts/<name>.md`. Stormboard `.md` → `StormBoard.Claude/Prompts/<name>.md`.
> The 2 Stormboard **inline** prompts (`wizard-prompts`, `asset-mapping`): backport also **extracts them to
> `Prompts/*.md`** routed through `FilePromptStore` (fixes the smell) — a structural change Stormboard may
> track in its own system; see T4.
> *(There's no "deployed" marker inside LitmusAI yet — tracked as finding **F1**; until then the fill sheet +
> T3/T4 tick are the record of what's shipped.)*

## Step 10 — Log
One line of learnings (what moved the needle). **Tick the prompt's row** in [T3](5.1.T3.md) (Golf) /
[T4](5.1.T4.md) (Stormboard) and record the backport decision (done + commit SHA, or declined + reason).

---

## Fill sheets — one per prompt
Copy-paste form values (all fields in UI order + a baseline/iteration log) live in **`fills/<name>.md`**.
Template & first example: [fills/daily-briefing.md](fills/daily-briefing.md).
