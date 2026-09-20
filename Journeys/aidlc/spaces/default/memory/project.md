# Project-Level Rules

> Project-specific specialisation and corrections. Loaded after `org.md` and
> `team.md` as strict-additive guidance; contradictions with broader policy
> are rejected. Populated by practices-discovery and the self-learning loop.
>
> Use sparingly: most teams don't need a project layer. Reach for it
> only when this specific project needs stable, durable guidance beyond the
> team practice (for example, package-specific release checks or an additional
> regression suite for a legacy component).

## Way of Working

- Session reading order is `AGENTS.md`, then `docs/product/`, then platform, then path rules, then specs/plans.
- Product meaning lives in `docs/product/`. `aidlc/` is execution state only. Do not create a second graph.
- Do not invent capability ids.
- Plan non-trivial work with `/brainstorming` (spec + plan). Superpowers `writing-plans` SDD header is void; use `.agents/skills/journeys-plan-to-aidlc/SKILL.md`. Execute with `/aidlc classic`. Humans approve non-trivial design, merge, and release. Agents never merge to `main`.
- After spec/plan approval, the human says `execute` plus an intent name. `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic` in this chat (preflight; ingest spec/plan; do not re-brainstorm). Do not run Superpowers subagent-driven-development / executing-plans. Linear unit issues land before any `Journeys.*` code. If another intent is already active, ask; `--new-intent` may require a fresh chat.
- One intent per workflow. Load `AGENTS.md` plus the product/platform **slice** for the change, not the whole tree. Prefer `/aidlc compose` to drop AWS/CDK stages. Do not use Express or `--review none` to save tokens on production work. Linear `upsert` waits on human `-ApproveCreate`.
- Reviewers: shipped `architecture-reviewer`, `product-lead`, plus `quality` and `devsecops` on stages that dispatch them. Required receipts must pass. Do not use `--review none` on production features. Do not use Express to dodge Inception or reviewers on production work.
- Linear is a projection of AI-DLC units (see `docs/specs/2026-09-14-Journeys-linear-aidlc-projection-design.md` and `docs/developer/linear-aidlc-projection.md`). Do not poll Linear for work.

## Walking Skeleton

The walking skeleton must go through Core services and DTOs, not a parallel write path in MCP or CampaignAgent handlers.

## Testing Posture

- Before completing Construction, run `.\scripts\aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed). Fail closed.
- Prefer `docs/developer/coding-standards.md` and existing `Journeys.Tests` layout.

## Change Control

Strict for this project: non-trivial design needs a human-approved spec/plan. Auth, ledger/money-like outcomes, and tenant isolation stay human-gated.

## Deployment

- No terraform or Databricks folders in this tree.
- Skip AWS/CDK platform stages unless the human names infrastructure work that belongs outside this sln.

## Code Style

Follow `docs/developer/coding-standards.md` and `.cursor/rules`. Do not invent a second aesthetic.

## Tech Stack

net8.0 onion: `Journeys.DTO`, `Journeys.Core`, `Journeys.DAL`, `Journeys.Infra*`, `Journeys.API`, `Journeys.Agent`, `Journeys.Tests`. Backend.* DLLs are external HintPath assemblies.

## Decided

DECIDED: AI-DLC is the execution kernel; Journeys docs/product is canon (spec 2026-09-13, 2026-09-14)
DECIDED: v1 reviewers are architecture-reviewer, product-lead, quality, devsecops as shipped (Stage construction-workflow, 2026-09-14)
DECIDED: Auth0 tenant "hayward" is human-gated debt; agents must not change it (Stage security, 2026-09-13)

## Forbidden

NEVER invent capability ids (affirmed 2026-09-14)
NEVER treat aidlc/ artifacts as product canon (affirmed 2026-09-14)
NEVER cap production reviews at none (affirmed 2026-09-14)

NEVER invent capability ids (already affirmed 2026-09-14) (affirmed 2026-09-19)

NEVER treat aidlc/ artifacts as product canon (already affirmed 2026-09-14) (affirmed 2026-09-19)

NEVER cap production reviews at none (already affirmed 2026-09-14) (affirmed 2026-09-19)

NEVER use Express to dodge Inception or reviewers on production work (human-stated) (affirmed 2026-09-19)

NEVER merge to main or release as an agent (human-stated) (affirmed 2026-09-19)

NEVER add terraform or Databricks folders in this tree (human-stated) (affirmed 2026-09-19)

NEVER run AWS/CDK platform stages unless the human names infrastructure work that belongs outside this sln (human-stated) (affirmed 2026-09-19)

NEVER edit Auth0 tenant "hayward" (human-stated DECIDED) (affirmed 2026-09-19)

NEVER log secrets, full event payloads, webhook auth headers, or raw audit JSON (human-stated in the approved plan) (affirmed 2026-09-19)

## Mandated

ALWAYS run scripts/aidlc-agent-verify-sensor.ps1 before completing Construction (affirmed 2026-09-14)
ALWAYS keep TenantId on business operations (affirmed 2026-09-14)


ALWAYS run scripts/aidlc-agent-verify-sensor.ps1 before completing Construction (already affirmed 2026-09-14) (affirmed 2026-09-19)

ALWAYS keep TenantId on every business operation (already affirmed 2026-09-14) (affirmed 2026-09-19)

ALWAYS land Linear unit issues — one issue per unit after units-generation — and wait for human -ApproveCreate before any Journeys.* product code (human-stated) (affirmed 2026-09-19)

ALWAYS keep walking-skeleton and engine writes on the Core + DTO path, not a parallel MCP or Campaign Agent write path (human-stated) (affirmed 2026-09-19)

ALWAYS treat auth, ledger/money-like outcomes, and tenant isolation as human-gated (human-stated) (affirmed 2026-09-19)

## Scope Overrides

<!-- Custom scope rules for this project. -->

## Forbidden

<!-- Populated by practices-discovery affirmation gate. -->
<!-- Format: NEVER [behavior] (affirmed [date]) -->
<!-- Example: NEVER throw exceptions across service layer boundaries (affirmed 2026-05-17) -->

## Mandated

<!-- Populated by practices-discovery affirmation gate. -->
<!-- Format: ALWAYS [behavior] (affirmed [date]) -->
<!-- Example: ALWAYS use Result<T,E> for fallible operations in service layer (affirmed 2026-05-17) -->

## Corrections

<!-- Project-specific corrections from human feedback. -->
<!-- Format: NEVER/ALWAYS [behavior] (learned [date]) -->
