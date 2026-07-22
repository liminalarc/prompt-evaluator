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
- **D2 — The `Current in source` marker's commit SHA is plumbed everywhere except the UI input — decided
  NOT to track it (2026-07-20, tool-native backport walk).** The optional `commitSha` is threaded end-to-end:
  the `set-current` endpoint accepts it, `Prompt.CurrentVersionSha` stores it, the Deployment card *renders*
  it when present, and the 1.20 artifact markdown carries a "Current commit" line — **but** `prompt-detail`'s
  `setCurrent(versionId)` calls the API with `commitSha` unset for **both** buttons (`Set as current in
  source` *and* `Mark backported → vN`), and **no text field exists** to enter one. So the feature is
  half-wired: supported by the model/API, unreachable from the screen. Surfaced when trying to record Golf's
  `daily-briefing` v2 apply (`abd385f8`) — there was nowhere to paste it. **Decision (user):** don't track the
  SHA by hand. It goes stale the moment the source repo takes its next commit (it pins *the applying commit*,
  not current source state), it duplicates provenance we already hold (source git history + this spec's
  `backport/` ledger), and manual entry is skip-/typo-prone. The SHA earns its keep only under **[3.1]**
  (wired-in source write), which can capture it **automatically and freshly**. **Resolution:** leave the
  optional `commitSha` field in domain/API (harmless; 3.1 populates it), build **no** manual input now, and
  correct the runbook Step 9/10 to stop instructing a SHA paste. → *home: decision logged; auto-capture is
  [3.1]'s when the write is wired in. No UI work in 5.1.*
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
- **F4 — Auto-repeat a run N times (variance-native runs).** Gauging run-to-run noise (R4 / spec 2.14's
  "one run lies") today means manually re-triggering the *same* `version × dataset × scorers` run 2–3× and
  reading the **Variance view**. A **run-count** control on the run form ("run this N times") that fires N
  runs in one action and feeds the variance aggregation directly would make **stability a first-class,
  one-click operation** instead of hand-repetition. Surfaced walking `golf-dna` — the JSON-fence defect is
  **stochastic** (v1 2/5 fenced; v2 run 1 = 0/5), so a single run can't confirm a fix; you *must* repeat.
  **Couplings:** it's the producer half of **[2.14](../2.14.md)** (the variance consumer already shipped),
  and it depends on **[2.17](../2.17.md)** async runs (N sequential runs multiply wall-clock and blow past
  the sync timeout — R1). Design notes: pick a small N (e.g. 3/5/10), surface progress, one aggregated result.
  → *home: **[2.17](../2.17.md)** — folded into async runs' scope (user 2026-07-22); async is the enabler.*
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

## Eval methodology — backport recommender (2026-07-20, tool-native backport walk)
- **R9 — The backport-target recommender blends across subject models (R5 confound, now in the "ship this"
  engine).** Walking `round-debrief`'s in-tool backport: Current = **v1** (`claude-sonnet-4-6`, Golf's real
  model), and the Deployment card badges **`Backport target` = v2** — but **v2 ran on `claude-sonnet-5`**
  (v2/v3/v4 = Sonnet 5; v1/v5/v6/v7 = Sonnet 4.6, confirmed from the Version-history model chips). v2's
  Sonnet-5 scores (sparse ~0.90) outrank the honest real-model winner **v7** (Sonnet 4.6, ~0.84), so the
  tool recommends shipping a version whose "win" is **the model, not the prompt** — the exact ~4×
  overstatement **R5** documented, now steering the backport recommendation itself.
  **Root cause:** `VersionStatusHandler` keys every series off **(dataset × `ScorerRef.Identity`)** — the
  subject model (`PromptVersion.TargetModel`) is **not** part of the key. It takes the latest run per version
  and compares raw means regardless of which model produced them. **2.9** closed the *scorer-config/rubric*
  confound (a stale-rubric version can't win); the **subject-model** confound was never closed for the
  deployment/backport engine — **R5**'s fix (2.12) only added add-version model-default+warn and a
  cross-model flag in **Analytics/Compare**, *not* in `VersionStatusHandler` / the Deployment card.
  **Impact:** (a) the recommendation can crown a spuriously-higher cross-model version; (b) worse, once *any*
  stronger-model version exists in history, *every* real-model `Current` is **perpetually** nagged to backport
  to it (the cross-model score always "wins"), so the card can **never** reach a clean "nothing beats
  Current → done." This blocks round-debrief from ever showing an honest recommendation and would silently
  mislead any future prompt whose history spans models. **Fix:** hold the subject model constant in
  eligibility + target selection — only compare versions sharing `Current`'s subject model (exclude or flag
  cross-model series), the sibling of the same-scorer-config rule; add a cross-model warning on the
  Deployment card. **The fill sheet's earlier prediction that "2.9 picks v7 over v2" was wrong** — 2.9 holds
  the *scorer config* constant, not the *model*. → *home: **[2.9a](../2.9a.md)** — folded in + **R9 slice
  SHIPPED 2026-07-20 (`c362be7`)**: eligibility/target now hold the subject model constant + a cross-model
  card warning. **Verified live on dev** (`2ccc27f`): round-debrief's card now shows **no target** (the v2
  mis-pick gone; v7 is top among Sonnet-4.6 versions) + **⚠ 3 cross-model versions excluded**. The deliberate
  cross-model-comparison counterpart → new spec **[1.21](../1.21.md)**.*

## UX / consistency (2026-07-22, golf-dna walk)
- **U17 — Add/reveal forms stay open after a successful submit — revisit the 2.4 decision.** After
  **`Add version`** (workspace), **`Add test case`** and **`Add scorer`** (dataset) succeed, the reveal form
  stays open (fields clear, but `showCapture`/`showAddScorer`/the add-version toggle are **not** reset to
  closed — `dataset-detail.ts` success paths reset fields only; the `set(false)` lives in the *cancel* paths).
  This is
  **working as designed** — 2.4 explicitly chose "creation forms reveal behind `+` toggles **that stay open
  after submit**" (web/CLAUDE.md) so you can add several in a row. **Operator feedback (2026-07-22):** it reads
  as clutter — the add surface looks "always present," and for **add-version** the content is pre-seeded from
  the latest version (U11), so an open post-submit form invites an accidental duplicate version. Requested
  behavior: **collapse back to the summary/`+` after a successful add** (revealed again on `+`); if a
  rapid-multi-add affordance is still wanted, make it explicit ("Add another"). Not a bug — a deliberate-UX
  reversal, and it applies uniformly to **all three** reveal forms (version / test case / scorer). Observed
  registering `golf-dna` v1 + F1 + scorers. → *home: **[2.23](../../2.23.md)**.*

- **U18 — Add-test-case `Origin` defaults to `Captured` — manual entry mislabels as real capture.** The
  add form's `fixtureOrigin` signal (`dataset-detail.ts` ~L817) initialises to `'Captured'` and resets to
  `'Captured'` after each add (~L1004/L1205); the form is literally the capture form (`data-testid="capture"`).
  So an inattentive hand-add stamps a synthetic case `Captured`. This is **U8 landed incompletely** — 2.8 added
  the Origin selector but left the default on the value that's *wrong* for the common manual-entry path.
  Hit registering `golf-dna` F1 (synthetic) as `Captured`. Fix: default **`Synthetic`** (manual entry is
  hand-written by definition), or no default (force a pick). → *home: **[2.23](../../2.23.md)**.*
- **U19 — A wrong `Origin` is unrecoverable without deleting the whole dataset.** `Origin` is immutable
  (2.8: input/origin/seed fixed; `editFixture` patches only label/description) **and** there is **no
  single-test-case delete** in the web client (`datasets-api.service.ts` exposes `deleteDataset` only) — so
  fixing U18's mislabel means nuking the entire dataset + every case in it. `Origin` is provenance metadata,
  **not** part of score identity (`Prompt × Version × Dataset × Scorer`), unlike `input`/`seed` — so making it
  editable is defensible on its own terms. Fix (chosen): **per-test-case delete** (delete + re-add), keeping
  `Origin` immutable per the domain. → *home: **[2.23](../../2.23.md)**.*
- **U20 — Test-case `Prompt input` textarea is 2 rows — too short; and it should NOT be markdown.** The
  input (and `Upstream SLM output` / `Expected output`) render `<textarea rows="2">` (`dataset-detail.ts`
  ~L234/244/254) — cramped for a multi-section JSON block. Make it **taller / auto-growing, monospace**
  (JSON pretty-print/validate would help). **Not** the 2.10 MarkdownEditor: the input is *data* (JSON for
  golf-dna; serialized input generally), not prose — markdown would mangle it, which is exactly why 2.10
  gated the editor to Content/Rubric and kept Regex/JsonSchema configs plain. → *home: **[2.23](../../2.23.md)**.*

- **U21 — Eval-run per-test-case scorer badges render in an unstable order (varies row to row).** On the
  run page each test-case summary row lists its per-scorer badges in a **different order** (observed on
  `golf-dna` baseline: `JsonSchema·LlmJudge·Regex`, `LlmJudge·Regex·JsonSchema`, `LlmJudge·JsonSchema·Regex`,
  `Regex·LlmJudge·JsonSchema`, … across the 5 rows). Almost certainly rendered from an unordered map of
  scores (keyed by scorer id) with no sort. Kills at-a-glance scanning — your eye can't track "the JsonSchema
  column" down the list, which is exactly how the 2/5 JSON failures were easy to miss. Fix: sort badges by a
  **stable key** on every row (scorer kind, or scorer creation order / `ScorerRef.Identity`) so the columns
  line up. → *home: **[2.23](../../2.23.md)**.*

- **U22 (minor) — `Prepare backport → Copy exact prompt` omits the trailing newline.** The copied artifact
  is byte-identical to the version content but has **no EOF newline**; pasted straight into a source file that
  conventionally ends with one (Golf's `golf-dna.md` does), it adds a spurious "\ No newline at end of file"
  to the diff. Likely the stored version content is trimmed. Low priority. Caught verifying golf-dna v2 (used
  the newline-correct staged `backport/golf-dna.md` instead). → *home: **TBD** (1.20 `Prepare backport` follow-up).*

## Ops / infra
- **O1 — Dev deployed without the Anthropic key set.** Provisioning shipped the secret as a placeholder;
  the first eval was the first thing to exercise it. The next environment shouldn't repeat this — add a
  post-deploy check that a real key is present. → *home: 3.2 / infra runbook*
