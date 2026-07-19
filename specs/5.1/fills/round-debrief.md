# Fill sheet — `round-debrief` (Cortex Golf) — durable record

> The full click-by-click playbook + verbatim prompt/fixture/rubric blocks have been executed and
> retired; recover them from git history if needed. The prompts/fixtures/scorers now live **in
> LitmusAI** (created by hand during the walk) and the shipped prompt in **Golf git `9ba2ad3c`**.
> Study detail: [../catalog.md](../catalog.md) · runbook: [../runbook.md](../runbook.md).

## Setup
- **Prompt** `round-debrief` — post-round coaching narrative (200-400 words) + a `[DRILLS_JSON]`
  block of 2-3 data-tied drills.
- **Subject model `claude-sonnet-4-6`** (Golf's real default; in the catalog since v0.15.0 / [1.19]).
  **Judge = Opus 4.8** — must be a *stronger tier* than the Sonnet subject (Sonnet-5 is the same tier,
  disqualified as judge here). Confirmed via the `claude-api` skill.
- **Dataset** `Core round scenarios` — 4 representative post-round profiles:
  - **F1** strong front, back-nine blow-up (full per-hole data).
  - **F2** good ball-striking, putting cost the round (totals + putting splits).
  - **F3** low-handicap, consistent both nines (full totals).
  - **F4** sparse edge — score + putts + career avg/best only, **no per-hole / nine split**.
- **Scorers** — LlmJudge (Opus 4.8) rubric judging prose (~70%) + drills block (~30%); Regex
  `\[DRILLS_JSON\]` (drills present) and `[0-9]` (cites a number).
  - ⚠️ Config trap (fixed): paste **only** the raw regex into the config field — a stored pattern
    of `Config [0-9]` matches the literal text "Config …" → always "no match".

## Versions
- **v1 `baseline`** — Golf's shipped prompt. Fixed structure (always front/back momentum + "2-3
  patterns"); bans markdown, benchmarks, predictions.
- **v2 `adaptive structure + ban generic benchmarks/predictions on sparse data`** — makes the
  structure **conditional** (skip analyses with no data; scale length down when sparse) and hardens
  the anti-benchmark / anti-prediction / no-missing-data rules. **← shipped to Golf `9ba2ad3c`.**
- **v3 `sparse few-shot + never-reference-missing-data`** — v2 + a sharper missing-data rule + a
  sparse worked example (few-shot). **Not shipped** (didn't beat v2).

## Baseline & diagnosis
- **v1 baseline** (Sonnet 4.6 / Opus 4.8): avg ≈**0.81-0.84**, all pass. F1 0.90, F2 0.88-0.92,
  F3 0.83, **F4 0.62-0.71**. Regex 8/8 (after the "Config" trap fix). Run-to-run noise ~±0.1.
- **F4 (sparse) is the sole weak spot.** Starved of data, the model fills gaps by breaking v1's own
  rules: substitutes **generic benchmarks** for absent personal stats, makes **score predictions**,
  and **harps on missing data**. Same data-starvation → fabrication failure as daily-briefing, on the
  edge case.

## Verdict (3-way on the data-conditional rubric — the fair yardstick)
- v1 avg **0.79** (F1 .85 / F2 .88 / F3 .82 / **F4 .60**)
- v2 avg **0.88** (.88 / .90 / .85 / **F4 .90**) ← **winner**
- v3 avg **0.84** (.82 / .87 / .86 / **F4 .82**)
- The **rubric was the real F4 cap**: v2's already-clean sparse output went 0.62 (old rubric) →
  **0.90** (fair rubric); v1 stays ~0.60 because its output genuinely fabricates. **v3's few-shot
  didn't help** (≤ v2 on 3/4 fixtures + ~200 tokens/call) — instruction-based v2 was enough.
- **Backport decision: ✅ v2 → Golf `9ba2ad3c`** (`feat(ai): improve round-debrief prompt…`). The
  **data-conditional rubric** change stays in LitmusAI (it's measurement, not what Golf runs).

## ⚠️ Model-drift caveat (finding R5) — pending validation
- v1 ran on **Sonnet 4.6** but **v2/v3 ran on Sonnet 5** (add-version didn't hold the model), so the
  v1→v2 comparison confounded *prompt* with *model upgrade*, and the backported v2 was **validated on
  Sonnet 5, not Golf's Sonnet 4.6**. Changes are behavioral/model-agnostic so the backport is *likely*
  fine — but **unvalidated on the real model**.
- **v4 real-model validation (still to do — left for the user):** add **v4 = v2's exact content,
  Target model = `Claude Sonnet 4.6`** (paste v2 from git history; do **not** seed-from-latest, which
  is v3). Run v4, then **Compare v1 vs v4** (both Sonnet 4.6, fair rubric) → the clean prompt-only
  result on Golf's real model. v4 ≥ v1 → backport confirmed; else reconsider. (R5's add-version model
  hold + cross-model Compare flag now ship in 2.12, so this drift is prevented going forward.)

## Learnings
1. daily-briefing's **data-starvation → fabrication** lesson **transfers** — explicit bans removed the
   benchmarks/predictions on sparse input.
2. **Score ≠ quality on a single run** — v2 improved the output while the aggregate stayed flat; the
   win was visible only in the **rationale** (→ R4). Never attribute from an incomplete version set
   (an earlier "few-shot is the hero" read came from a partial v1+v3 set; the full 3-way flipped it).
3. A single rubric over rich+sparse fixtures **caps the sparse ones** (→ R3) — author data-conditional
   rubrics or split the dataset. ~±0.1 run-to-run noise: judge the trend, not one decimal (→ R4).

## Findings raised (see [../findings.md](../findings.md))
- **B8** analytics dataset picker leak · **R2** silent timeout/gateway failure · **R5** subject-model
  drift — **all shipped in [2.12]** (2026-07-19).
- **R1** async runs · **R3** data-conditional/per-fixture rubric scoring · **R4** variance view +
  rationale-diff — **homed in [2.12], not yet built** (heavy slices).
