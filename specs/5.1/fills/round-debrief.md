# Fill sheet — `round-debrief` (Cortex Golf) — durable record

> Clean, backport-ready prompt: [../backport/round-debrief.md](../backport/round-debrief.md) (currently **v7**).
> Verbatim version content, fixture JSON, and the executed step-by-step playbook are retired to git history;
> the prompts/fixtures/scorers live in LitmusAI (built by hand during the walk). Study: [../catalog.md](../catalog.md).

## Setup
- **Prompt** `round-debrief` — post-round coaching narrative (200-400 words) + a `[DRILLS_JSON]` block of 2-3 data-tied drills.
- **Subject `claude-sonnet-4-6`** (Golf's real model); **judge Opus 4.8** (stronger tier — Sonnet-5-as-judge is disqualified, same tier).
- **Dataset** `Core round scenarios` — 4 fixtures: **F1** strong-front/back-blowup (full per-hole), **F2** putting-cost (totals+putting splits), **F3** low-hcp consistent (full totals), **F4** sparse edge (score + putts + career avg/best only, no per-hole).
- **Scorers** — LlmJudge (Opus 4.8, **data-conditional rubric** — prose ~70% + drills ~30%), Regex `\[DRILLS_JSON\]` + `[0-9]`.
  - ⚠️ Config trap (fixed): paste **only** the raw regex — a stored `Config [0-9]` matches the literal text "Config …".

## Versions (content in git / `backport/`)
- **v1** `baseline` — Golf's shipped prompt (fixed structure; bans markdown/benchmarks/predictions).
- **v2** — adaptive structure + harden anti-benchmark/prediction/missing-data rules.
- **v3** — v2 + sharper missing-data rule + sparse few-shot (didn't beat v2 on Sonnet 5; few-shot dropped).
- **v4/v5** — v2 content re-run on **Sonnet 4.6** for real-model validation (v4 accidentally saved on Sonnet 5; v5 correct).
- **v6** — v2 + sharper never-narrate-missing-data / ban typical-golfer framing / no soft predictions / sparse-drill discipline (instruction-only, no few-shot).
- **v7** `input-whitelist + anti-filler` — v6 + "only analyze/drill metrics literally in the input" + no filler. **← chosen for backport.**

## Real-model validation (Sonnet 4.6) — the honest result
The Sonnet-5 walk had said v2 crushed v1 on the sparse fixture (0.90 vs 0.60). On Golf's **real** model that
collapsed: v1 ≈ v5 (avg ~0.8175 vs ~0.815; sparse ~0.75 vs ~0.72) — **most of the "v2 win" was the model, not
the prompt** (R5, quantified). Rationales (not scores) drove every call from there:
- **v1** still makes a soft prediction ("cracking 95") + invents detail; v5/v2 doesn't → backport justified on **risk**, not score.
- **v6** (avg ~0.84) reliably killed predictions/benchmarks/missing-data narration but **over-inferred on sparse**
  (invented a `girPercent` drill from score+putts), and its F4 was **noisy** (0.82 then 0.72 — one run lies, R4).
- **v7** (avg ~0.84) added an input-whitelist rule that **fixed the over-inference** (F4 rationale: drills tied only
  to putts, no invented stats), at a possible small (likely-noise) cost to the rich fixture F1. **Chosen** for the
  safer edge-case behavior — the thin-data edge is where a coaching app's fabrication risk actually lives.

**Verdict: v7 → [../backport/round-debrief.md](../backport/round-debrief.md).** Applied by a source-repo agent
(LitmusAI doesn't commit into Golf — the direct v2/v6 commits were reverted; new process, runbook Step 9).

## Learnings
1. **Hold the subject model (R5).** A cross-model comparison overstated the prompt's effect ~4×; the real gain only showed on Golf's actual model.
2. **Score ≠ quality/risk; read the rationale (R4/R7).** Every call — v1-vs-v5, v6-vs-v7 — turned on *why* the judge deducted, not the number: tied scores hid a prediction-risk difference; a 0.82/0.72 wobble hid a fixed defect.
3. **One run lies (R4).** v6's F4 swung 0.10 between runs; a backport was called off a single run and had to be walked back. Run the noisy fixture 2-3×.
4. **The sparse edge is the fabrication hotspot.** Every residual defect (benchmarks, predictions, over-inference) surfaced only on the thin-data fixture — targeted *rules*, not few-shot, fixed them.

## Findings raised (see [../findings.md](../findings.md))
- **Shipped in [2.12]** (2026-07-19): **B8** analytics-picker leak · **R2** loud run failure · **R5** hold-model + cross-model flag · **R1 band-aid** run timeout 100s→5min.
- **Homed, not built:** **R1** async runs · **R3** data-conditional/per-fixture rubric · **R4** variance + rationale-diff · **R7** structured/severity-tagged judging (likely its own spec) · **U15/U16/R6** UX nits · **R8 → [2.13]** Dataset Design Assistant.
