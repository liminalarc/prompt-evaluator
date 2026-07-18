# 5.1 — Prompt Inventory & Catalog (T1 deliverable)

> Discovery output for spec 5.1. One row per prompt across Cortex Golf and Stormboard, with the
> fields onboarding (T2–T4) needs: purpose, model, input/output shape, call site, capture
> feasibility. **Reconciled 2026-07-18.**

## Reconciled counts

| App | `.md` prompts | Inline prompts | **Total** |
|---|---|---|---|
| Cortex Golf | 15 | 0 | **15** |
| Stormboard | 37 | 2 | **39** |
| **Total** | **52** | **2** | **54** |

The spec's sizing scan assumed **52** (`.md`-only). The true count is **54**: two Stormboard prompts
are hardcoded C# **system-prompt string literals**, not `.md` files, so a `.md`-only scan misses them.

## Completeness sweep (how we know nothing else was missed)

Five passes per repo (file-type, LLM-SDK call-site, prompt-language, other-project, loading mechanism)
plus a redundant second deep sweep each.

- **Cortex Golf — COMPLETE.** Anthropic is the sole LLM SDK, in one project (`AiService.WebApi`); its
  15 handlers map 1:1 to the 15 embedded `.md`. No inline prompts; no other-project/Python/Angular
  callers (CourseService + Python CLIs delegate over HTTP). Non-`.md` prompt-adjacent code is only
  user-message *assembly*, not standalone prompts.
- **Stormboard — 37 `.md` + 2 inline.** All 37 load via `FilePromptStore.GetPromptAsync("<name>")`.
  Two live system prompts are hardcoded C# literals bypassing that loader:
  `ClaudeWizardPromptEngine.cs:23` (`"wizard-prompts"`) and `CodebaseEndpoints.cs:1200`
  (`"asset-mapping"`). The latter sits in `StormBoard.WebApi`, not `StormBoard.Claude` — why a
  single-project scan skipped it; the redundant deep sweep caught it. **Both are a smell** (they should
  be `.md` under `FilePromptStore` like the other 37) — a Stormboard cleanup to fold into their backport.

**Model-id note:** ids below (`claude-sonnet-4-6`, `claude-haiku-4-5-20251001`) are the literal strings
each app sends today, recorded as-is. Confirm canonical/alias status via the `claude-api` skill if a run
needs exact resolution.

---

## Cortex Golf (15)

Repo: `C:\Development\code\Golfstat\golfstat` · Prompts: `server/src/AiService/AiService.WebApi/Prompts/*.md`
(embedded resources; each handler in `…/Services/` loads one 1:1). Model default `claude-sonnet-4-6`
via `ClaudeOptions`; only `daily-briefing` overrides (haiku).

| name | purpose | model | input shape | output shape | call site | capture feasibility |
|---|---|---|---|---|---|---|
| auto-map-centerline-detection | Trace per-hole tee→green GPS centerlines from a satellite image (+ optional layout/scorecard). | claude-sonnet-4-6 | **Vision**: satellite (+ layout) image + GPS bounds/pixel dims + scorecard text | JSON `{holes:[{holeNumber,centerline:[{lat,lng}]}],routingNotes,warnings}` | `Services/CourseMapPipelineService.cs` (~L331) | real — Esri tiles deterministically fetched from GPS bounds |
| course-layout-analysis | Identify hole features (green centers, tees, centerlines, bunkers) from a satellite image. | claude-sonnet-4-6 | **Vision**: satellite image + GPS bbox/pixel dims + optional scorecard | JSON `{holes:[{…,green,teeArea,bunkers,fairway,confidence}],warnings}` | `Services/AnalyzeCourseLayoutHandler.cs` (~L349) | real — same Esri-tile input path |
| daily-briefing | Write a 2-3 sentence personalized daily dashboard blurb from player stats. | **claude-haiku-4-5-20251001** | Structured player-stats JSON (via BriefingContextProvider) | Freeform text (75-100 words) | `Services/GenerateDashboardBriefingHandler.cs` (~L88) | real — stats snapshotted per player |
| facility-enrichment | Web-search authoritative facility metadata (address, phone, site) for a course. | claude-sonnet-4-6 (SearchModel; web_search tool) | Text: facility name + location hint | Strict JSON `{canonicalName,line1,city,…,confidence,notes}` | `Services/ClaudeFacilityEnricher.cs` (~L219) | mixed — trivial input, live-web output; nondeterministic |
| golf-dna | Classify a player into an archetype + 4 data-backed insight sentences. | claude-sonnet-4-6 | Player statistical profile JSON | JSON `{archetype,topStrength,biggestOpportunity,signaturePattern,improvementTrajectory}` | `Services/GenerateGolfDnaHandler.cs` (~L159) | real — from stored player stats |
| hole-notes | Write 2-3 caddie-voice notes on a player's tendencies on one hole. | claude-sonnet-4-6 | Per-hole scoring + optional shot-by-shot JSON | JSON array of 2-3 strings | `Services/GenerateHoleNotesHandler.cs` (~L117) | real — from player round/shot history |
| overview-map-registration | Register a course-overview/satellite image into per-hole GPS centerlines. | claude-sonnet-4-6 | **Vision**: one image + `bounds`, `expectedHoleCount`, optional scorecard | JSON `{holes:[{holeNumber,vertices|null,lengthYards,confidence}],overallConfidence,notes}` | `Services/ClaudeOverviewMapRegistrar.cs` (~L411) | real — image + bounds reproducible |
| per-hole-polygon-detection | Trace GPS polygons (green/tee/bunker/water/fairway) for one hole from an annotated satellite image. | claude-sonnet-4-6 | **Vision**: annotated satellite image + tee/green anchors + bounds | JSON `{polygons:[{type,label,vertices,confidence}],warnings}` | `Services/DetectHolePolygonsHandler.cs` (~L266) | real — annotated tiles generated deterministically |
| round-debrief | Post-round coaching narrative + a drills JSON block. | claude-sonnet-4-6 | Round data + career stats JSON | Prose (200-400 words) + `[DRILLS_JSON]…[/DRILLS_JSON]` array | `Services/GenerateDebriefHandler.cs` (~L157) | real — per-round data snapshotted |
| routing-hole-search | Web-search per-hole aerial/description evidence + overview-map URL. | claude-sonnet-4-6 (SearchModel; web_search/web_fetch, raw HttpClient) | Text: course name, hole count, optional bounds/site | JSON `{holes:[{holeNumber,sourceUrl,imageUrl,description,confidence,vertices:null}],…,overviewMapUrl}` | `Services/ClaudeRoutingHoleSearcher.cs` (~L559) | mixed — live-web tool loop; hard ground-truth |
| routing-review | Judge: merge multi-source routing evidence into canonical per-hole centerlines + verdict. | claude-sonnet-4-6 | **Multimodal**: OSM/candidate JSON (text) + image assets (vision) | JSON `{courses:[{holes:[{vertices,confidence,sources,reason}]}],verdict,verdictReason,confidence,concerns}` | `Services/ReviewRoutingHandler.cs` (~L205) | real — inputs are captured upstream pipeline artifacts |
| scorecard-extraction | Extract structured scorecard (courses/tees/holes) from HTML or scorecard images. | claude-sonnet-4-6 (MaxTokens 32768) | **Multimodal**: cleaned page HTML and/or scorecard image(s) | JSON `{facility,courses:[{teeSets:[{holes:[{par,yardage,handicap,…}]}]}],combinedNineStats,warnings}` | `Services/ExtractScorecardHandler.cs` (~L96) | real — scraped HTML/images captured; deterministically scorable |
| scorecard-review | Judge: reconcile multiple scraped extractions into one canonical scorecard. | claude-sonnet-4-6 | Source extractions + existing-course context JSON | JSON `{finalScorecard,courseDecisions,verdict,verdictReason,confidence,concerns}` | `Services/ReviewScorecardHandler.cs` (~L121) | real — inputs are upstream extraction artifacts |
| scorecard-search | Web-search pages/PDFs containing a full hole-by-hole scorecard. | claude-sonnet-4-6 (SearchModel; DI comment says "Haiku" but is stale) | Text: facility name + location, optional site | JSON `{candidates:[{url,confidence,hasStructuredData,hasImage,sourceDomain,notes}],…}` | `Services/ClaudeScorecardSearcher.cs` (~L365) | mixed — live-web dependent |
| stat-narratives | Write one-sentence verdict + tone per dashboard stat card. | claude-sonnet-4-6 | Player stats JSON | JSON map card→`{text,tone}` (tone ∈ strength/opportunity/neutral) | `Services/GenerateStatNarrativesHandler.cs` (~L137) | real — from stored player stats |

---

## Stormboard (39 = 37 `.md` + 2 inline)

Repo: `C:\Development\stormboard` · `.md` prompts: `server/src/StormBoard.Claude/Prompts/*.md`
(embedded resources loaded via `FilePromptStore`; the `.md` is the **system** prompt, the payload is the
user turn). No per-prompt model pinning — each engine hard-codes a `TaskComplexity` at its call site and
`ModelRouter.GetModel(...)` maps it: **Light → claude-haiku-4-5-20251001**, **Moderate/Heavy →
claude-sonnet-4-6**. Re-tiering a prompt means editing its engine.

| name | purpose | model (tier) | input shape | output shape | call site | capture feasibility |
|---|---|---|---|---|---|---|
| affinity-group | Narrate why a cluster of domains should be worked on together. | haiku (Light) | One affinity group's structural/coupling metrics JSON | Plain text, 2-3 sentences | `ClaudeNarrativeSummaryEngine.cs` (SummarizeAffinityGroupAsync) | real — computed git/structural metrics |
| behavioral-analysis | Extract behavioral insights (rules, state machines, data access) from method bodies. | haiku (Light) | Type summaries w/ method bodies + deps + optional priorContext | JSON `{behavioralInsights:[{typeName,memberName,kind,description,confidence}]}` | `ClaudeCodeAnalysisEngine.cs:76` | real — ingested codebases |
| big-picture-analysis | Analyze a Big Picture Event Storming board (cluster events, gaps, follow-ups). | sonnet-4-6 (Moderate) | Board JSON (stickies, connections, boundaries) | Large JSON (discoveredDomains, suggestedBoards, hotSpots, opportunities, gapsAnalysis) | `ClaudeAnalysisEngine.cs:43` (BoardType key) | real — user-authored boards |
| big-picture-inference | Infer missing connections/boundaries on a Big Picture board. | haiku (Light) | Board JSON (stickies, existing connections/boundaries) | JSON `{inferredConnections[],inferredBoundaries[]}` | `ClaudeCanvasInferenceEngine.cs:42` | real — user boards |
| branching-summary | Narrate git branching-strategy evolution + current risks. | haiku (Light) | Branching-strategy timeline + risk metrics JSON | Plain text, 2-3 sentences | `ClaudeNarrativeSummaryEngine.cs` (SummarizeBranchingAsync) | real — git-history metrics |
| canvas-inference | Generic (non-typed board) fallback: infer missing connections/boundaries. | haiku (Light) | Board JSON | JSON `{inferredConnections[],inferredBoundaries[]}` | `ClaudeCanvasInferenceEngine.cs:45` (default) | real — user boards |
| capability-consolidation | Merge/dedupe per-chunk capability lists into one hierarchy (chunked-discovery phase 2). | sonnet-4-6 (Moderate) | `{partialCapabilities[],totalTypeCount,chunkCount}` (template-interpolated) | JSON `{capabilities:[{name,description,category,mergedFrom[],namespaces[],subCapabilities[]}]}` | `ClaudeHierarchicalCapabilityEngine.cs:66` | real — upstream discovery output |
| capability-discovery | Identify distinct business capabilities from codebase structure. | sonnet-4-6 (Moderate) | repositoryName, typeSummaries, dependencySummaries, project/namespace names | JSON `{capabilities:[{name,description,category,complexity,involvedTypes[],involvedNamespaces[]}]}` | `ClaudeCapabilityDiscoveryEngine.cs:39` | real — ingested repos |
| characterization-tests | Generate characterization (pin-behavior) tests for legacy methods. | sonnet-4-6 (Moderate) | language, methods[] w/ bodies + deps + insights | JSON `{files:[{fileName,content,testCount}]}` (compilable source) | `ClaudeCharacterizationTestEngine.cs:40` | real — real method bodies + insights |
| codebase-analysis | Produce patterns, seams, inferred DDD concepts, behavioral insights. | sonnet-4-6 (Moderate) | Type summaries (members+bodies), dep summaries, metrics, names | JSON `{patterns[],seams[],inferredConcepts[],behavioralInsights[]}` | `ClaudeCodeAnalysisEngine.cs:40` | real — ingested codebases |
| codebase-qa | Answer architecture/modernization Qs grounded in prior analysis (RAG-style). | haiku (Light) | System prompt w/ `{{CODEBASE_CONTEXT}}` + multi-turn ConversationHistory | Freeform markdown (<500 words) | `ClaudeCodebaseQAEngine.cs:68` | mixed — context real; real user Qs sparse, synthetic fill |
| context-map-analysis | Analyze a Context Map board (integration recs, dependency risks, gaps). | sonnet-4-6 (Moderate) | Board JSON (BoundedContext/IntegrationPattern/Team/DomainEvent stickies) | Large JSON (integrationRecommendations, dependencyRisks, hotSpots, opportunities, suggestedBoards, gapsAnalysis) | `ClaudeAnalysisEngine.cs:43` | real — user boards |
| context-map-inference | Infer missing connections/boundaries on a Context Map board. | haiku (Light) | Board JSON | JSON `{inferredConnections[],inferredBoundaries[]}` | `ClaudeCanvasInferenceEngine.cs:43` | real — user boards |
| ddd-analysis | Full Event Storming analysis (bounded contexts, aggregates, command chains, ACLs, gaps). | sonnet-4-6 (Moderate) | Board JSON (full sticky taxonomy + optional parent/migration context) | Large JSON (boundedContexts, commandChains, contextRelationships, hotSpots, opportunities, suggestedBoards, aclSpecs, gapsAnalysis) | `ClaudeAnalysisEngine.cs:43` (default) | real — user boards |
| ddd-chat | DDD-coach conversational assistant over the current board. | sonnet-4-6 (Moderate) | System prompt w/ `{{BOARD_CONTEXT}}` + multi-turn ConversationHistory | Freeform markdown reply (<500 words) | `ClaudeChatEngine.cs:74` | mixed — board context real; user turns need synthetic fill |
| delivery-summary | Narrate team delivery-health metrics (DORA, bus factor, hotspot/dup churn). | haiku (Light) | Team stats + DORA class + churn shares JSON | Plain text, 2-3 sentences | `ClaudeNarrativeSummaryEngine.cs` (SummarizeDeliveryAsync) | real — git-history metrics |
| design-level-analysis | Analyze a Design-Level board (aggregate specs, invariants, state transitions, command validation). | sonnet-4-6 (Moderate) | Board JSON (Command/Invariant/StateTransition/AC stickies) | Large JSON (aggregateSpecs, commandValidations, hotSpots, opportunities, gapsAnalysis) | `ClaudeAnalysisEngine.cs:43` | real — user boards |
| design-level-inference | Infer missing connections/boundaries (+ suggested invariants) on a Design-Level board. | haiku (Light) | Board JSON | JSON `{inferredConnections[],inferredBoundaries[]}` | `ClaudeCanvasInferenceEngine.cs:44` | real — user boards |
| divergence-analysis | Compare a branch's structure vs baseline capabilities; flag New vs Diverged. | sonnet-4-6 (Moderate) | repositoryName, branchName, baselineCapabilities[], branch summaries | JSON `{capabilities:[{name,description,status:New\|Diverged,involvedTypes[],baseCapabilityName,divergenceSummary}]}` | `ClaudeDivergenceAnalysisEngine.cs:40` | real — branch vs base ingestion |
| domain-decomposition | Group capabilities/namespaces into 2-6 business domains with recursive subdomains. | haiku (Light) | Capabilities + descriptions + namespace names (text) | JSON `{domains:[{name,description,mappedCapabilities,subdomains[]}],summary}` | `ClaudeDomainDecompositionEngine.cs:41` | real — ingested capability/namespace inventory |
| executive-summary | Write a 2-3 paragraph VP/CTO narrative from combined assessment metrics. | haiku (Light) | Combined metrics JSON (truncated 50k chars) | Freeform plain text (<400 words) | `ClaudeNarrativeSummaryEngine.cs:42` | real — aggregated analysis metrics |
| facilitation | Review an event-storming board; suggest missing stickies, dups, patterns, connections. | haiku (Light) | Board state JSON (phase, allowedStickyTypes, stickies, connections, boundaries, recentChanges) | JSON array of typed suggestions (`[]` if none) | `ClaudeFacilitationEngine.cs:39` | real — live board state |
| hierarchical-capability-discovery | Derive top-level + sub-capabilities from type/dependency metadata. | sonnet-4-6 (Moderate) | `{typeSummaries,dependencySummaries,projectNames,namespaceNames, optional priorContext}` | JSON `{capabilities:[{name,description,category,namespaces,subCapabilities[]}]}` | `ClaudeHierarchicalCapabilityEngine.cs:40` | real — structural metadata |
| method-summary | Batch-summarize methods into one-sentence searchable summaries + side effects + domain terms. | haiku (Light) | `methods[{index,methodName,typeName,namespace,signature,body,complexity}]` | JSON array `[{index,methodName,typeName,summary,sideEffects,domainTerms}]` | `ClaudeMethodSummarizer.cs:60` | real — parsed method bodies |
| migration-analysis | Compare source vs target product capabilities (gaps, overlaps, net-new). | sonnet-4-6 (Moderate) | `{sourceProductName,sourceCapabilities[],targetProductName,targetCapabilities[]}` | JSON `{gaps[],overlaps[],netNew[],summary}` | `ClaudeMigrationAnalysisEngine.cs:39` | mixed — needs two analyzed products (upstream output) |
| modernization-plan | Generate a full modernization plan (strategies, phases, target arch, business case, risks). | sonnet-4-6 (Moderate) | Large JSON: patterns, seams, healthMetrics, inferredConcepts, behavioralInsights, projectGraph + optional extras | JSON `{strategies[],phases[],targetArchitecture{},businessCase{},risks[]}` | `ClaudeModernizationEngine.cs:40` | mixed — chained on upstream analysis outputs |
| module-comparison | Compare product modules vs a shared core (Extended/Replaced/New). | sonnet-4-6 (Moderate) | `{coreModuleName,coreCapabilities[],modules[{capabilities[]}]}` | JSON `{comparisons[],overlapGroups[],summary}` | `ClaudeModuleComparisonEngine.cs:39` | mixed — needs white-label multi-module analysis |
| persona-discovery | Discover customer/end-user JTBD personas + pain points from analysis. | haiku (Light) | Compact text: productName, patterns, seams, capabilities, insights, concepts | JSON `{personas:[{name,description,source,problems[],mappedCapabilities,mappedSeams,frequency,migrationSensitivity,modernizationBenefit}]}` | `ClaudePersonaDiscoveryEngine.cs:40` | mixed — chained on upstream analysis |
| product-backlog-generation | Convert aggregated multi-board DDD architecture into an epic/feature/story backlog. | sonnet-4-6 (Moderate) | Board contributions JSON (per-tier) or flat aggregate fallback | JSON `{epics:[{features:[{stories[]}]}]}` | `ClaudeBacklogEngine.cs:40` | real — from real board analyses |
| quality-summary | Summarize code-quality metrics in exactly 2-3 sentences (observations only). | haiku (Light) | Quality metrics JSON (50k trunc) | Freeform plain text (2-3 sentences) | `ClaudeNarrativeSummaryEngine.cs:30` | real — quality metrics |
| semantic-overlap | Group semantically equivalent capabilities across sources (branches/repos/sub-products). | haiku (Light) | Capabilities across sources (sourceName, capabilityName, description, types) | JSON `{groups:[{consolidatedName,rationale,members[]}]}` | `ClaudeSemanticOverlapEngine.cs:39` | mixed — needs multi-source analyzed set |
| strategy-synthesis | Synthesize one product-specific modernization strategy (play, carve-outs, target contexts, watch-outs). | sonnet-4-6 (Moderate) | Single-codebase analysis: capabilities, domains, affinity/coupling, hotspots, testedness | Strict JSON `{approach,carveOutTargets[],targetContexts[],watchOuts[]}` | `ClaudeStrategyEngine.cs:39` | mixed — chained on upstream computed analysis |
| team-topology | Recommend a 3-tier Team-Topologies structure from capabilities. | sonnet-4-6 (Moderate) | `{capabilities:[{category,subCapabilities}], optional seams[]}` | JSON `{strategyTeam,productTeams:[{deliveryTeams[]}],summary}` | `ClaudeTeamTopologyEngine.cs:39` | mixed — upstream capability output |
| testing-summary | Summarize test-coverage metrics in exactly 2-3 sentences (observations only). | haiku (Light) | Testing metrics JSON (optional assertionDensity; 50k trunc) | Freeform plain text (2-3 sentences) | `ClaudeNarrativeSummaryEngine.cs:39` | real — testing metrics |
| topology-detection | Classify a codebase into one of six architectural topologies w/ confidence + evidence. | haiku (Light) | `{projectNames,namespaceNames,directoryTree,branchNames,repositoryCount,totalTypeCount}` | JSON `{kind,confidence,evidence[],detectedModules[]?}` | `ClaudeTopologyDetectionEngine.cs:39` | real — structural metadata |
| type-summary | Batch-summarize code types into one-sentence summaries + responsibility classification. | haiku (Light) | `types[{index,typeName,namespace,kind,members,dependencies,body}]` | JSON array `[{index,typeName,summary,classification,domainTerms}]` | `ClaudeTypeSummarizer.cs:59` | real — parsed type metadata |
| ubiquitous-language | Build a DDD ubiquitous-language glossary (30-80 terms) bridging code + business. | haiku (Light) | `{capabilities[], optional inferredConcepts[], optional behavioralInsights[]}` | JSON `{entries:[{term,definition,category,boundedContext,sourceEvidence,aliases}],summary}` | `ClaudeUbiquitousLanguageEngine.cs:39` | mixed — chained on upstream analysis |
| **wizard-prompts** *(inline)* | Friendly consultant: from detected topology + metrics, produce a plain-English summary, next-step guidance, and follow-up questions. | haiku (Light) | Text: topologyKind, confidence, typeCount, repoCount, evidence signals, detected modules | JSON `{topologySummary,nextStepGuidance,suggestedQuestions[]}` | `ClaudeWizardPromptEngine.cs:23` (prompt), `:69` (call) | real — from real topology-detection output |
| **asset-mapping** *(inline)* | Map a codebase's non-code asset groups (templates, schemas, stylesheets) to the most specific business capability + domain. | haiku (Light) | Text: asset groups (dir/purpose/extensions/samples) + available capabilities + domains | JSON `{mappings:{<dir>:{capability,domain}}}` | `CodebaseEndpoints.cs:1200` (prompt), `:1246` (call) | real — from real ingested repos w/ non-code assets |

## Cross-cutting notes for onboarding (T2–T4)

- **Scorer by output family.** ~85% of the 54 emit strict JSON (parsers/post-validation exist) →
  deterministic/schema scorers first (parse-valid, field presence, enum/range). Freeform prose (Golf
  narratives, Stormboard's 6 summaries + 2 conversational engines) → LLM-judge. The Golf judge prompts
  (routing-review, scorecard-review; verdict ∈ AutoAccept/NeedsReview/Reject) → human-label agreement.
- **Capture-first works almost everywhere.** Only the 3 Golf web-tool prompts (facility-enrichment,
  routing-hole-search, scorecard-search) are eval-hostile (live web) → synthetic/frozen-tool fixtures.
  Golf vision prompts use reproducible Esri tiles; Stormboard consumes real boards / ingested-repo metadata.
- **Capture the *intermediate* artifact, not the final input,** for chained prompts (Stormboard persona,
  modernization, strategy, ubiquitous-language, team-topology, migration/module/semantic — fed upstream
  analysis output) and template-interpolated prompts (`capability-consolidation`, `ddd-chat`, `codebase-qa`
  — the on-disk `.md` isn't the final system prompt). The 3 comparison prompts need multi-product inputs
  (rare) → thinner real corpora.
- **Shared engines** mean several Stormboard "prompts" share one code path (`ClaudeAnalysisEngine`,
  `ClaudeCanvasInferenceEngine`, `ClaudeNarrativeSummaryEngine`, `ClaudeHierarchicalCapabilityEngine`) —
  fixtures can be keyed the same way.
- **Baseline on the source app's real model** (recorded per row) before testing improvements — mostly
  `claude-sonnet-4-6`, cheap/fast prompts on `claude-haiku-4-5-20251001`.
