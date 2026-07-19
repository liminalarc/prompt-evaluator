# LitmusAI — positioning

The single source of truth for how we talk about LitmusAI. The README intro and the in-product
copy (login, register, onboarding, dashboard) borrow from this — keep them in sync when this changes.

## One-liner

**Stop shipping prompt regressions.** LitmusAI versions every LLM/SLM prompt in our apps, scores each
version against real test cases, and flags the moment a change makes a prompt worse — before it reaches
users.

## Who it's for

Engineers who own a prompt embedded in one of our apps (Cortex Golf, Stormboard, and the rest) and need
**evidence** that a change helped rather than hurt — instead of eyeballing a diff and hoping. Internal
tool; no external audience or pricing.

## The problem we remove

A prompt tweak that reads like an improvement can quietly regress on cases you didn't re-check. Without a
harness, "did this get better?" is a vibe. LitmusAI makes it a measured, version-over-version answer, and
tells you when a better version is sitting unshipped (backport-eligible).

## Value beats (outcome first, mechanics second)

1. **Catch regressions before users do** — every version scored against the same test cases; drops are
   flagged, not discovered in production.
2. **Know which version to ship** — a "Current in source" marker plus a backport-eligible signal when a
   higher-scoring version exists.
3. **Trust the score** — deterministic checks + an LLM judge + human review, composed per dataset, with
   same-scorer-config comparisons so a rubric change can't hide a real drop.

## Voice

Plain, outcome-framed, benefit-first. Model line (already live): the dashboard subtitle
*"How {org}'s prompts are doing."* and empty states that narrate the next action
(*"run a prompt over a dataset to see activity"*). Avoid category labels and mechanics-first intros in
user-facing copy; keep engineering terms (e.g. "fixtures") to developer docs, "test cases" in the UI.
