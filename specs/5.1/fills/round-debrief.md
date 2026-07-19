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
- **Regex** · Config `\[DRILLS_JSON\]` (drills block present)
- **Regex** · Config `[0-9]` (cites a number)

## Baseline & iterations (fill as you run)
- **v1 baseline** (Sonnet 4.6/5, judge Opus 4.8): _pending run_ — record judge value per fixture + aggregate,
  and which fixtures fail either Regex guardrail.
- **Diagnose / hypothesis:** _pending_ (watch for: length drift past 400 on rich data like F1/F2; markdown creeping
  into the prose; drills not tied to the actual weakness; front/back momentum skipped on F3's even split; over-noting
  missing data on F4).
- **v2:** _pending_ (Target model = SAME as v1).
- **Backport decision:** _pending_ (LitmusAI signals; edit Golf `AiService.WebApi/Prompts/round-debrief.md` by hand, or decline).
- **Learnings:** _pending_ — does the daily-briefing lesson ("firm length target reached by citing real data; ban
  computed stats") transfer to a longer prose+JSON prompt?
```
