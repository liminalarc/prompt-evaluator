# Fill sheet — `daily-briefing` (Cortex Golf)

> Every form value in UI order for a full playbook walk. Follow the click-by-click steps in
> [../runbook.md](../runbook.md); paste these values. Study detail: [../catalog.md](../catalog.md).
> Subject model **`claude-haiku-4-5-20251001`** (the model Golf runs it on) → Target model **Haiku 4.5**.

## ② Prompt
- **Name:** `daily-briefing`
- **Description:** `Cortex Golf — 2-3 sentence personalized daily dashboard blurb from player stats`

## ② Version v1
- **Target model:** **Claude Haiku 4.5**
- **Label:** `baseline`
- **Content** (paste verbatim — becomes the system prompt):
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

## ③ Dataset
- **Name:** `Core player scenarios`
- **Description:** `5 representative player profiles — improving, sparse/new, low-hcp steady, declining, minimal-data edge`

## ③ Fixtures
Paste each block into **Prompt input**; leave **Upstream SLM output** and **Expected output** blank.

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

## ④ Scorers
- **LlmJudge** · Judge model **Sonnet 5** (or Opus 4.8) — both fine once the B6 eval-runner fix is
  live (judge now budgets for thinking-on-by-default models) · Rubric:
```
Score 0-1 how well this daily golf briefing follows its brief:
(1) 75-100 words, 2-3 sentences, plain text with NO markdown, bullets, or headers;
(2) cites the player's ACTUAL numbers from the input (handicap, scores, averages) — specific, not vague;
(3) tone is a smart, stat-literate friend — encouraging AND honest, no jargon or life-coaching;
(4) focuses on the single most relevant thing (recent round, trend, or opportunity), at most one micro-tip;
(5) invents NO data beyond the input.
Deduct for markdown, vagueness, generic filler, wrong or invented numbers, and wrong length.
```
- **Regex** · Config `[0-9]` (cites a number)

## Baseline & iterations (fill as you run)
- **✅ RESOLVED (2026-07-18)** — dev Anthropic key was a placeholder; set it + rolled eval-runner.
  Subject execution (Haiku) now works.
- **⏸ PAUSED at ⑤ (2026-07-18)** — 2nd 500: the **LlmJudge** step, not the key. Judge model **Sonnet 5**
  runs adaptive thinking by default → truncated the structured verdict JSON → eval-runner 500 (finding
  B6). **Fixed** in eval-runner (`JUDGE_MAX_TOKENS`, model-agnostic budget); deploying via CI. Your saved
  Sonnet-5 scorer is fine as-is — just **re-run** once dev redeploys (no dataset recreate needed).
- **v1 baseline** (haiku, judge Sonnet 5): avg **0.55**, **1/5 pass** (F4=0.80; F3=0.62, F2=0.55,
  F5=0.45, F1=0.35). Regex 5/5 pass. Dominant miss = **word count**: 4/5 under 75 (F1 62, F5 55, F2 49,
  F3 69; only F4 88). Secondary: **computed/invented stats** (F1 wrong 4-round avg 86.3 vs 88.75; F3
  speculative) + **vague micro-tips**. (Also surfaced redactor bug B7.)
- **Hypothesis:** "75-100 **maximum**" reads as a ceiling → undershoot; model fills/reasons via
  computed or vague filler. → v2: firm 75-100 target reached by **citing more real numbers**, and
  **ban computing new stats**; micro-tip must be data-tied.
- **v2:** firm length + no-computed-stats + data-tied tip → **avg 0.88 (was 0.55, +0.33), 5/5 pass
  (was 1/5)**. Fixed undershoot (all in 75-100) + invented stats (F1 0.35→0.90). Residue: slightly
  generic micro-tips (the 0.85s). Cost/call ~3-5× (longer prompt + fuller output) — negligible for a
  once-daily 24h-cached call. Analytics: _pending user check_.
- **Backport:** ✅ **decision made (2026-07-18)** — v2 is the winner (firm length + ban computed stats;
  clear quality win, cost bump immaterial for a once-daily 24h-cached call). The direct Golf commit
  `660a3ff2` was **reverted (2026-07-19, new process)** — the drop-in [../backport/daily-briefing.md](../backport/daily-briefing.md)
  is the record for the source-repo agent.
  - **✅ In-tool backport DONE (2026-07-20).** Re-formalized through the tool-native flow: Set Current = v1
    → target badge = v2 → **Prepare backport** (the 1.20 artifact reproduced this drop-in byte-for-byte —
    clean dogfood pass) → a **source-repo agent applied v2 to Golf** in a clean one-file commit **`abd385f8`**
    → **Mark backported → v2**. The commit SHA is **not** recorded in-tool (no UI input; deliberate — finding
    **D2**: SHA goes stale, git history + this ledger is the provenance, auto-capture waits for 3.1).
- **Learnings:** "N words **maximum**" reads as a ceiling → models undershoot; reframe to a firm target
  reached by **citing more real data**, and **explicitly ban computing new stats** (models will fabricate
  derived numbers to fill space). Likely applies to other narrative prompts (golf-dna, hole-notes,
  round-debrief, stat-narratives).
