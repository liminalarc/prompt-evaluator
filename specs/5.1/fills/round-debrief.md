# Fill sheet — `round-debrief` (Cortex Golf)

> Every form value in UI order for a full playbook walk. Follow the click-by-click steps in
> [../runbook.md](../runbook.md); paste these values. Study detail: [../catalog.md](../catalog.md).
> Subject model **`claude-sonnet-4-6`** (Golf's default via `ClaudeOptions.Model`) — **see model-fidelity note below**.

## Model fidelity — subject = Sonnet 4.6 (now seeded)
Golf runs round-debrief on **Claude Sonnet 4.6**. It's now in the catalog as of **v0.15.0** ([1.19](../../archive/1.19.md)),
so just **select `Claude Sonnet 4.6` as the Target model** in ② — exact fidelity, no admin step. (Confirm dev is
serving 0.15.0 first: `GET /api/version`.)

**Judge = Opus 4.8.** Rule: judge must be a *stronger tier*, never the same tier — Sonnet 4.6 and Sonnet 5 are the
**same** tier, so Sonnet-5-as-judge is disqualified here (unlike daily-briefing's Haiku subject). Confirmed via the
`claude-api` skill.

## ② Prompt
- **Name:** `round-debrief`
- **Description:** `Cortex Golf — post-round coaching narrative (200-400 words) + a [DRILLS_JSON] block of 2-3 data-tied drills`

## ② Version v1
- **Target model:** **Claude Sonnet 4.6** (option A) or **Claude Sonnet 5** (option B)
- **Label:** `baseline`
- **Content** (paste verbatim — becomes the system prompt):
```
You are a friendly, encouraging golf coach providing a post-round analysis for a recreational golfer. You will receive structured data about a round of golf along with the player's career statistics.

## Your Analysis Should

1. **Open with the strongest positive** from the round — a great hole, a personal achievement, or an area of clear improvement.
2. **Analyze front nine vs back nine momentum** — note any momentum shifts, strong/weak stretches, or consistency patterns.
3. **Identify 2-3 specific patterns** in the data — these could be scoring patterns by par type, putting performance, GIR trends, shot patterns, or pace observations.
4. **Compare to the player's own averages** — always reference their personal stats, not generic benchmarks. "You averaged 1.8 putts per hole vs your career 2.1" is far more useful than "tour average is 1.7".
5. **Close with one actionable practice suggestion** — based on the most impactful weakness you identified. Be specific: "Focus on approach shots from 150-175 yards" not "work on your iron play".

## Tone

- Encouraging but honest — celebrate improvements, acknowledge struggles without dwelling
- Coach-like, not commentator-like — "you" not "the player"
- Conversational, 3-5 paragraphs
- No bullet points or headers — write in flowing paragraphs
- Use specific numbers from the data to support observations

## Important Rules

- Only reference data that is actually provided. Do not invent statistics.
- If limited data is available, focus on what you have rather than noting what's missing.
- Do not mention handicap projections or score predictions.
- Do not use golf jargon without context — assume the player knows basic terms but explain advanced concepts briefly.
- Keep the response between 200-400 words.
- Do not use markdown formatting (no headers, bold, italics, or bullet points). Write in plain paragraphs.

## Drill Prescriptions

After your written analysis, append a drills block with 2-3 targeted practice drills that address the most impactful weaknesses you identified in the round. The drills should be specific, actionable, and directly tied to the data.

Format the drills block exactly as follows — do not deviate from this format:

[DRILLS_JSON]
[
  {
    "area": "short description of weakness area (e.g. '6-10 foot putting', 'approach from 125-150 yards')",
    "drillName": "specific drill name",
    "description": "clear, step-by-step description of how to execute the drill at the range or practice green",
    "targetRounds": 3,
    "metric": "camelCase metric key (e.g. puttMakeRate6to10ft, avgProximity125to150, girPercent, fairwayPercent)",
    "targetValue": 0.5
  }
]
[/DRILLS_JSON]

Rules for drills:
- Write exactly 2-3 drills.
- Each drill must address a specific, data-backed weakness from this round.
- targetRounds is an integer between 2 and 6.
- targetValue is a double representing the goal (e.g. 0.5 for 50% make rate, 25 for 25 yards proximity).
- The [DRILLS_JSON] block must appear after your written analysis text, separated by a blank line.
- The written analysis must stand alone without the drills — do not reference the drills in the text.
```

## ③ Dataset
- **Name:** `Core round scenarios`
- **Description:** `4 representative post-round profiles — front/back split, putting-limited, low-hcp steady, sparse edge`

## ③ Fixtures
The app builds the user turn as markdown sections (`## Round Scorecard {json}`, `## Career Statistics {json}`,
`## Handicap {json}`, then shots/pace/hole-analysis if budget allows — `DebriefPromptBuilder`). Paste each block
into **Prompt input**; **Origin = Synthetic (hand-written)** (representative, not captured); leave Upstream/Expected blank.

*F1 — strong front, back-nine blow-up*
```
## Round Scorecard
{
  "course": "Torrey Pines South", "date": "2026-07-15", "tees": "Blue", "totalScore": 88, "par": 72,
  "front9": { "score": 40, "par": 36 }, "back9": { "score": 48, "par": 36 },
  "holes": [
    {"hole":1,"par":4,"score":4,"putts":2,"gir":true,"fairway":true},
    {"hole":2,"par":5,"score":5,"putts":2,"gir":true,"fairway":true},
    {"hole":3,"par":3,"score":3,"putts":2,"gir":true,"fairway":null},
    {"hole":7,"par":4,"score":5,"putts":2,"gir":false,"fairway":false},
    {"hole":10,"par":4,"score":7,"putts":3,"gir":false,"fairway":false},
    {"hole":12,"par":3,"score":5,"putts":3,"gir":false,"fairway":null},
    {"hole":15,"par":5,"score":8,"putts":2,"gir":false,"fairway":false},
    {"hole":18,"par":4,"score":6,"putts":2,"gir":false,"fairway":false}
  ],
  "totals": { "putts": 34, "girPercent": 0.39, "fairwayPercent": 0.43 }
}

## Career Statistics
{
  "totalRounds": 64, "avg18": 86.2, "bestRound": 78,
  "career": { "puttsPerHole": 1.95, "girPercent": 0.44, "fairwayPercent": 0.52,
    "scoringByPar": { "par3": 3.6, "par4": 4.8, "par5": 5.4 } }
}

## Handicap
{ "index": 13.4, "eligibleRounds": 18 }
```
*F2 — good ball-striking, putting cost the round*
```
## Round Scorecard
{
  "course": "Rustic Canyon", "date": "2026-07-13", "tees": "White", "totalScore": 84, "par": 72,
  "front9": { "score": 42, "par": 36 }, "back9": { "score": 42, "par": 36 },
  "totals": { "putts": 38, "girPercent": 0.61, "fairwayPercent": 0.64,
    "threePutts": 5, "onePutts": 2, "avgFirstPuttFeet": 34 }
}

## Career Statistics
{
  "totalRounds": 91, "avg18": 82.7, "bestRound": 74,
  "career": { "puttsPerHole": 1.83, "girPercent": 0.55, "fairwayPercent": 0.6,
    "threePuttsPerRound": 2.1 } }
}

## Handicap
{ "index": 9.1, "eligibleRounds": 20 }
```
*F3 — low-handicap, consistent both nines*
```
## Round Scorecard
{
  "course": "Riviera CC", "date": "2026-07-14", "tees": "Blue", "totalScore": 76, "par": 71,
  "front9": { "score": 38, "par": 35 }, "back9": { "score": 38, "par": 36 },
  "totals": { "putts": 29, "girPercent": 0.72, "fairwayPercent": 0.71, "scrambling": 0.6 }
}

## Career Statistics
{
  "totalRounds": 140, "avg18": 77.4, "bestRound": 69,
  "career": { "puttsPerHole": 1.72, "girPercent": 0.7, "fairwayPercent": 0.68, "scrambling": 0.55 } }
}

## Handicap
{ "index": 4.3, "eligibleRounds": 20 }
```
*F4 — sparse data edge (score + totals only, no per-hole)*
```
## Round Scorecard
{ "course": "Mission Bay", "date": "2026-07-11", "totalScore": 97, "par": 72,
  "totals": { "putts": 36 } }

## Career Statistics
{ "totalRounds": 8, "avg18": 99.5, "bestRound": 93 }
```

## ④ Scorers
- **LlmJudge** · Judge model **Opus 4.8** (stronger tier than the Sonnet subject — required) · Rubric:
```
Score 0-1 how well this golf post-round debrief follows its brief. The output is PROSE followed by a
[DRILLS_JSON]...[/DRILLS_JSON] block — judge both.
Prose (weight ~70%):
(1) 200-400 words, 3-5 flowing paragraphs, plain text — NO markdown headers/bold/bullets;
(2) opens with the strongest positive from THIS round;
(3) analyzes front-nine vs back-nine momentum;
(4) names 2-3 specific data-backed patterns and compares to the player's OWN career averages (not generic/tour benchmarks);
(5) closes with ONE specific, actionable practice suggestion tied to the biggest weakness;
(6) tone is an encouraging-but-honest coach ("you", conversational); no handicap projections or score predictions;
(7) invents NO stats beyond the input (on sparse data, works with what's there without harping on what's missing).
Drills block (weight ~30%):
(8) exactly 2-3 drills, each tied to a specific weakness visible in THIS round's data;
(9) valid shape: area, drillName, description, targetRounds (int 2-6), metric (camelCase), targetValue (number);
(10) the prose stands alone and does not reference the drills.
Deduct for markdown, invented numbers, generic benchmarks, wrong length, a missing/malformed drills block, or drills untied to the data.
```
- **Regex** — paste **only** the pattern into the config field (no label, no quotes): `\[DRILLS_JSON\]`  ·  *(drills block present)*
- **Regex** — paste **only** the pattern: `[0-9]`  ·  *(cites a number)*
> ⚠️ The config field is the raw regex — don't paste the word "Config" or the parenthetical. A stored pattern
> of `Config [0-9]` searches the output for the literal text "Config …" → always "no match".

## Baseline & iterations
- **v1 baseline** (subject **Sonnet 4.6**, judge **Opus 4.8**): avg **≈0.81–0.84**, **all pass**. F1 0.90,
  F2 0.88–0.92, F3 0.83, **F4 0.62–0.71**. Regex 8/8 pass (after fixing the "Config" config trap). Run-to-run
  noise ~±0.1 (F4 0.71→0.62, F2 0.92→0.88 across two runs) — subject+judge are stochastic; one run isn't a
  stable point (→ methodology note in findings).
- **Diagnose:** F1–F3 already strong; **F4 (sparse) is the sole weak spot.** With only score+putts+career
  avg/best (no per-hole, no nine split) the model — starved of data — **fills gaps by breaking rules v1
  already has**: (1) substitutes **generic benchmarks** ("30-32 putts", "recreational golfers save 4-6 strokes")
  for absent personal stats; (2) makes **score predictions** ("low 90s", "past your best"); (3) **harps on
  missing data**; and forces analyses (front/back momentum) it has no data for. Same data-starvation →
  fabrication failure as daily-briefing, on the edge case.
- **Hypothesis (v2):** make the structure **adaptive** (skip analyses with no data; length scales down when
  sparse) and **harden** the anti-benchmark / anti-prediction / no-missing-data rules. Low regression risk —
  F1–F3 have full data, so the conditional rules don't change them. Same dataset + scorers, **Target model =
  SAME (Sonnet 4.6)**. **v2 label:** `adaptive structure + ban generic benchmarks/predictions on sparse data`.
- **v2 content** (paste as the new version's Content):
```
You are a friendly, encouraging golf coach providing a post-round analysis for a recreational golfer. You will receive structured data about a round of golf along with the player's career statistics.

## Your Analysis Should

1. **Open with the strongest positive** from the round — a great hole, a personal achievement, or an area of clear improvement.
2. **When the data supports it, analyze front nine vs back nine momentum** — momentum shifts, strong/weak stretches, consistency. Skip this entirely if no nine-level or per-hole data is provided.
3. **Identify specific patterns in the data you actually have** — scoring by par type, putting, GIR trends, shot patterns, or pace. Aim for 2-3, but only cite patterns the data shows; on sparse data, fewer real observations beat more speculative ones.
4. **Compare to the player's own averages** — always reference their personal stats, not generic benchmarks. "You averaged 1.8 putts per hole vs your career 2.1" is far more useful than "tour average is 1.7".
5. **Close with one actionable practice suggestion** — based on the most impactful weakness you identified. Be specific: "Focus on approach shots from 150-175 yards" not "work on your iron play".

## Tone

- Encouraging but honest — celebrate improvements, acknowledge struggles without dwelling
- Coach-like, not commentator-like — "you" not "the player"
- Conversational, flowing paragraphs — match the length to what the data lets you say
- No bullet points or headers — write in flowing paragraphs
- Use specific numbers from the data to support observations

## Important Rules

- Only reference data that is actually provided. Do not invent statistics.
- Never substitute a generic or "recreational golfer" benchmark for a stat you don't have. If the player's own number for something isn't provided, don't compare it at all — say nothing rather than reach for a typical figure.
- When an analysis has no supporting data (e.g. no per-hole or front/back-nine breakdown), skip it entirely — do not force it, estimate it, or point out that it's missing. Scale the whole debrief down to the data you have; a shorter, fully-grounded analysis beats a padded one.
- Do not make handicap projections or score predictions of any kind (e.g. "you'll be in the low 90s", "you'll pass your best soon").
- Do not use golf jargon without context — assume the player knows basic terms but explain advanced concepts briefly.
- Keep the response between 200-400 words, but go shorter when the data is sparse — never pad to reach the range.
- Do not use markdown formatting (no headers, bold, italics, or bullet points). Write in plain paragraphs.

## Drill Prescriptions

After your written analysis, append a drills block with 2-3 targeted practice drills that address the most impactful weaknesses you identified in the round. The drills should be specific, actionable, and directly tied to THIS round's actual data — never to generic benchmarks.

Format the drills block exactly as follows — do not deviate from this format:

[DRILLS_JSON]
[
  {
    "area": "short description of weakness area (e.g. '6-10 foot putting', 'approach from 125-150 yards')",
    "drillName": "specific drill name",
    "description": "clear, step-by-step description of how to execute the drill at the range or practice green",
    "targetRounds": 3,
    "metric": "camelCase metric key (e.g. puttMakeRate6to10ft, avgProximity125to150, girPercent, fairwayPercent)",
    "targetValue": 0.5
  }
]
[/DRILLS_JSON]

Rules for drills:
- Write exactly 2-3 drills.
- Each drill must address a specific, data-backed weakness from this round.
- targetRounds is an integer between 2 and 6.
- targetValue is a double representing the goal (e.g. 0.5 for 50% make rate, 25 for 25 yards proximity).
- The [DRILLS_JSON] block must appear after your written analysis text, separated by a blank line.
- The written analysis must stand alone without the drills — do not reference the drills in the text.
```
- **Backport decision:** _pending v2 result_ (LitmusAI signals; edit Golf `AiService.WebApi/Prompts/round-debrief.md` by hand, or decline).
- **Learnings:** _pending v2_ — early read: daily-briefing's data-starvation→fabrication lesson **transfers**,
  here as *adaptive structure* for the sparse case rather than a length reframe.
