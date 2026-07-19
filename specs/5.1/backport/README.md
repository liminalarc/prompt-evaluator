# Backport-ready prompts (source of truth for Cortex)

Each file here is the **current best version of a prompt**, as a clean drop-in for its source app —
no LitmusAI metadata, byte-for-byte what the source repo's prompt file should contain. An agent in
the source repo (Cortex Golf / Stormboard) consumes these and applies them; LitmusAI does **not**
commit into the source repos (5.1 process decision, 2026-07-19).

Provenance, eval evidence, and the version history that produced each prompt live in the fill sheets
(`../fills/<prompt>.md`) and findings (`../findings.md`).

| File | App | Best version | Eval evidence (Sonnet 4.6 subject · Opus 4.8 judge · data-conditional rubric) |
|------|-----|--------------|-------------------------------------------------------------------------------|
| `round-debrief.md` | Cortex Golf | **v7** | avg ~0.84 (Sonnet 4.6); v7 adds an input-whitelist + anti-filler rule that **fixed the sparse over-inference** v6 still had (F4 rationale: drills tied only to putts, no invented GIR/ball-striking) — chosen over v6 for safer edge-case behavior (rich-fixture cost was within noise). Golf path: `server/src/AiService/AiService.WebApi/Prompts/round-debrief.md`. |
| `daily-briefing.md` | Cortex Golf | **v2** | eval v1 0.55 → v2 0.88 (T2 shakeout); firm length + ban computed stats. Golf path: `server/src/AiService/AiService.WebApi/Prompts/daily-briefing.md`. |

> **Golf commits reverted 2026-07-19** — the direct backport commits (round-debrief v2/v6, daily-briefing)
> were removed from Golf `main` (kept the unrelated polygon commit); recoverable via the Golf tag
> `backup/prompt-backports-20260719`. These files are now the source of truth; an agent applies them.

> Update the file when a better version validates; note the new version + evidence in the row above.
