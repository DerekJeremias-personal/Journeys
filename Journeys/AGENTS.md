# Journeys — session contract

Journeys is a multi-tenant **program runtime**: customer-owned event models, journey graphs and declarative rules, pluggable outcomes. This folder is the onion .NET solution (`Journeys.sln`). A Next.js UI is **not** in this solution.

## Essential reading (in order)

Before code search, edits, or terminal work:

1. This file (`AGENTS.md`)
2. `docs/product/index.md` plus the taxonomy, ontology, and graph slice for the task
3. `docs/roadmap/` when the work is a feature (why now, non-goals)
4. `docs/platform/index.md` and `docs/platform/architecture.md`
5. Path rules under `.cursor/rules/` that match files you will touch
6. A design spec in `docs/specs/` and a plan in `docs/plans/` for any multi-file or non-trivial change

If an agent cannot find it from this door plus path rules, it is not operational knowledge.

## Instruction hierarchy

1. Explicit user instruction in the current turn
2. `AGENTS.md` + `.cursor/rules/`
3. `.agents/skills/` on this repo
4. Generic plugin skills (only when they do not conflict)
5. Default model behavior

## Human approval (binding)

- Always provide a plan for non-trivial code modifications for review and approval.
- Do not create or modify product code without expressed consent and approval of the plan.
- Do not delete existing comments without cause.

## Definition of done (agent)

Not “I added files.” Done means:

- Code builds for the blast radius (`scripts/agent-verify.ps1`)
- Required tests for touched seams
- `docs-impact` and `graph-impact` pass (docs/meaning updated **or** an explicit waiver)
- Ready for **human** merge — agents never merge or release

## Human-only authorities

- Intent and prioritization
- Approve non-trivial design
- Merge and release
- Risk surfaces: auth, ledger/money-like outcomes, tenant isolation

## Tools (outside this folder)

`..\tools\` (CampaignContextAudit) is not a second contract root. Read `docs/developer/tools.md` before changing it.

## Graph

Product meaning lives in `docs/product/graph/*.yaml` (canon). Neo4j is a future backend — see `docs/product/graph/neo4j-adapter.md`. Do not invent a second graph.
