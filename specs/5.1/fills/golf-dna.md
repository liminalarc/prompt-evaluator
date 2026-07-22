# `golf-dna` (Cortex Golf) — LitmusAI walkthrough

> **Self-contained.** Every step + every value to paste is inline below — drive top to bottom, no
> cross-referencing. (Generic click reference, if ever needed: [../runbook.md](../runbook.md); study
> detail: [../catalog.md](../catalog.md).) All manual — the maintainer drives the UI by hand.
>
> **Dev:** `https://dytjmtfgmj.us-east-1.awsapprunner.com`
> **This prompt:** archetype classifier + 4 data-backed insight sentences → strict JSON.
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
Distill player stats into **one archetype (of 8)** + **4 insight sentences** (topStrength,
biggestOpportunity, signaturePattern, improvementTrajectory). Call site `GenerateGolfDnaHandler.cs`
(`RequestType="GolfDna"`, `MaxTokens=1024`).

**Input** (`BuildUserMessage`): labeled JSON sections joined by blank lines, in order — `PLAYER STATS:` ·
`HANDICAP:` · `TREND DATA:` · `PERSONAL BESTS:` (each present only if the aggregator returned it). Not
upstream-SLM output → test-case **Upstream SLM output = blank**. ⚠ Exact stat schema comes from an external
aggregator (not in-repo); the seeds here are **synthetic, plausible shapes** — anchor fidelity with ≥1 real
capture (Step 3).

**Output:** strict JSON, no prose/fences: `{archetype, topStrength, biggestOpportunity, signaturePattern,
improvementTrajectory}` (all strings).

**Hard rules (→ scorers):** (1) valid JSON, exactly those 5 string fields; (2) archetype ∈ the 8 names;
(3) topStrength & biggestOpportunity **begin with "Your"** + **cite exact numbers**; (4) each sentence
**< 120 chars**; (5) **no invented numbers** (general, not fabricated, when data is sparse); (6) no generic filler.

**Diagnose candidates (confirm against baseline, don't pre-judge):**
- **H-a** — the prompt contradicts itself: intro says "**three** insight sentences," Rules say "**All four**."
- **H-b** — the app's own golden sample violates the spec (`biggestOpportunity` doesn't begin with "Your") →
  suspect the model drops the "Your" prefix.
- **H-c** — sparse data (F5) is the fabrication hotspot (round-debrief learning #4).

---

## Step 2 — Register v1 (baseline)
1. **Prompts** (topbar) → confirm **Cortex Golf** → **`+ New prompt`**:
   - **`Prompt name in Cortex Golf`** = `golf-dna`
   - **`Description (optional)`** = `Player archetype + 4 insight sentences (JSON).`
   - → **`Create prompt`** (lands on `/prompts/<id>`).
2. **`Version history`** card → **`+ Add version`**:
   - **`Import content from a file`** → `C:\Development\code\Golfstat\golfstat\server\src\AiService\AiService.WebApi\Prompts\golf-dna.md`
     *(truest — zero transcription risk)*, **or** paste the block below into **`Content`** (Edit tab):

```markdown
You are an expert golf analyst creating a personalized "Golf DNA" profile for a player. Your job is to distill their statistical data into a clear archetype classification and three specific, data-backed insight sentences that read like a knowledgeable caddie describing their game.

## Archetypes

Classify the player into exactly one of these archetypes based on their relative strengths and weaknesses:

- **Power Player**: Exceptional driving distance/accuracy, scores primarily on ball-striking, putting is compensatory
- **Precision Iron Player**: Consistent approach play and GIR, gains strokes on ball-striking, less dependent on driver
- **Short Game Wizard**: Saves strokes through scrambling, chipping, and short putting despite missing more greens
- **Putting Machine**: Puts-per-round and make rates are exceptional — saves strokes wherever missed
- **Grinder**: Scores better than raw stats suggest — avoids blow-ups, manages course well, high scrambling rate
- **Streaky Player**: High variance in round-to-round scoring — elite rounds mixed with blow-up rounds, inconsistent
- **Steady Eddie**: Low variance, consistent ball-striking and scoring, rarely exceptional but rarely terrible
- **Work in Progress**: New to tracking, limited data patterns, general improvement trajectory visible

## Output Format

Respond with ONLY a valid JSON object in exactly this structure — no markdown, no explanation, no surrounding text:

{
  "archetype": "one of the eight archetype names above",
  "topStrength": "one specific sentence beginning with 'Your' that quantifies the player's biggest statistical strength with exact numbers from the data",
  "biggestOpportunity": "one specific sentence beginning with 'Your' that quantifies the most impactful statistical weakness with exact numbers from the data",
  "signaturePattern": "one specific sentence describing a distinctive pattern in their game (e.g. performance in weather conditions, at specific course types, in specific holes, or a streak pattern)",
  "improvementTrajectory": "one sentence about their improvement over time using trend data — include time period and magnitude if data permits"
}

## Rules

- Use only data that is actually provided. Do not invent statistics or numbers.
- All four sentences must be specific and data-backed — no generic statements like "your putting could improve."
- The topStrength and biggestOpportunity must reference specific numbers from the provided stats.
- If trend data is limited, write a general trajectory statement rather than fabricating numbers.
- signaturePattern and improvementTrajectory may be more general if specific data is sparse.
- Keep each sentence under 120 characters.
```

   - **`Target model`** = **`claude-sonnet-4-6`**
   - **`Label (optional description)`** = `baseline`
   - → **`Add version`**. Confirm the history row shows **Target model = claude-sonnet-4-6**.

---

## Step 3 — Dataset + test cases
> **Terminology:** the UI labels these **Test cases** (renamed 2026-07-22); the domain term is still *fixture*
> (`Fixture.Input`) — same thing. F1–F5 below are test cases.
1. Workspace → **`Datasets`** card → **`+ New dataset`**:
   - **`New dataset name`** = `Archetype coverage`
   - **`Description (optional)`** = `5 test cases spanning distinct archetypes + a sparse edge.`
   - → **`Add dataset`**. Open it (`/datasets/<id>`).
2. **`Test cases`** card → **`+ Add test case`**, once per test case below. For each: set **`Label`**,
   **`Origin`** = **`Synthetic (hand-written)`**, paste the block into **`Prompt input`**, leave
   **`Upstream SLM output`** + **`Expected output`** blank → **`Add test case`**.
   > ⚠ **Anchor fidelity:** capture ≥1 **real** `golf-dna` input from the running Golf app and add it as a
   > 6th test case with **`Origin` = `Captured (from real app traffic)`**. If not practical now, proceed
   > synthetic-only — flag it as a caveat on the backport decision (Step 9).

**F1 — power ball-striker** *(Label: `power ball-striker`)*
```
PLAYER STATS:
{"rounds":42,"averageScore":79.4,"drivingDistanceYds":289,"drivingAccuracyPct":61,"girPct":64,"puttsPerRound":31.8,"scramblingPct":48,"sandSavePct":40}

HANDICAP:
{"current":6.2,"trend":"steady"}

TREND DATA:
{"last10AvgScore":78.9,"prev10AvgScore":80.1,"girTrend":"+3pct/6mo"}

PERSONAL BESTS:
{"bestRound":71,"longestDriveYds":318}
```

**F2 — short-game/putting saver** *(Label: `short-game saver`)*
```
PLAYER STATS:
{"rounds":55,"averageScore":82.1,"drivingDistanceYds":242,"drivingAccuracyPct":58,"girPct":41,"puttsPerRound":28.4,"scramblingPct":67,"sandSavePct":58,"onePuttPct":44}

HANDICAP:
{"current":9.8,"trend":"improving"}

TREND DATA:
{"last10AvgScore":81.2,"prev10AvgScore":83.0,"puttsTrend":"-1.1/6mo"}

PERSONAL BESTS:
{"bestRound":74,"fewestPutts":25}
```

**F3 — steady mid-handicap** *(Label: `steady low-variance`)*
```
PLAYER STATS:
{"rounds":60,"averageScore":86.3,"scoreStdDev":2.1,"drivingDistanceYds":255,"drivingAccuracyPct":63,"girPct":50,"puttsPerRound":31.0,"scramblingPct":52}

HANDICAP:
{"current":13.1,"trend":"steady"}

TREND DATA:
{"last10AvgScore":86.1,"prev10AvgScore":86.6}

PERSONAL BESTS:
{"bestRound":81}
```

**F4 — high-variance streaky** *(Label: `streaky high-variance`)*
```
PLAYER STATS:
{"rounds":48,"averageScore":88.7,"scoreStdDev":7.4,"bestRound":76,"worstRound":103,"blowUpHolesPerRound":2.3,"girPct":47,"puttsPerRound":32.5}

HANDICAP:
{"current":15.4,"trend":"volatile"}

TREND DATA:
{"last10AvgScore":87.9,"prev10AvgScore":89.5,"scoreStdDevTrend":"flat"}

PERSONAL BESTS:
{"bestRound":76,"eaglesThisSeason":2}
```

**F5 — sparse edge, new tracker** *(Label: `sparse new-tracker`)* — the fabrication hotspot
```
PLAYER STATS:
{"rounds":3,"averageScore":94.0}

HANDICAP:
{"current":null,"trend":"insufficient-data"}
```
*(no TREND DATA / PERSONAL BESTS — mirrors the real "not enough data" path.)*

Confirm the row count (5, or 6 with a capture) and the `Origin` filter.

---

## Step 4 — Scorers (compose; every run applies the set)
Workspace/dataset → **`Scorers`** card → **`+ Add scorer`**, once per scorer.

**1. JsonSchema** — structural gate. **`Scorer`** = `JsonSchema`; paste into **`Config (required)`**:
```json
{"type":"object","required":["archetype","topStrength","biggestOpportunity","signaturePattern","improvementTrajectory"],"properties":{"archetype":{"type":"string","enum":["Power Player","Precision Iron Player","Short Game Wizard","Putting Machine","Grinder","Streaky Player","Steady Eddie","Work in Progress"]},"topStrength":{"type":"string"},"biggestOpportunity":{"type":"string"},"signaturePattern":{"type":"string"},"improvementTrajectory":{"type":"string"}},"additionalProperties":false}
```
→ **`Add scorer`**.

**2. LlmJudge** — primary quality. **`Scorer`** = `LlmJudge`; **`Judge model`** = **`Opus 4.8`** (stronger tier
than Sonnet 4.6 — never the same tier). Paste into **`Rubric`**:
```
Score 0.0–1.0 how well this Golf DNA profile follows its spec, given the player stats in the input.
Deduct for each:
- archetype clearly mismatches the stats (e.g. "Putting Machine" with poor putting numbers).
- topStrength or biggestOpportunity does NOT begin with "Your", or cites no specific number from the input.
- any sentence exceeds ~120 characters.
- any number/statistic that is NOT present in the provided input (fabrication) — heaviest penalty.
- generic, non-data-backed statements ("your short game could improve").
- signaturePattern/improvementTrajectory reasonable to be general ONLY when the input lacks that data;
  penalize inventing a trend/pattern when the data is absent (see the sparse test case).
Reward: correct archetype fit, four crisp data-tied sentences, honest handling of missing data.
Give a one-paragraph rationale citing which fields/inputs drove the score.
```
→ **`Add scorer`**.

**3. Regex** *(optional guardrail)* — "cites at least one number." **`Scorer`** = `Regex`; **`Config
(required)`** = paste **only** the raw regex (a stored `Config …` matches literal text — the round-debrief trap):
```
[0-9]
```
→ **`Add scorer`**.

> **Composite weight (2.9) — leave all at `1` for the baseline.** Each scorer carries a per-dataset
> **weight** feeding the weighted composite; default is equal. **Keep all three at `1`** for now (operator
> decision 2026-07-22). ⚠ Worth revisiting after we read results: the composite's intent (2.9) is that a
> high-signal scorer (**LlmJudge**) should outweigh a low-signal guardrail (**Regex**) and the structural gate
> (**JsonSchema**, pass/fail) — equal weights let a clean-JSON pass inflate the number. Note it; don't tune it
> until the baseline says it matters.

---

## Step 5 — Baseline run
- Workspace **`Run a version`** card (or the dataset's **`Run evaluation`** card):
  **`Version`** = `v1 · claude-sonnet-4-6` · **`Dataset`** = `Archetype coverage` → **`Run evaluation`**.
- Lands on `/eval-runs/<id>`. Expand each test-case row: output + latency/tokens/cost + a score per scorer.
- **Re-run F5 (sparse) 2–3×** — one run lies (round-debrief R4).
- **Record the baseline here** (the number to beat):

| Test case | JsonSchema | LlmJudge | Regex | Notes |
|---|---|---|---|---|
| F1 power | **0** | 0.82 | 1 | **JSON schema FAIL** — judge still likes content |
| F2 short-game | **0** | 0.88 | 1 | **JSON schema FAIL** |
| F3 steady | 1 | 0.88 | 1 | clean |
| F4 streaky | 1 | 0.95 | 1 | clean; best content |
| F5 sparse | 1 | 0.87 | 1 | clean — no fabrication observed |
| **aggregate** | **3/5 pass** | **0.88 mean** | 5/5 | equal-weight composite ≈ 0.83 (JSON fails drag F1/F2 to ~0.61/0.63) |

**Baseline read (v1, Sonnet 4.6, 2026-07-22):** content quality is high everywhere (judge 0.82–0.95), but
**2 of 5 outputs fail strict JSON schema** (F1 power, F2 short-game) while the other three pass. Regex (cites
a number) passes 5/5. **The defect is structural, not qualitative** — a "respond with ONLY JSON" prompt whose
output isn't reliably parseable ~40% of the time. The source app hides this with `ExtractJsonObject` (its own
comment: *"Claude esp. Sonnet often wraps JSON in ```json fences or adds a sentence of preamble despite the
'ONLY JSON' instruction"*) — strong prior that F1/F2 are **fence/preamble-wrapped**, but confirm from the
output before writing v2.

---

## Step 6 — Diagnose — CONFIRMED
**Hypothesis (confirmed):** F1/F2 fail JsonSchema because the output is **wrapped in a ```` ```json ```` code
fence** — the scorer's parse error is `'`' is an invalid start of a value … BytePositionInLine: 0` (a backtick
at byte 0). Not a content/field/enum problem (H-a/H-b/H-c are **not** the driver). Nondeterministic: 3/5 came
back as bare JSON, 2/5 fenced. The prompt's *"ONLY … no markdown"* instruction is too weak for Sonnet 4.6.
**v2 target: eliminate the code fence — force a bare `{`-to-`}` response.**

## Step 7 — Improve → v2
**`Version history`** → **`+ Add version`**: Content pre-seeds from v1 — make the **single** edit below;
**`Target model` = `claude-sonnet-4-6` (SAME)**; **`Label`** = `no code fences: response starts with {`.
→ **`Add version`**.

**The edit** — replace the first line of the `## Output Format` section:

- **Old:**
  > Respond with ONLY a valid JSON object in exactly this structure — no markdown, no explanation, no surrounding text:
- **New:**
  > Respond with ONLY a valid JSON object in exactly this structure. Do **not** wrap it in markdown code fences — no ```` ```json ````, no ```` ``` ````. Do not add any text, explanation, or preamble before or after it. Your entire response must start with the character `{` and end with the character `}`.

Everything else stays byte-for-byte identical (single-variable change, per R4/R5 — isolate the prompt effect).
> Deliberately **not** touching the "three vs four" intro (H-a) here — the baseline showed no field-count
> harm, so folding it in would muddy the comparison. Candidate for a later cosmetic v3 if wanted.

## Step 8 — Compare
**Analytics** (topbar) → **`Prompt`** = `golf-dna` · **`Dataset`** = `Archetype coverage` (threshold 0.05).
Read **`Score trend`** (per-scorer + **weighted composite**), **`Regressions`**, **`Compare versions`**
(From/To per-test-case deltas). Iterate v3+ on the same dataset+scorers until satisfied.
> **Runs are append-only — a re-run never replaces the old one.** Trend/Compare/regression/backport plot each
> version's **latest** run only; the **Variance view** (2.14) aggregates **all** runs of a version → use it to
> read the stochastic JSON-fence rate across the repeated v2 runs (JsonSchema steady at 1.0 vs wobbling?).

### v2 runs (JsonSchema is the metric; LlmJudge should hold ~0.88)
| run | F1 power | F2 short-game | F3 steady | F4 streaky | F5 sparse | judge mean |
|---|---|---|---|---|---|---|
| 1 | JS **1** / J 0.82 | JS **1** / J 0.88 | JS 1 / J 0.95 | JS 1 / J 0.88 | JS 1 / J 0.82 | 0.87 |
| 2 | JS **1** / J 0.82 | JS **1** / J 0.82 | JS 1 / J 0.97 | JS 1 / J 0.93 | JS 1 / J 0.85 | 0.88 |
| 3 | JS **1** / J 0.88 | JS **1** / J 0.93 | JS 1 / J 0.97 | JS 1 / J 0.92 | JS 1 / J 0.87 | 0.91 |

**Verdict — v2 wins (clean, low-risk).** JsonSchema **3/5 → 5/5 across all 3 runs** (0/15 fenced vs v1's
fence defect); LlmJudge flat-to-slightly-up (v1 0.88 → v2 0.87/0.88/0.91, within noise). Single-variable change
(output-format fence ban), subject model held at Sonnet 4.6 → no R9 cross-model confound. Equal-weight composite
≈ 0.83 → ≈ 0.96, driven by the JSON gate. **→ backport v2.**

## Step 9 — Backport (tool-native)
1. **`Versions`** tab → **`Deployment`** card → **`Set as current in source`** on the version Golf runs today (v1).
2. Read the **`Backport target`** badge (highest composite above Current, same-scorer-config). No badge = nothing
   beats Current → decline + log.
3. **`Prepare backport`** → **`Copy exact prompt`** or **`Download markdown`** → save the drop-in to
   **`../backport/golf-dna.md`**; note best-version + evidence in **`../backport/README.md`**.
4. A **source-repo agent** applies it to `server/src/AiService/AiService.WebApi/Prompts/golf-dna.md` — or declines
   with a recorded reason (marginal gain / model-specific / cost). **⚠ Confirm on the real model (Sonnet 4.6) +
   read rationales, not just the number (R4/R5/R7).** If synthetic-only test cases, flag lower fidelity here.
5. Once shipped → Deployment card → **`Mark backported → vN`** (moves the Current marker; the in-tool record of live).

**Decision — ✅ backported v2 (2026-07-22).** In-tool: Set current → v1 → target badge **v2** (both Sonnet 4.6,
no R9 confound) → `Prepare backport` (artifact matched the staged drop-in byte-for-byte **except a missing EOF
newline** → finding U22; used the newline-correct [../backport/golf-dna.md](../backport/golf-dna.md)) → a
source-repo agent (user-run, separate) applied the one-line change to Golf's `Prompts/golf-dna.md` → **`Mark
backported → v2`** by hand. Golf commit SHA not tracked in-tool (D2). Drop-in: [../backport/golf-dna.md](../backport/golf-dna.md).

## Step 10 — Log
- **Learning:** the win was **structural, not qualitative** — a "respond with ONLY JSON" prompt that
  Sonnet 4.6 fenced ~40% of the time; the content was already good (judge ~0.88 throughout). Diagnosis came
  from the **JsonSchema parse error** (`'`' … BytePositionInLine: 0`), invisible to the LLM-judge (it reads
  through fences) — a case where the **deterministic scorer caught what the judge couldn't**. Fix was one
  hardened output-format line. Confirmed across 3 runs (fence behavior is stochastic — the F4 motivation).
