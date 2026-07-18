# 5.1 — Dogfood findings (shakeout log)

> Findings surfaced while walking prompts through LitmusAI (per 5.1's "findings feed the backlog"
> rule). Each has a **type** and a **proposed home**; homes are confirmed with the user, then
> re-homed into a spec. First batch: the `daily-briefing` T2 shakeout (2026-07-18).

## Bugs
- **B1 — Run 500s with no clear error on a missing provider key.** First eval on dev returned a bare
  HTTP 500 because eval-runner's Anthropic provider wasn't configured (`ANTHROPIC_API_KEY` placeholder).
  Should fail with a clear "eval-runner: Anthropic not configured" message; ideally a preflight/health
  check catches it before a run. → *home: 2.8*
- **B2 — UI swallows the run error.** No banner, spinner, or toast on the 500 — "looked like nothing
  happened." Run failures must be loud. → *home: 2.8*
- **B3 — Run-eval Prompt droplist is not org-scoped.** `dataset-detail` lists prompts across *all* orgs
  (`listPrompts()`), so foreign-org prompts leak in. Deeper: a dataset belongs to one prompt
  (`Dataset.PromptId`) — the picker should be **fixed to the dataset's owning prompt** (pick only a
  version), which fixes the org leak too. → *home: 2.8*
- **B4 — LlmJudge rubric is a single-line `<input>`.** A rubric is multi-line → should be a `<textarea>`.
  → *home: 2.8*
- **B5 — Scorer Config is optional even when required.** `Regex`/`JsonSchema` need a pattern/schema;
  the field is blanket-optional. Make it required per scorer kind. → *home: 2.8*

## UX / consistency
- **U1 — Create-prompt lands on the prompts table**, not the new prompt's workspace (`/prompts/:id`).
  Should navigate to the thing you just made. → *home: 2.8*
- **U2 — Version-history should be a collapsible list** (progressive disclosure, 2.4 pattern): rows only,
  details/add revealed on `+` or row-select. → *home: 2.8*
- **U3 — Allow editing version *metadata*** (label/description) — content stays locked (immutable, correct),
  and **target model stays immutable too** (it defines the run; change it by adding a version — see D1/§below).
  → *home: 2.8*
- **U4 — Clarify versioning:** auto `v1/v2…` is the identity; "Label" is just an optional description —
  name/position it that way. → *home: 2.8*
- **U5 — Create-dataset form lacks a Description field** (the domain has `Dataset.Description`; only the
  create form omits it). → *home: 2.8*
- **U6 — Fixtures need the add/edit table pattern** (add or edit a row → reveal details), like the rest. → *home: 2.8*
- **U7 — Fixtures have no label/description.** A short name ("improving mid-handicapper") beats showing the
  raw input in the table. Add `Fixture.Label`/`Description`. → *home: 2.8*
- **U8 — Hand-entered fixtures are mislabeled `Captured`.** The capture form is the only manual-entry path
  and stamps origin `Captured`; truly-synthetic hand-written fixtures read wrong. → *home: 2.8*
- **U9 — Scorers need the add/edit table pattern** (add or edit → fields → save). → *home: 2.8*

## Design (decision needed)
- **D1 — System-prompt vs "normal" prompt / prompt *shape*.** Today execution is fixed: version content →
  *system* prompt, fixture input → *user* turn (+ optional upstream context). That fits **all 54** current
  prompts (Golf + Stormboard are both system-prompt + serialized-input). A distinction would matter only for
  prompts that are single-message/templated (`{input}` substitution) or user-role. **Recommendation:** no
  change now (no premature abstraction); revisit if a prompt that doesn't fit appears. → *home: decision
  logged; spec only when needed*

## Ops / infra
- **O1 — Dev deployed without the Anthropic key set.** Provisioning shipped the secret as a placeholder;
  the first eval was the first thing to exercise it. The next environment shouldn't repeat this — add a
  post-deploy check that a real key is present. → *home: 3.2 / infra runbook*
