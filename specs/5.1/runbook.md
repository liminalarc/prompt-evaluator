# 5.1 — Per-prompt Run Book (LitmusAI UI, click-by-click)

> The operational companion to the 10-step playbook in [5.1.md](5.1.md). Reusable for **every**
> prompt (all 54). Generic steps here; each prompt's copy-paste values live in a fill sheet
> (`fills/<name>.md`). All manual — no API automation (the maintainer learns the tool by driving it).
> Refined as the shakeout surfaces UI realities.
>
> **Dev:** `https://dytjmtfgmj.us-east-1.awsapprunner.com`
> **Score identity:** `Prompt × Version × Dataset × Scorer` — a **Version** is immutable (no edit/
> per-version delete; only *Add version*, or delete the whole prompt). A **Dataset** is the fixed
> yardstick you re-run every version against, so it's named for coverage, never a version.

## Step 0 — Session setup (once per session)
1. Log in.
2. **Topbar org switcher** → select the app's org (`Cortex Golf` / `Stormboard`). It's a **global
   context** (persists via `?org=` + localStorage), not a per-page picker — set it once.

## Step 1 — Study (in the source repo, read-only)
Pull the row from [catalog.md](catalog.md): purpose · **subject model** (→ Step 2 target model) · input
shape · output shape · call site · capture feasibility. Note the output's **hard rules** (→ Step 4
rubric) and what the app actually runs it on (→ fidelity). Capture the values into the prompt's
`fills/<name>.md` as you go.

## Step 2 — Register v1 (baseline)
1. **Prompts** (topbar) → confirm org → reveal **create-prompt** → **Name** = the prompt's file name;
   **Description** optional. Create.
2. Open the prompt (`/prompts/<id>`) → **+ Add version**:
   - **Content** — paste the prompt text **verbatim** (it becomes the *system prompt*).
   - **Target model** — the model the app uses (**fidelity**). *Baseline on the app's real model or the
     result won't transfer.*
   - **Label** — optional annotation (e.g. `baseline`); the v-number is automatic.
   - **Add version.** Confirm the history row shows the right **Target model**.

## Step 3 — Dataset + fixtures
1. In the workspace → **Datasets** section → reveal **create dataset**: **Name** (coverage, not a
   version) + **Description**. Open it (`/datasets/<id>`).
2. **Fixtures** card → **+ Capture fixture**, once per fixture:
   - **Prompt input** — paste one input (→ the user turn the model sees).
   - **Upstream SLM output (optional)** — blank unless the input is literally derived from an upstream
     model's output (then paste that; it's prepended as context).
   - **Expected output (optional)** — blank for freeform prompts (judged by rubric, not a reference);
     fill only when there's a real reference answer (e.g. extraction ground-truth).
   - **Capture fixture.** Repeat. Confirm the row count.
> ⚠️ **Known quirk:** the capture form stamps origin **Captured**. Hand-written *synthetic* fixtures get
> mislabeled (only **+ Generate synthetic**, which needs a captured seed, marks Synthetic). Cosmetic for
> now — logged as a 5.1 finding. Truly-captured real inputs go through this same form correctly.

## Step 4 — Scorers (compose per dataset; every run applies the set)
**Scorers** card → **+ Add scorer**. Kinds: `Regex · JsonSchema · ExactMatch · FuzzyMatch · Latency ·
Cost · LlmJudge`. Pick per output family (catalog notes the family per prompt):
- **Freeform prose** → **LlmJudge** (primary): choose a **Judge model stronger than the subject** (Sonnet
  5 / Opus 4.8 — never the same tier as the subject), paste a **Rubric**. Optional **Regex** guardrail
  for a hard rule (e.g. `[0-9]` = "cites a number").
- **Strict JSON** → **JsonSchema** (+ **ExactMatch**/**FuzzyMatch** where a reference exists).
- **Ops interest** → **Latency**/**Cost** (add later; not quality).

## Step 5 — Baseline run
**Run evaluation** card → **Prompt** = this prompt → **Version** = `v1 · <target model>` (confirm it's the
app's model) → **Run evaluation**. Lands on `/eval-runs/<id>`: per-fixture **output**, **latency/cost**,
one **score row per scorer**. **Record the baseline** in the fill sheet — judge value per fixture +
aggregate, and which fixtures fail any guardrail. That's the number to beat.

## Step 6 — Diagnose
On the run page, read the **low-scoring fixtures' outputs + judge detail**. Form **one** hypothesis to
improve (e.g. "over-runs the word limit on sparse data," "vague when only stats are present").

## Step 7 — Improve → v2
Workspace → **+ Add version**: revised **Content**; **Target model = SAME** as v1 (hold the model
constant — you're testing the *prompt*); **Label** = `v2 — <hypothesis>`. Add version.

## Step 8 — Compare
Dataset → **Run evaluation** → **Version** = `v2` → Run. Then **Analytics** (topbar) → select this prompt
+ dataset → the **trend chart** shows v1→v2 movement per scorer, the **regression list** flags any drop,
and **version-vs-version comparison** gives per-fixture detail. Iterate **v3+** on the *same dataset +
scorers* until satisfied or diminishing returns.

## Step 9 — Backport
Copy the **winning version's text back into the source app** and commit there
(`[<prompt>] improve …`), **or decline with a recorded reason** (marginal gain, model-specific, etc.).
> Golf → `AiService.WebApi/Prompts/<name>.md`. Stormboard `.md` → `StormBoard.Claude/Prompts/<name>.md`.
> The 2 Stormboard **inline** prompts (`wizard-prompts`, `asset-mapping`): backport also **extracts them
> to `Prompts/*.md`** routed through `FilePromptStore` (fixes the smell) — see T4.

## Step 10 — Log
One line of learnings (what moved the needle). **Tick the prompt's row** in [T3](5.1.T3.md) (Golf) /
[T4](5.1.T4.md) (Stormboard) and record the backport decision.

---

## Fill sheets — one per prompt
Copy-paste form values (all fields in UI order + a baseline/iteration log) live in **`fills/<name>.md`**.
Template & first example: [fills/daily-briefing.md](fills/daily-briefing.md).
