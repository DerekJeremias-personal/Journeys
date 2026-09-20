# Discovered Rules

Hard constraints only. Already human-stated in `AGENTS.md`, `project.md` (2026-09-14), `.agents/skills/aidlc-journeys`, or the approved closeout spec/plan. Interview practices (longer `feat/*` branches, skip second skeleton, Methodology `custom`, no in-repo coverage floor, no CD, no in-tree scanners) stay in `team-practices.md`. Increment-only non-goals (no hosted sweep, no UX, no Award signature change) stay in evidence, not this file.

## Mandated

ALWAYS run scripts/aidlc-agent-verify-sensor.ps1 before completing Construction (already affirmed 2026-09-14)
ALWAYS keep TenantId on every business operation (already affirmed 2026-09-14)
ALWAYS land Linear unit issues — one issue per unit after units-generation — and wait for human -ApproveCreate before any Journeys.* product code (human-stated)
ALWAYS keep walking-skeleton and engine writes on the Core + DTO path, not a parallel MCP or Campaign Agent write path (human-stated)
ALWAYS treat auth, ledger/money-like outcomes, and tenant isolation as human-gated (human-stated)

## Forbidden

NEVER invent capability ids (already affirmed 2026-09-14)
NEVER treat aidlc/ artifacts as product canon (already affirmed 2026-09-14)
NEVER cap production reviews at none (already affirmed 2026-09-14)
NEVER use Express to dodge Inception or reviewers on production work (human-stated)
NEVER merge to main or release as an agent (human-stated)
NEVER add terraform or Databricks folders in this tree (human-stated)
NEVER run AWS/CDK platform stages unless the human names infrastructure work that belongs outside this sln (human-stated)
NEVER edit Auth0 tenant "hayward" (human-stated DECIDED)
NEVER log secrets, full event payloads, webhook auth headers, or raw audit JSON (human-stated in the approved plan)
