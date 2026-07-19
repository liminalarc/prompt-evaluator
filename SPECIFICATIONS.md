# LitmusAI — Specifications

> Index only. Each spec's detail is in `specs/<id>.md`. **Status here is the
> single source of truth** for lifecycle — edit it as work moves.

## Phase 0 — Foundation
- _All specs archived._

## Phase 1 — Core Evaluation Loop
- **1.8** Shared / Cross-Prompt Datasets (cloning) — `NOT STARTED` — [detail](specs/1.8.md)
- **1.12** Modal SLM provider adapter — `NOT STARTED` — [detail](specs/1.12.md)
- **1.14** Live model discovery (auto-sync the catalog from provider APIs) — `NOT STARTED` — [detail](specs/1.14.md)
- **1.15** Per-org model catalogs (per-organization customization) — `NOT STARTED` — [detail](specs/1.15.md)
- **1.16** Version status & backport lifecycle (deployed marker + backport-eligible signal) — `NOT STARTED` — [detail](specs/1.16.md)
- **1.17** Multimodal / image fixtures (vision-input prompts) — `NOT STARTED` — [detail](specs/1.17.md)
- **1.18** Tool-augmented evaluation (web_search / tool-loop prompts) — `NOT STARTED` — [detail](specs/1.18.md)

## Phase 2 — Review & Advisory
- **2.1** Human Review UI — `NOT STARTED` — [detail](specs/2.1.md)
- **2.2** Prompt-Engineering Advisory — `NOT STARTED` — [detail](specs/2.2.md)
- **2.3** Prompt Authoring UX — `NOT STARTED` — [detail](specs/2.3.md)
- **2.6** Adopt brand-tokens hero tokens (drop local stopgap) — `NOT STARTED` — [detail](specs/2.6.md)
- **2.7** AI Prompt Authoring Assistant (proactive, best-practice) — `NOT STARTED` — [detail](specs/2.7.md)
- **2.9** Weighted composite scoring (per-dataset scorer weights → one overall score) — `NOT STARTED` — [detail](specs/2.9.md)
- **2.10** Markdown editor (with preview) for markdown-bearing fields — `NOT STARTED` — [detail](specs/2.10.md)
- **2.12** Eval-loop round 3 — reliability & fair scoring (round-debrief dogfood findings) — `NOT STARTED` — [detail](specs/2.12.md)

## Phase 3 — Integrations & Ops
- **3.1** Zatomic-backed Prompt Registry — `NOT STARTED` — [detail](specs/3.1.md)

## Phase 4 — Accounts & Access
- **4.2** SSO / OAuth Sign-In — `NOT STARTED` — [detail](specs/4.2.md)

## Phase 5 — Dogfooding & Real-World Adoption
- **5.1** Adopt LitmusAI across Cortex Golf & Stormboard (discover → onboard → improve every prompt) — `IN PROGRESS` — [detail](specs/5.1/5.1.md)

## Archive
- **2.11** Cancel action on every reveal / expand-to-edit surface — `DONE` — [detail](specs/archive/2.11.md)
- **1.19** Catalog: add current Anthropic models (Sonnet 4.6, Opus 4.7, Opus 4.6) — `DONE` — [detail](specs/archive/1.19.md)
- **2.8** Eval-loop UX round 2 (dogfood findings) — `DONE` — [detail](specs/archive/2.8.md)
- **4.6** Admin-created users (create user, no email) — `DONE` — [detail](specs/archive/4.6.md)
- **3.2** Production Deployment (AWS App Runner/ECR/RDS dev env, Prism-modeled) — `DONE` — [detail](specs/archive/3.2.md)
- **4.5** Org-owner member management on the org page (owner-or-admin, member-scoped) — `DONE` — [detail](specs/archive/4.5.md)
- **4.4** Organization management (admin) — list / create / rename / delete orgs — `DONE` — [detail](specs/archive/4.4.md)
- **4.3** Admin user & access management (admin flag, org membership, passwords — no email) — `DONE` — [detail](specs/archive/4.3.md)
- **1.13** Model Catalog + admin management (droplists, no free-text model ids) — `DONE` — [detail](specs/archive/1.13.md)
- **1.5** Multi-Provider Model Support — `DONE` — [detail](specs/archive/1.5.md)
- **1.6** Prompt Import (file / bulk) — `DONE` — [detail](specs/archive/1.6.md)
- **3.3** Version display in the web UI + deploy-channel plumbing — `DONE` — [detail](specs/archive/3.3.md)
- **1.11** Regression flagging — small-sample handling & clearer messaging — `DONE` — [detail](specs/archive/1.11.md)
- **1.10** Deletion & lifecycle for registry entities — `DONE` — [detail](specs/archive/1.10.md)
- **2.5** Eval-loop UI/UX overhaul (dogfood findings) — `DONE` — [detail](specs/archive/2.5.md)
- **4.1** Authentication & Multi-User Access — `DONE` — [detail](specs/archive/4.1.md)
- **2.4** UX Overhaul — App Shell, Navigation & Design-System Foundation — `DONE` — [detail](specs/archive/2.4.md)
- **1.9** Organizations (top-level + permission boundary) + Prompts UX overhaul — `DONE` — [detail](specs/archive/1.9.md)
- **1.7** Prompt Grouping (folders) + Unified Prompt Workspace — `DONE` — [detail](specs/archive/1.7.md)
- **1.4** Score Tracking & Analytics — `DONE` — [detail](specs/archive/1.4.md)
- **0.2** Rename → LitmusAI — `DONE` — [detail](specs/archive/0.2.md)
- **1.3** Eval Harness — `DONE` — [detail](specs/archive/1.3.md)
- **1.2** Datasets & Fixtures — `DONE` — [detail](specs/archive/1.2.md)
- **1.1** Prompt Registry — `DONE` — [detail](specs/archive/1.1.md)
- **0.1** Walking Skeleton — `DONE` — [detail](specs/archive/0.1.md)
