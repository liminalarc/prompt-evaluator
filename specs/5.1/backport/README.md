# Backport-ready prompts (source of truth for Cortex)

Each file here is the **current best version of a prompt**, as a clean drop-in for its source app —
no LitmusAI metadata, byte-for-byte what the source repo's prompt file should contain. An agent in
the source repo (Cortex Golf / Stormboard) consumes these and applies them; LitmusAI does **not**
commit into the source repos (5.1 process decision, 2026-07-19).

> **Now tool-native (2026-07-20).** These files are **produced by LitmusAI's `Prepare backport`** (1.20 —
> *Copy exact prompt* / *Download markdown*), not hand-copied, and the in-tool **`Mark backported → vN`**
> (1.16) is the official record of what's live. Runbook **Step 9** is the click-by-click. This supersedes
> the earlier hand-copy framing above (kept for provenance).

## Official in-tool backport — both DONE (2026-07-20)
Both prompts were decided under the *old* hand-copy process, before the 1.16 marker / 1.20 artifact existed,
and have now been re-formalized **through the tool** (all by hand — 5.1 stays manual) so LitmusAI holds the truth:
- ~~**`round-debrief` (Golf)**~~ — ✅ **DONE (2026-07-20).** The tool's `Backport target` badge **mis-picked
  v2** (Sonnet 5) over v7 (Sonnet 4.6) — the **R9** subject-model confound (see `../findings.md`; homed to
  [2.9a](../../2.9a.md)). So we **bypassed the target button and the v2 artifact**: a source-repo agent applied
  the hand-made **v7** drop-in (`round-debrief.md`, this dir) to Golf in a clean one-file commit **`d04617ed`**,
  then in-tool **Set as current in source → v7** via the v7 row (NOT `Mark backported → v2`). At the time the
  Deployment card mis-badged `Backport target = v2` — the **R9** subject-model confound — so `Prepare backport`
  was **not** usable (it would have emitted a v2 artifact) and this drop-in stayed hand-made, unlike
  daily-briefing's. **✅ R9 fixed + deployed 2026-07-20 (`2ccc27f`, [2.9a](../../2.9a.md)):** the card now shows
  **no target** (v7 is top among Sonnet-4.6 versions) + **⚠ 3 cross-model versions excluded**.
- ~~**`daily-briefing` (Golf)**~~ — ✅ **DONE (2026-07-20).** Re-formalized through the tool: Set Current = v1
  → target badge = v2 → **Prepare backport** (artifact reproduced the committed drop-in byte-for-byte — 1.20
  dogfood pass) → a **source-repo agent applied v2 to Golf**, clean one-file commit **`abd385f8`** → **Mark
  backported → v2**. (Commit SHA **not** recorded in-tool — no UI input; deliberate, finding **D2**.)

Provenance, eval evidence, and the version history that produced each prompt live in the fill sheets
(`../fills/<prompt>.md`) and findings (`../findings.md`).

| File | App | Best version | In-tool backport | Eval evidence (Sonnet 4.6 subject · Opus 4.8 judge · data-conditional rubric) |
|------|-----|--------------|------------------|-------------------------------------------------------------------------------|
| `round-debrief.md` | Cortex Golf | **v7** | ✅ done — applied to Golf `d04617ed`; Set current → v7 by hand (tool mis-picked v2 — R9) (2026-07-20) | avg ~0.84 (Sonnet 4.6); v7 adds an input-whitelist + anti-filler rule that **fixed the sparse over-inference** v6 still had (F4 rationale: drills tied only to putts, no invented GIR/ball-striking) — chosen over v6 for safer edge-case behavior (rich-fixture cost was within noise). Golf path: `server/src/AiService/AiService.WebApi/Prompts/round-debrief.md`. |
| `daily-briefing.md` | Cortex Golf | **v2** | ✅ done — applied to Golf `abd385f8`; Mark backported → v2 (2026-07-20) | eval v1 0.55 → v2 0.88 (T2 shakeout); firm length + ban computed stats. Golf path: `server/src/AiService/AiService.WebApi/Prompts/daily-briefing.md`. |
| `golf-dna.md` | Cortex Golf | **v2** | ✅ done — Set current → v1 → target v2 → source-repo agent applied → Mark backported → v2 (2026-07-22) | JsonSchema **3/5 → 5/5** across 3 runs (v1 fenced ~40% of outputs; parse error `'`' … byte 0`); LlmJudge flat ~0.88 (Sonnet 4.6 subject · Opus 4.8 judge). Single-line output-format fence ban. Golf path: `server/src/AiService/AiService.WebApi/Prompts/golf-dna.md`. |

> **Golf commits reverted 2026-07-19** — the direct backport commits (round-debrief v2/v6, daily-briefing)
> were removed from Golf `main` (kept the unrelated polygon commit); recoverable via the Golf tag
> `backup/prompt-backports-20260719`. These files are now the source of truth; an agent applies them.

> Update the file when a better version validates; note the new version + evidence in the row above.
