# Backport-ready prompts (source of truth for Cortex)

Each file here is the **current best version of a prompt**, as a clean drop-in for its source app —
no LitmusAI metadata, byte-for-byte what the source repo's prompt file should contain. An agent in
the source repo (Cortex Golf / Stormboard) consumes these and applies them; LitmusAI does **not**
commit into the source repos (5.1 process decision, 2026-07-19).

> **Now tool-native (2026-07-20).** These files are **produced by LitmusAI's `Prepare backport`** (1.20 —
> *Copy exact prompt* / *Download markdown*), not hand-copied, and the in-tool **`Mark backported → vN`**
> (1.16) is the official record of what's live. Runbook **Step 9** is the click-by-click. This supersedes
> the earlier hand-copy framing above (kept for provenance).

## ⏳ Pending official in-tool backport (do these through the tool)
Both prompts below were decided under the *old* hand-copy process, before the 1.16 marker / 1.20 artifact
existed. Re-formalize each **through the tool** (all by hand — 5.1 stays manual) so LitmusAI holds the truth:
- **`round-debrief` (Golf)** — in the tool: **Set as current in source** = the version Golf runs today
  (**v1** baseline / Golf's shipped prompt, paste its live SHA) → confirm the Deployment card recommends
  **`Backport target` = v7** (weighted composite; this is the 2.9 fix that picks v7 over v2) → **Prepare
  backport** → apply → **Mark backported → v7**.
- **`daily-briefing` (Golf)** — in the tool: **Set as current in source** = **v1** (Golf runs v1 again — the
  direct v2 commit `660a3ff2` was reverted; see below) → target should be **v2** → **Prepare backport** →
  apply → **Mark backported → v2**.

Provenance, eval evidence, and the version history that produced each prompt live in the fill sheets
(`../fills/<prompt>.md`) and findings (`../findings.md`).

| File | App | Best version | In-tool backport | Eval evidence (Sonnet 4.6 subject · Opus 4.8 judge · data-conditional rubric) |
|------|-----|--------------|------------------|-------------------------------------------------------------------------------|
| `round-debrief.md` | Cortex Golf | **v7** | ⏳ pending (Set Current v1 → Mark backported → v7) | avg ~0.84 (Sonnet 4.6); v7 adds an input-whitelist + anti-filler rule that **fixed the sparse over-inference** v6 still had (F4 rationale: drills tied only to putts, no invented GIR/ball-striking) — chosen over v6 for safer edge-case behavior (rich-fixture cost was within noise). Golf path: `server/src/AiService/AiService.WebApi/Prompts/round-debrief.md`. |
| `daily-briefing.md` | Cortex Golf | **v2** | ⏳ pending (Set Current v1 → Mark backported → v2) | eval v1 0.55 → v2 0.88 (T2 shakeout); firm length + ban computed stats. Golf path: `server/src/AiService/AiService.WebApi/Prompts/daily-briefing.md`. |

> **Golf commits reverted 2026-07-19** — the direct backport commits (round-debrief v2/v6, daily-briefing)
> were removed from Golf `main` (kept the unrelated polygon commit); recoverable via the Golf tag
> `backup/prompt-backports-20260719`. These files are now the source of truth; an agent applies them.

> Update the file when a better version validates; note the new version + evidence in the row above.
