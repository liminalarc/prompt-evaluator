# 5.1 — Dogfood findings (shakeout log)

> Findings surfaced while walking prompts through LitmusAI (per 5.1's "findings feed the backlog"
> rule). Each has a **type** and a **proposed home**; homes are confirmed with the user, then
> re-homed into a spec. First batch: the `daily-briefing` T2 shakeout (2026-07-18).
>
> **Status (2026-07-18): B1–B7 + U1–U14 all shipped in [2.8] (archived — `specs/archive/2.8.md`).**
> D1 = decision logged (no spec, revisit when a non-fitting prompt appears); O1 → [3.2] / infra runbook.
>
> **Status (2026-07-19): round-debrief-walk findings all homed to [2.12](../2.12.md).**
> **B8, R2, R5 shipped** (2.12 quick-fix slices, 2026-07-19). **R1** (async runs), **R3** (data-conditional
> rubric scoring), **R4** (variance view + rationale-diff) are homed in 2.12 but **not yet built** (heavy
> slices; 2.12 stays IN PROGRESS). F1→[1.16], F2→[1.17], F3→[1.18] (standalone specs, not built in 5.1).

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
- **B6 — LLM-judge 500s when the judge model has thinking-on-by-default (sonnet-5, fable-5).**
  `judging.py` calls `provider.structured(..., max_tokens=1024)` with no thinking config;
  `AnthropicProvider.structured` (`providers.py`) never disables thinking. On models where omitting
  `thinking` runs **adaptive** (claude-sonnet-5, claude-fable-5 — per the `claude-api` skill), thinking
  consumes the 1024-token budget before the structured JSON finishes → truncated JSON →
  `json.loads` `JSONDecodeError` → eval-runner 500 → API 500. Models that default thinking-off
  (claude-opus-4-8, claude-haiku-4-5) are unaffected. **Fix (eval-runner):** on the judge's
  `structured()` call, pass `thinking={"type":"disabled"}` (a rubric-scorer doesn't need extended
  thinking) and/or raise `max_tokens`. This blocks LLM-judge scoring for ~all freeform prompts, so it's
  the highest-priority finding. → *home: eval-runner fix (own task / 2.8 backend slice)*

- **B7 — Fixture redactor corrupts ISO dates as phone numbers.** `FixtureRedactor.PhoneRegex`
  (`\+?\d[\d\-\.\s]{7,}\d`) matches `2026-07-12` → replaces it with `[REDACTED-PHONE]`. Runs **at
  ingest before persist**, so the stored fixture *and* the input sent to the model are corrupted (not
  display-only). Surfaced on the daily-briefing baseline (dates in RECENT ROUNDS all scrubbed). Fix:
  tighten the phone pattern so date-shaped strings don't match (e.g. require ≥10 digits, phone-like
  grouping / leading `+`/`(`, or a negative lookahead for `\d{4}-\d{2}-\d{2}`). → *home: 2.8 (bug)*

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
  raw input in the table. Add `Fixture.Label`/`Description`. Also bites **Analytics** — the compare-versions
  table lists fixtures by opaque GUID (`4ae1e431…`), so you can't tell which scenario moved. → *home: 2.8*
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

## Feature (new capability)
- **F1 — "Backport-pending" signal (deployed-version marker) — LitmusAI signals, never executes.**
  LitmusAI tracks `Prompt × Version` but has no concept of *which version the source app is actually
  running*. After a backport, that state lives only in 5.1's fill sheets — invisible inside the tool. Two
  costs: (a) on **re-onboarding** a prompt, the true baseline is the deployed version (e.g. `daily-briefing`
  v2), not v1, and nothing surfaces that; (b) you can't tell "won in the harness" from "shipped to the app."
  **Design stance (2026-07-18, user):** LitmusAI must **not** be a backport *executor* — it never edits a
  source repo. Its role is a **signal**: mark a version **Deployed** (with optional commit SHA + timestamp),
  and when the deployed version ≠ the best-scoring version, raise a **"backport pending"** flag. The actual
  backport is a human action in the source app's *own* process — which **may not be our flow/spec system at
  all**, so the signal and its "done" state (a manual *mark-deployed*) must stand entirely inside LitmusAI,
  assuming nothing about the source system. Overlaps the **registry's** job of tracking version/deployment
  state. → *home: **[1.16](../1.16.md)** — new standalone spec (user 2026-07-18). Not built in 5.1.*

  **Feature detail (2026-07-18, user) — version-status marking + backport lifecycle:**
  1. **`Current in source`** — a marker on exactly one version per prompt = the version the source app is
     believed to be running. Visible/noticeable (a badge on the version, not buried).
  2. **`Backport-eligible`** — auto-raised when a *higher-scoring* version exists above the `Current` one
     (a better prompt sits unshipped in the harness).
  3. **Mark-as-backported action** — after shipping, return to LitmusAI and move `Current` to the new
     version; the `Backport-eligible` flag then **clears** (no longer an opportunity).
  4. **Version-level badges** — surface status **on the version itself** in Version history, the way
     regressions should be marked too. Unifies into one **version-status** concept:
     `Current` · `Backport-eligible` · `Regressed`. (Note: today regressions live only in the Analytics
     table — this adds a marker *on the version*, a related gap the user flagged.)
- **F2 — No multimodal/image fixtures (domain-level).** `Fixture.Input` is a `string`
  (`src/Domain/Fixture.cs:23`); there is no representation for image/vision input. So **vision/multimodal
  prompts can't be walked end-to-end**. Blocks **6 Golf prompts**: `auto-map-centerline-detection`,
  `course-layout-analysis`, `overview-map-registration`, `per-hole-polygon-detection`, `scorecard-extraction`
  (HTML+images), `routing-review` (text+images). A real feature: `Fixture` carries multimodal input →
  eval-runner sends image blocks to the provider → UI gets an image-upload affordance. → *home:
  **[1.17](../1.17.md)** — new standalone spec (user 2026-07-18). Not built in 5.1.*
- **F3 — No tool-use / live-web prompt execution.** The harness runs `system prompt + user input` against a
  model; it does not replay an app's **tool loop** (`web_search`/`web_fetch`). Prompts whose real behavior
  *is* the tool loop can't be faithfully evaluated. Affects **3 Golf prompts**: `facility-enrichment`,
  `routing-hole-search`, `scorecard-search` (all `SearchModel` + web tools). Workaround: freeze tool
  results into synthetic fixtures (evaluates the reasoning, not the retrieval) — partial fidelity only.
  → *home: **[1.18](../1.18.md)** — new standalone spec (user 2026-07-18); or decline-with-reason per
  prompt. Not built in 5.1.*
- **Corollary (process, not a feature):** we do **not** create backport specs in source repos as a rule —
  can't assume a source app is on our process. The backport *process* is defined in 5.1 (playbook step 9:
  manual commit, or decline with reason); the *record* is the fill sheet + T3/T4 tick + source git history.
  Exception: the 2 Stormboard inline-prompt extractions (`wizard-prompts`, `asset-mapping`) are a structural
  refactor Stormboard may choose to track in its own system — its call, not ours to impose (see T4).

## Bugs (post-2.8, round-debrief walk)
- **B8 — Analytics dataset picker isn't prompt-scoped (cross-prompt leak).** `analytics-dashboard.ts` loads
  `datasetsApi.listDatasets()` (all) and filters only by **org** prompt ids (line ~312), never by the
  **selected prompt** — so under prompt=`round-debrief` the Dataset dropdown also offers `Core player
  scenarios`, which belongs to `daily-briefing`. A dataset belongs to exactly one prompt (1.7,
  `Dataset.PromptId`); picking a foreign one yields empty/mismatched analytics. Same class as **B3** (the
  run picker), same fix: filter `datasets()` by `d.promptId === promptId()`. → *home: **[2.12](../2.12.md)** — **shipped 2026-07-19**.*

## Reliability (2026-07-18, round-debrief walk)
- **R1 — Synchronous eval runs time out on heavier prompts.** The run endpoint
  (`EvalHarnessEndpoints` `MapPost .../eval-runs`) executes the whole batch inline — every fixture's
  subject generation **and** LLM-judge call — and only responds when done. The API→eval-runner HttpClient
  (`Infrastructure/DependencyInjection.cs`) sets **no timeout** → the .NET default **100 s**. round-debrief
  (4 fixtures × 200-400-word Sonnet output + Opus judge each) sits right at that edge: it **failed once, then
  succeeded on retry** — non-deterministic at the boundary. daily-briefing (75-100-word outputs) stayed under
  it. Real fix: **async runs** — kick off a job, return immediately, poll for results (also unblocks bigger
  datasets). Interim band-aid: raise the HttpClient + App Runner request timeouts. → *home: **[2.12](../2.12.md)** (heavy slice — may split out).*
  - **Update 2026-07-19 (v4-validation walk):** the 100s cap bit **live** — a `round-debrief` run 502'd twice
    on dev at the boundary. **Interim band-aid shipped:** the eval-runner HttpClient timeout is now **5 min**
    (was the 100s .NET default; `Infrastructure/DependencyInjection.cs`) and the compose nginx proxy timeouts
    are **300s** (were 60s; `web/nginx.conf` — local-only, dev serves the SPA same-origin from the API so no
    nginx is in its path). **Diagnosis:** dev's successful runs *and* its 502s both land near ~100s, so the
    HttpClient cap — not App Runner's router — was the ceiling; the bump should let round-debrief complete.
    App Runner exposes no per-request timeout to tune. **R1-proper (async)** is the real fix for genuinely long
    runs / bigger datasets → **promoted to its own spec [2.17](../2.17.md)** (not built).
- **R2 — Timeout/gateway 502 fails silently (hole in B2's "loud failures").** When a run 502s via **timeout or
  the App Runner gateway**, the body is not the API's structured `502 {error}` JSON — so the SPA's
  `serverError(err)` path (2.8) can't extract a message and **shows no banner at all**. B2 made the *structured*
  eval-runner error loud, but timeout/infra 5xx slip through silently ("nothing on screen" on the round-debrief
  timeout). Fix: surface **any** run failure loudly — a generic banner on a non-JSON/timeout 5xx, not only the
  `{error}` shape. → *home: **[2.12](../2.12.md)** — **shipped 2026-07-19**.*

## Eval methodology (2026-07-18, round-debrief walk)
- **R3 — One rubric over a heterogeneous dataset caps the "hard" fixtures, hiding real prompt gains.** The
  round-debrief rubric rewards "front/back-nine momentum" + "2-3 patterns" — analyses a **sparse** fixture
  (score+putts only, no per-hole/nine data) physically can't support. So the sparse fixture (F4) is capped
  ~0.62 **no matter how good the prose is**: even after v2 eliminated the real defects (fabricated benchmarks +
  score predictions — confirmed in the judge rationale), the fixture score didn't move, because the judge
  anchors on the structurally-impossible criteria. Net: the **aggregate said "v2 = no gain" while v2 genuinely
  removed a production fabrication risk** — you only see it by reading the rationale, not the number. Options:
  (a) **rubric-authoring guidance** — write data-conditional rubrics ("if no nine-level data, don't penalize
  its absence; reward graceful sparse-handling"); (b) keep datasets **homogeneous** (split sparse fixtures into
  their own dataset+rubric); (c) a tool feature — **per-fixture / conditional criteria** so one dataset can
  fairly score mixed-richness fixtures. → *home: **[2.16](../2.16.md)** (promoted from 2.12; + runbook rubric-authoring note) — **not yet built**.*
- **R4 — Score ≠ quality on a single run; read the rationale + expect noise.** Fixture scores wobble ~±0.1
  run-to-run (F1 0.90→0.85, F2 0.88→0.93) and a real prompt improvement can land as a **flat number** (v2). A
  stable baseline / regression call wants **repeated runs or a variance view**, and a diff should surface the
  **rationale delta**, not just the score delta. → *home: **[2.14](../2.14.md)** (promoted from 2.12; **the active build**).*

## Subject-model drift (2026-07-18, round-debrief walk)
- **R5 — Add-version doesn't hold the subject model; silent drift confounds prompt comparisons.** On
  round-debrief, v1 ran on `claude-sonnet-4-6` (Golf's real model) but **v2/v3 ran on `claude-sonnet-5`** —
  the add-version form defaulted the Target model to a different model and it slipped through, so the v1→v2
  comparison confounded *prompt* with *model upgrade*, and the backported prompt was **never validated on
  Golf's actual model**. For a tool whose whole job is isolating the prompt's effect, holding the subject
  model constant must be the **default**. Fix: (a) add-version **defaults Target model to the latest
  version's model**; (b) **warn** when a new version's model differs from the prior one; (c)
  Analytics/Compare **flags a cross-model comparison** (you can't cleanly compare prompts across different
  subject models — the axis isn't held). Sibling to 1.16's same-*scorer*-config rule: hold the identity axes
  (subject model **and** scorer config) constant when comparing versions. → *home: **[2.12](../2.12.md)** — **shipped 2026-07-19**.*

## UX / consistency (2026-07-19, v4 real-model validation walk)
- **U15 — The workspace "Run a version" recent-runs list labels runs by timestamp only.** The card
  (`prompt-detail`, `recentRuns`) shows each run as its raw `createdAt` + scorer kinds + fixture count —
  it never resolves the run to **which version / subject model** it scored, so you can't tell what a run
  *was* at a glance (which matters exactly when you're iterating v1→v5 and re-running). The **dataset**
  page's Runs table already does this (U14, 2.8: resolves `vN` + target model from the owning prompt's
  versions); this workspace card lagged behind. Fix: give the workspace recent-runs list the same
  version/model columns (and consider a short label). Same data is already loaded (`p.versions`).
  → *home: **[2.18](../2.18.md)** (eval-loop UX polish). Not built.*

- **R6 — The loud run-failure banner is page-level, so it lands off-screen (follow-up to R2).** R2
  (shipped 2.12) makes any run failure loud — verified live: a 502 timeout on a `round-debrief` run
  showed *"The run failed with a server error (HTTP 502)…"*. But the banner is the **page-level
  `<app-error-state>` at the top** (by the breadcrumb); triggering a run from the "Run a version" card
  (or the dataset run card) lower on a long page puts the failure **above the fold** — "I didn't see
  anything happen." The message is right, the **placement** is wrong. Fix options: a **toast/snackbar**
  that draws the eye (persists until dismissed) and/or an **inline error next to the run button**;
  optionally auto-scroll to the banner. → *home: **[2.18](../2.18.md)** (eval-loop UX polish). Not built.*

- **U16 — Dark mode: the topbar org-switcher dropdown options are near-unreadable.** In dark mode the
  open `<select>` option list (topbar org switcher, app shell 2.4) renders low-contrast — the
  non-selected options ("Default", "Stormboard") are dark-grey on dark and hard to read; only the
  highlighted option is legible. Native `<option>` popups aren't fully brand-token-styled. Likely fix:
  set **`color-scheme: dark`** on `:root`/the control in dark theme (makes the browser render native
  dropdowns dark-aware), or explicitly token the select's `color`/`background`. Not specific to 5.1 —
  a general dark-mode/design-system nit. → *home: **[2.18](../2.18.md)** (eval-loop UX polish; may fold into 2.6). Not built.*

## Eval methodology — the biggest gap (2026-07-19, v4/v5 real-model validation)
- **R7 — The tool throws away the judge's *reasons*; score ≠ risk.** Decisive evidence from the v4/v5 walk:
  v1 (0.75) and v5 (0.72) scored a **dead heat**, but v1's rationale "**edges toward a 'cracking 95'
  quasi-prediction**" + invents speculative detail, while v5 makes **no predictions** — a higher-severity
  production risk that the **scalar score cannot see**. The whole call had to be made by reading prose by
  hand. LitmusAI collapses a rich judge verdict to one 0-1 and discards the signal that matters. **Build a
  structured-rationale layer:** (a) **per-criterion pass/fail** (the rubric already enumerates ~10 criteria)
  instead of one scalar; (b) **failure-mode tagging** — each deduction categorised (score-prediction /
  invented-stat / generic-benchmark / narrates-missing-data / over-inference — a recurring family, also hit
  by daily-briefing) → a **version × failure-mode matrix**; (c) **severity weighting** so risk, not a flat
  average, drives the verdict (a clean 0.72 can beat a fabricating 0.75); (d) **rationale-diff in Compare**
  (R4's other half) — surface *what reasons changed*, not just the score delta. Distinct, sizeable capability
  — bigger than R3 (per-fixture criteria) / R4 (variance+rationale-diff) / 2.9 (weighted *scorer* composite),
  though it subsumes parts of each. → *home: **new spec [2.15](../2.15.md)** (structured/severity-tagged judging). Not built.*

- **R8 — Building a good test dataset is an artform with no proactive tooling.** Surfaced while building
  `Core round scenarios`: coverage, rich-vs-sparse balance, homogeneity-vs-split, capture/synthetic ratio,
  and fixture-count sufficiency are all craft decisions the tool doesn't help with — and getting them wrong
  silently sabotages the eval (R3's cap; R7's hidden risk). The *mechanics* live in [[1.2]] and the
  *reactive* critique in [[2.2]] (given run history), but the **proactive, at-build-time** craft guidance —
  the dataset sibling of 2.7's proactive prompt authoring — has no home. → *home: **new spec [2.13](../2.13.md)**
  (Dataset Design Assistant), drafted concept-only 2026-07-19 (user). Not built.*

## Ops / infra
- **O1 — Dev deployed without the Anthropic key set.** Provisioning shipped the secret as a placeholder;
  the first eval was the first thing to exercise it. The next environment shouldn't repeat this — add a
  post-deploy check that a real key is present. → *home: 3.2 / infra runbook*
