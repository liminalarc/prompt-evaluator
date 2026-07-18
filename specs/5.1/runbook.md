# 5.1 — Per-prompt Run Book (LitmusAI UI, click-by-click)

> The operational companion to the 10-step playbook in [5.1.md](5.1.md). Reusable for **every**
> prompt (all 54). Generic steps + a worked example in **`daily-briefing`** (Cortex Golf). All manual —
> no API automation (that's the point; the maintainer learns the tool by driving it). Refined as the
> shakeout surfaces UI realities.
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
rubric) and what the app actually runs it on (→ fidelity).
> **daily-briefing:** subject `claude-haiku-4-5-20251001`, MaxTokens 300. Input = a plain-text stats
> block assembled by `BriefingPromptBuilder` (handicap/stats/recent-rounds/trend; sections optional).
> Output = 2-3 sentences, 75-100 words, plain text, cites real numbers, invents nothing.

## Step 2 — Register v1 (baseline)
1. **Prompts** (topbar) → confirm org → reveal **create-prompt** → **Name** = the prompt's file name;
   **Description** optional. Create.
2. Open the prompt (`/prompts/<id>`) → **+ Add version**:
   - **Content** — paste the prompt text **verbatim** (it becomes the *system prompt*).
   - **Target model** — the model the app uses (**fidelity**). *Baseline on the app's real model or the
     result won't transfer.*
   - **Label** — optional annotation (e.g. `baseline`); the v-number is automatic.
   - **Add version.** Confirm the history row shows the right **Target model**.
> **daily-briefing:** Content = the `daily-briefing.md` text; **Target model = Claude Haiku 4.5**;
> Label `baseline`. (Values in the Appendix.)

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
> **daily-briefing:** dataset **`Core player scenarios`** / *"5 representative player profiles —
> improving, sparse/new, low-hcp steady, declining, minimal-data edge"*; paste F1–F5 (Appendix) into
> **Prompt input**, other two blank.
>
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
> **daily-briefing:** (1) **LlmJudge**, Judge **Sonnet 5**, rubric in Appendix; (2) **Regex** `[0-9]`.
> *(If the Regex config's pass/fail semantics on-screen aren't "pass when found," note it and adjust.)*

## Step 5 — Baseline run
**Run evaluation** card → **Prompt** = this prompt → **Version** = `v1 · <target model>` (confirm it's the
app's model) → **Run evaluation**. Lands on `/eval-runs/<id>`: per-fixture **output**, **latency/cost**,
one **score row per scorer**. **Record the baseline** — judge value per fixture + aggregate, and which
fixtures fail any guardrail. That's the number to beat.

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

## Appendix — `daily-briefing` worked-example values

**Prompt content (paste verbatim as v1):**
```
You are a knowledgeable golf companion delivering a brief, personalized daily dashboard message for a golfer who uses Cortex Golf to track their game.

## Your Task
Write exactly 2-3 sentences about what's happening in this player's game right now. Be specific — use their actual numbers.

## Tone
- Smart friend who follows their stats closely, not a chatbot or life coach
- Encouraging and honest in equal measure
- Conversational, no jargon without explanation

## Rules
- 75-100 words maximum
- Plain text only — no bullet points, no headers, no markdown formatting
- Use specific numbers from the data (don't be vague or generic)
- Focus on the single most relevant thing right now (recent round, trend, or upcoming opportunity)
- One concrete observation or micro-tip if the data supports it
- Never invent data or make assumptions beyond what's provided
```

**LlmJudge rubric:**
```
Score 0-1 how well this daily golf briefing follows its brief:
(1) 75-100 words, 2-3 sentences, plain text with NO markdown, bullets, or headers;
(2) cites the player's ACTUAL numbers from the input (handicap, scores, averages) — specific, not vague;
(3) tone is a smart, stat-literate friend — encouraging AND honest, no jargon or life-coaching;
(4) focuses on the single most relevant thing (recent round, trend, or opportunity), at most one micro-tip;
(5) invents NO data beyond the input.
Deduct for markdown, vagueness, generic filler, wrong or invented numbers, and wrong length.
```

**Fixtures F1–F5 (paste each into *Prompt input*):**

*F1 — improving mid-handicapper*
```
HANDICAP:
  Index: 14.2
  Eligible rounds: 15
STATS:
  Total rounds: 32
  18-hole avg: 89.4
  Best round: 82
RECENT ROUNDS (newest first):
  - 2026-07-12, Torrey Pines South, Score: 86, (+14)
  - 2026-07-05, Balboa Park, Score: 88, (+16)
  - 2026-06-28, Torrey Pines North, Score: 91, (+19)
  - 2026-06-21, Coronado, Score: 90, (+18)
TREND (last 8 rounds): Improving (+1.5 strokes)
```
*F2 — new player, sparse data*
```
HANDICAP:
  Index: 26.8
  Eligible rounds: 5
STATS:
  Total rounds: 6
  18-hole avg: 102.3
  Best round: 97
RECENT ROUNDS (newest first):
  - 2026-07-10, Mission Bay Par-3, Score: 99, (+27)
  - 2026-06-30, Mission Bay Par-3, Score: 104, (+32)
```
*F3 — low-handicap, steady*
```
HANDICAP:
  Index: 4.1
  Eligible rounds: 20
STATS:
  Total rounds: 118
  18-hole avg: 77.9
  Best round: 71
RECENT ROUNDS (newest first):
  - 2026-07-14, Riviera CC, Score: 76, (+5)
  - 2026-07-08, Riviera CC, Score: 78, (+7)
  - 2026-07-01, Rustic Canyon, Score: 77, (+5)
  - 2026-06-24, Rustic Canyon, Score: 79, (+7)
TREND (last 8 rounds): Steady (+0.2 strokes)
```
*F4 — declining, needs honest + encouraging*
```
HANDICAP:
  Index: 11.5
  Eligible rounds: 18
STATS:
  Total rounds: 64
  18-hole avg: 85.6
  Best round: 78
RECENT ROUNDS (newest first):
  - 2026-07-13, Pebble Beach, Score: 92, (+20)
  - 2026-07-06, Spyglass Hill, Score: 90, (+18)
  - 2026-06-29, Poppy Hills, Score: 89, (+17)
  - 2026-06-22, Pebble Beach, Score: 87, (+15)
TREND (last 8 rounds): Declining (-2.1 strokes)
```
*F5 — minimal data edge*
```
STATS:
  Total rounds: 3
  18-hole avg: 94.0
```
