# `hole-notes` (Cortex Golf) — LitmusAI walkthrough

> **Self-contained.** Every step + every value to paste is inline below — drive top to bottom, no
> cross-referencing. (Generic click reference, if ever needed: [../runbook.md](../runbook.md); study
> detail: [../catalog.md](../catalog.md).) All manual — the maintainer drives the UI by hand.
>
> **Dev:** `https://dytjmtfgmj.us-east-1.awsapprunner.com`
> **This prompt:** a caddie writes 2-3 short, data-grounded notes on one player's tendencies on one hole → JSON array of strings.
> **Subject model:** `claude-sonnet-4-6` (Golf's real model — baseline on it or the result won't transfer).
> **Reminder:** a Version is immutable (content + target model); add a new version to change either. A
> Dataset is the fixed yardstick you re-run every version against — name it for coverage, never a version.

---

## Step 0 — Session setup (once)
1. Log in.
2. Topbar → **`Org` switcher** (right side, left of your name) → **`Cortex Golf`**. Persists via `?org=` +
   localStorage; set it once. Confirm it before every create below.

---

## Step 1 — Study (done — read-only, for context)
Write **2-3 short caddie-voice notes** on THIS player's tendencies on THIS hole, grounded strictly in the
player's own data. Call site `GenerateHoleNotesHandler.cs` (`RequestType="HoleNotes"`, `MaxTokens=512`).

**Input** (`BuildUserMessage`) — a labeled text block, **not** upstream-SLM output → test-case
**Upstream SLM output = blank**:
```
HOLE: <n>
ROUNDS PLAYED ON THIS HOLE: <n>

SCORING HISTORY (this hole):
<HoleAnalysis JSON — avg vs par, distribution, best/worst>

SHOT DATA (this hole):                ← ONLY when shot data exists
<Shots JSON — club, lie, result, distance>
```
…and when there is **no** shot data, that last block is replaced by the literal line:
`No shot-level data for this hole - base notes on the scoring history only.`

⚠ The exact `HoleAnalysis` / `Shots` JSON schema comes from `IHoleNoteContextProvider` (not in the prompt
repo) — the seeds below are **synthetic, plausible shapes**; anchor fidelity with ≥1 real capture (Step 3).

**Output:** a JSON array of 2-3 strings, no prose/fences: `["note one", "note two"]`.

**Two things that shape the eval:**
1. **Data-conditional** — a hard branch between *shot-data-present* and *score-history-only*, with an explicit
   rule: with only scores, **don't invent** clubs/trouble-spots. Fixtures MUST cover **both branches** (the
   score-only branch is where hallucination shows — round-debrief learning #4).
2. **The app parser is fence-tolerant** (`ParseNotes` grabs everything between the first `[` and last `]`), so
   fences don't break the *app* — but LitmusAI's **JsonSchema** scorer parses raw, so it still (correctly)
   enforces the prompt's own "no code fences" contract. Keep it strict; it's a free structural gate.

**Hard rules (→ scorers):** (1) JSON array of **2-3 strings**; (2) each note **one sentence, < 130 chars**;
(3) **no em dash** (hyphens only); (4) speaks to the player ("you"); (5) **no invented numbers/clubs/trouble
spots** — score-only inputs especially; (6) no generic golf advice.

**Preconditions in the app:** needs **≥ 3 rounds** on the hole (`MinRounds`), 7-day cache.

**Diagnose candidates (confirm against baseline, don't pre-judge):**
- **H-a** — **invention on the score-only branch**: notes cite clubs/distances/trouble spots absent from the
  input (the prompt's headline risk; the fabrication hotspot).
- **H-b** — a note exceeds 130 chars.
- **H-c** — em dash used despite the explicit ban.
- **H-d** — generic advice not tied to this player's pattern.
- **H-e** — wrong count (1 or 4 notes) or multi-sentence notes.

---

## Step 2 — Register v1 (baseline)
1. **Prompts** (topbar) → confirm **Cortex Golf** → **`+ New prompt`**:
   - **`Prompt name in Cortex Golf`** = `hole-notes`
   - **`Description (optional)`** = `Caddie-voice per-hole tendency notes (JSON array of strings).`
   - → **`Create prompt`** (lands on `/prompts/<id>`).
2. **`Version history`** card → **`+ Add version`**:
   - **`Import content from a file`** → `C:\Development\code\Golfstat\golfstat\server\src\AiService\AiService.WebApi\Prompts\hole-notes.md`
     *(truest — zero transcription risk)*, **or** paste the block below into **`Content`** (Edit tab):

```markdown
You are a seasoned local caddie who knows this player's game on this specific hole. You will receive the player's own history on one hole at one course: their scoring record (average vs par, distribution, best/worst) and, when available, their shot-by-shot data (club, lie, result, distance).

Your job is to write 2-3 short, punchy notes — in a caddie's voice — about THIS player's tendencies on THIS hole, and the smart play based on their own data.

## Voice and content

- Speak directly to the player ("you"), like a caddie on the tee.
- Each note is one sentence, specific, and grounded in the data provided.
- Cover the things a caddie would say: typical score, go-to club and the distance it leaves, where trouble has happened, and the pattern behind good vs bad results.
- If only score history is available (no shot data), base the notes on scoring patterns alone — don't invent club or trouble-spot detail.

Examples of the right flavor:
- "You average bogey here (4.8 on a par 4) - three of your five rounds ended in the right rough."
- "Your go-to tee club is the 3-wood, averaging 198 yards, which leaves you about 145 in."
- "You've made par or better only when your approach finishes left of the pin - the right side feeds to the bunker."

## Output format

Respond with ONLY a JSON array of 2-3 strings — no markdown, no prose, no code fences:

["note one", "note two", "note three"]

## Rules

- 2 to 3 notes. One sentence each. Under 130 characters each.
- Use only the data provided. Never invent numbers, clubs, or trouble spots.
- No generic golf advice — every note must reflect this player's own pattern on this hole.
- Do not use an em dash; use a hyphen.
```

   - **`Target model`** = **`claude-sonnet-4-6`**
   - **`Label (optional description)`** = `baseline`
   - → **`Add version`**. Confirm the history row shows **Target model = claude-sonnet-4-6**.

> Note: the prompt's own examples contain an em dash ("...caddie's voice —") in the instructions while the
> Rules forbid em dashes in the *output*. Not a v1 edit — just a diagnose candidate if the model mirrors the
> instruction's dash into its notes (H-c).

---

## Step 3 — Dataset + test cases
> **Terminology:** the UI labels these **Test cases**; the domain term is still *fixture* (`Fixture.Input`).
1. Workspace → **`Datasets`** card → **`+ New dataset`**:
   - **`New dataset name`** = `Hole coverage`
   - **`Description (optional)`** = `5 test cases: both data branches (score-only + shot-data) with a sparse edge.`
   - → **`Add dataset`**. Open it (`/datasets/<id>`).
2. **`Test cases`** card → **`+ Add test case`**, once per test case below. For each: set **`Label`**,
   leave **`Origin`** at its default **`Synthetic (hand-written)`** (2.23 U18), paste the block into
   **`Prompt input`**, leave **`Upstream SLM output`** + **`Expected output`** blank → **`Add test case`**.
   > ⚠ **Anchor fidelity:** capture ≥1 **real** `hole-notes` input from the running Golf app (a player with
   > ≥3 rounds on a hole) and add it as a 6th test case with **`Origin` = `Captured (from real app traffic)`**.
   > If not practical now, proceed synthetic-only — flag it as a caveat on the backport decision (Step 9).

**HN1 — score-only, sparse (exactly 3 rounds)** *(Label: `score-only sparse`)* — the fabrication hotspot
```
HOLE: 12
ROUNDS PLAYED ON THIS HOLE: 3

SCORING HISTORY (this hole):
{"par":4,"averageScore":5.0,"distribution":{"par":1,"bogey":1,"double":1},"best":4,"worst":6}

No shot-level data for this hole - base notes on the scoring history only.
```

**HN2 — score-only, richer history** *(Label: `score-only rich`)* — still no shot data
```
HOLE: 5
ROUNDS PLAYED ON THIS HOLE: 8

SCORING HISTORY (this hole):
{"par":3,"averageScore":3.4,"distribution":{"birdie":1,"par":4,"bogey":2,"double":1},"best":2,"worst":5,"scoreStdDev":0.8}

No shot-level data for this hole - base notes on the scoring history only.
```

**HN3 — score + shot data, typical par 4** *(Label: `shot-data typical`)*
```
HOLE: 7
ROUNDS PLAYED ON THIS HOLE: 6

SCORING HISTORY (this hole):
{"par":4,"averageScore":4.5,"distribution":{"par":3,"bogey":2,"double":1},"best":3,"worst":6}

SHOT DATA (this hole):
{"rounds":[{"tee":{"club":"3-wood","result":"fairway","distanceYds":198},"approach":{"club":"8-iron","result":"green","distanceYds":150},"score":4},{"tee":{"club":"3-wood","result":"right-rough","distanceYds":205},"approach":{"club":"7-iron","result":"green-side-bunker","distanceYds":160},"score":5},{"tee":{"club":"driver","result":"fairway","distanceYds":250},"approach":{"club":"pitching-wedge","result":"green","distanceYds":120},"score":3}]}
```

**HN4 — score + shot data, clear trouble pattern** *(Label: `shot-data trouble-pattern`)*
```
HOLE: 15
ROUNDS PLAYED ON THIS HOLE: 7

SCORING HISTORY (this hole):
{"par":4,"averageScore":4.9,"distribution":{"par":2,"bogey":3,"double":2},"best":4,"worst":7}

SHOT DATA (this hole):
{"rounds":[{"tee":{"club":"driver","result":"right-water","distanceYds":240},"score":6},{"tee":{"club":"driver","result":"right-rough","distanceYds":255},"score":5},{"tee":{"club":"3-wood","result":"fairway","distanceYds":220},"approach":{"club":"9-iron","result":"green","distanceYds":135},"score":4},{"tee":{"club":"driver","result":"right-water","distanceYds":248},"score":7}]}
```
*(driver misses right into water; 3-wood keeps it dry — the pattern a caddie should surface.)*

**HN5 — score + shot data, strong (birdie) hole** *(Label: `shot-data strength`)*
```
HOLE: 2
ROUNDS PLAYED ON THIS HOLE: 6

SCORING HISTORY (this hole):
{"par":5,"averageScore":4.7,"distribution":{"eagle":1,"birdie":3,"par":2},"best":3,"worst":5}

SHOT DATA (this hole):
{"rounds":[{"tee":{"club":"driver","result":"fairway","distanceYds":268},"layup":{"club":"5-iron","result":"fairway","distanceYds":190},"approach":{"club":"gap-wedge","result":"green","distanceYds":95},"score":4},{"tee":{"club":"driver","result":"fairway","distanceYds":275},"approach":{"club":"3-wood","result":"green","distanceYds":230},"score":3}]}
```

Confirm the row count (5, or 6 with a capture) and that the `Origin` filter shows them.

---

## Step 4 — Scorers (compose; every run applies the set)
Workspace/dataset → **`Scorers`** card → **`+ Add scorer`**, once per scorer.

**1. JsonSchema** — structural gate: a bare JSON array of 2-3 strings, each ≤130 chars. **`Scorer`** =
`JsonSchema`; paste into **`Config (required)`**:
```json
{"type":"array","minItems":2,"maxItems":3,"items":{"type":"string","maxLength":130}}
```
→ **`Add scorer`**. *(A fenced or preamble-wrapped output fails to parse here — that's the point: it enforces
the prompt's "ONLY a JSON array, no code fences" contract even though the Golf app itself is lenient.)*

**2. LlmJudge** — primary quality, **data-conditional**. **`Scorer`** = `LlmJudge`; **`Judge model`** =
**`Opus 4.8`** (stronger tier than Sonnet 4.6 — never the same tier). Paste into **`Rubric`**:
```
Score 0.0–1.0 how well these caddie notes follow the spec, GIVEN the player data in the input.
The input has a HOLE header, a SCORING HISTORY block, and EITHER a SHOT DATA block OR the line
"No shot-level data for this hole". Judge against what is actually present.
Deduct for each:
- any number, club, distance, or trouble spot NOT present in the input (fabrication) — heaviest penalty,
  and heavier still on a score-only input (where the spec explicitly forbids inventing club/trouble detail).
- a note that is not grounded in THIS player's data on THIS hole (generic golf advice).
- more than 3 or fewer than 2 notes; a note longer than one sentence or over ~130 characters.
- a note that does not speak to the player ("you"); an em dash anywhere (hyphens only).
Reward: a caddie's voice, notes tied to the player's own numbers/patterns (typical score, go-to club and
the distance it leaves, where trouble happened, good-vs-bad pattern), and honest restraint on score-only data.
Give a one-paragraph rationale citing which input fields drove the score.
```
→ **`Add scorer`**.

**3. Regex** *(optional guardrail)* — "addresses the player." **`Scorer`** = `Regex`; **`Config (required)`**
= paste **only** the raw regex (a stored `Config …` matches literal text — the round-debrief trap):
```
(?i)\byou(r)?\b
```
→ **`Add scorer`**.

> **Composite weight (2.9) — leave all at `1` for the baseline** (operator decision, matches golf-dna).
> Revisit only if results say the equal weighting lets a clean-structure pass mask a fabrication the judge
> caught. Note it; don't tune it pre-baseline.
> **Gap to watch:** there's no deterministic "no em dash" check (the Regex scorer passes on *match*, not on
> absence) — the judge rubric carries the em-dash rule. If em dashes turn out to be a real, frequent defect,
> that's a candidate finding (a negative/absence Regex mode).

---

## Step 5 — Baseline run (record here)
- Workspace **`Run a version`** card (or the dataset's **`Run evaluation`** card):
  **`Version`** = `v1 · claude-sonnet-4-6` · **`Dataset`** = `Hole coverage` → **`Run evaluation`**.
- Lands on `/eval-runs/<id>`. Expand each row: output + latency/tokens/cost + a score per scorer.
- **Re-run the score-only fixtures (HN1, HN2) 2–3×** — fabrication is stochastic (round-debrief R4).
- **Record the baseline (the number to beat):**

| Test case | JsonSchema | LlmJudge | Regex | Notes |
|---|---|---|---|---|
| HN1 score-only sparse |  |  |  |  |
| HN2 score-only rich |  |  |  |  |
| HN3 shot-data typical |  |  |  |  |
| HN4 shot-data trouble |  |  |  |  |
| HN5 shot-data strength |  |  |  |  |
| **aggregate** |  |  |  |  |

**Baseline read (fill in):** what's the defect — structural (JSON/count/length) or qualitative (fabrication
on the score-only branch / generic notes / em dash)? Cite the actual outputs before writing v2.

---

## Step 6 — Diagnose (fill in — confirm one hypothesis)
Pick the dominant failure from Step 5 (candidates H-a…H-e from Step 1). Name the single change v2 will make.

## Step 7 — Improve → v2 (fill in)
**`Version history`** → **`+ Add version`** (content pre-seeds from v1). Make the **single** targeted edit,
**`Target model` = `claude-sonnet-4-6` (SAME)**, **`Label`** = the change. Isolate one variable (R4/R5).
Likely edits by hypothesis: tighten the "use only data provided" rule + an explicit score-only guard (H-a);
restate the count/length as a hard cap the model echoes (H-b/H-e); drop the em dash from the *instruction's*
own prose so the model stops mirroring it (H-c).

## Step 8 — Compare (fill in)
**Analytics** → **`Prompt`** = `hole-notes` · **`Dataset`** = `Hole coverage`. Read **`Score trend`**
(per-scorer + weighted composite), **`Regressions`**, **`Compare versions`** (From/To per-test-case deltas —
the To picker now shows the right version, U23). Use the **Variance view** (2.14) to read the fabrication rate
across the repeated score-only runs. Iterate v3+ on the same dataset+scorers until satisfied.

## Step 9 — Backport (tool-native, fill in)
1. **`Versions`** tab → **`Deployment`** card → **`Set as current in source`** on the version Golf runs today (v1).
2. Read the **`Backport target`** badge (highest composite above Current, same-scorer-config, subject-model-held).
   No badge = nothing beats Current → decline + log.
3. **`Prepare backport`** → **`Copy exact prompt`** / **`Download markdown`** → save the drop-in to
   **`../backport/hole-notes.md`**; note best-version + evidence in **`../backport/README.md`**.
   > ⚠ Copy-exact omits the trailing newline (finding U22) — use the staged `../backport/hole-notes.md`
   > (newline-correct) for the actual source edit.
4. A **source-repo agent** applies it to `server/src/AiService/AiService.WebApi/Prompts/hole-notes.md` — or
   declines with a recorded reason. **⚠ Confirm on Sonnet 4.6 + read rationales, not just the number.** If
   synthetic-only test cases, flag lower fidelity here.
5. Once shipped → Deployment card → **`Mark backported → vN`** (moves the Current marker).

## Step 10 — Log (fill in)
One line of learnings: what moved the needle (structural vs qualitative), and whether the deterministic gate or
the judge caught the real defect.
