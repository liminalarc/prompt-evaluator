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
- **LlmJudge** · Judge model **Sonnet 5** · Rubric:
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
- **v1 baseline** (haiku): _record judge avg + per-fixture, `[0-9]` fails_ →
- **Hypothesis:** _one thing to improve_ →
- **v2:** _what changed_ → _movement vs v1_ →
- **Backport:** _committed to Golf / declined + reason_ →
- **Learnings:** _what moved the needle_
