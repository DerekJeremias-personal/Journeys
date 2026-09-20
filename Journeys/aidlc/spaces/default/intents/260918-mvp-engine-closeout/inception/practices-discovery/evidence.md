# Practices Discovery Evidence (lead integration)

Intent `mvp-engine-closeout`. Brownfield re-run. Conversation language: English. Interview Q1–Q6 confirmed Looks correct (2026-09-19). Support contributions from quality, developer, and devsecops integrated below.

## Inspected

| Source | What was used |
|---|---|
| Record state | `aidlc/spaces/default/intents/260918-mvp-engine-closeout/aidlc-state.md` — brownfield, classic, Inception, practices-discovery in progress, Practices Affirmed Timestamp empty |
| Org / team / project | `memory/org.md` (defaults); `memory/team.md` (five sections still comment placeholders); `memory/project.md` (affirmed 2026-09-14 — used as baseline) |
| CodeKB | `code-structure`, `technology-stack`, `dependencies`, `code-quality-assessment`, `architecture`, `business-overview`, `reverse-engineering-timestamp` (commit `55ba6be`, branch `feat/journeys-ux-loyalty-shell`) |
| Session / standards | `AGENTS.md`, `docs/developer/coding-standards.md`, `docs/platform/coding-standards.md`, `docs/developer/testing.md`, `docs/developer/local-ops.md`, `docs/developer/linear-aidlc-projection.md`, `docs/platform/runtime.md`, `docs/roadmap/non-goals.md`, `.cursor/rules/tests.mdc` |
| Ingested spec/plan | `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md`, `docs/plans/2026-09-18-Journeys-mvp-engine-closeout.md` (ingest only; not re-brainstormed) |
| Git | Repo root `C:\Dev\Journeys`. `.git/HEAD` → `feat/journeys-ux-loyalty-shell`. `.git/logs/HEAD` and branch log. `packed-refs` `main` = `b951763`. Current feat tip `55ba6be` ("next slice"). Parent `df022fb` ("stage 1"). Branch created from `3e3ae18` ("corrections"). `git log --oneline -15` could not be executed (preToolUse blocked the shell). |
| CI / IaC | No `.github/` under `Journeys/Journeys`. No `azure-pipelines.yml`. Parent `C:\Dev\Journeys\.github` not present at the paths tried. No terraform / Databricks folders (CodeKB + non-goals). |
| Interview | `practices-discovery-questions.md` — Q1 B, Q2 A, Q3 B, Q4 A, Q5 A, Q6 A; consolidated summary Looks correct |
| Support contributions | `contributions/aidlc-quality-agent.md`, `contributions/aidlc-developer-agent.md`, `contributions/aidlc-devsecops-agent.md` (read-only; not rewritten) |

## Interview decisions

1. **Q1 Way of Working — B.** Accept longer `feat/*` branches when a slice needs more than a couple of days; still squash to `main`. Humans merge.
2. **Q2 Walking Skeleton — A.** Skip a second skeleton ceremony; the API + Core spine already exists.
3. **Q3 Testing — B.** Methodology `custom` — TDD on new closeout seams; existing historical tests stay test-after.
4. **Q4 Coverage — A.** No in-repo floor; keep the suite green; run `scripts/aidlc-agent-verify-sensor.ps1` with `-RunTests` when tests change.
5. **Q5 Deployment — A.** This tree has no CD; humans merge to `main`; deploy/release stays human-gated outside this folder. Org deploy-on-merge is not affirmed.
6. **Q6 Scanning — A.** Scanning is absent here; do not invent in-tree scanners this increment.

## Spoke OBJECTs accepted vs declined

### Accepted

- **Quality:** Methodology must be spoken. Affirmed as `custom` (Q3 B), not unlabeled `tdd`.
- **Quality:** Do not adopt an 80% (or any) coverage floor, `coverlet.runsettings`, or a new CI coverage job. Q4 A keeps suite-green + verify.
- **Developer:** Retag `APIErrorsException` / `GlobalExceptionMiddleware` from **[Inferred]** to **[Affirmed]** (already in both coding-standards docs).
- **Developer:** Add Code Style bullets for `Journeys.Core.Models` internal to Core; assembly `Journeys.DTO` vs platform `Journeys.Dto` (no second project); HTTP/DTO camelCase vs persist-lowercase Backend symbols; enums as strings (`JsonStringEnumConverter`); outcome `Kind` discriminators not `$type`; thin `Program.cs` + extensions; Scoped default; `CancellationToken` last with `default`.
- **Developer / DevSecOps:** No new formatter, `.editorconfig`, or `Directory.Build.props` this increment.
- **DevSecOps:** No in-tree scanners and no CD in this tree. Do not promote org “linter in CI blocks the PR,” SAST/DAST, secret scanning, NuGet/SBOM audit, or deploy-on-merge.

### Declined

- **Quality:** Always pass `-RunTests` on every Construction verify that touches engine seams. Interview Q4 chose when-tests-change, not always-on-engine-seams.

### Construction facts (not rules)

These stay out of `discovered-rules.md` and out of durable ALWAYS/NEVER:

- PointLifecycle cases that assume UtcNow-reset must be rewritten to the earn-date formula (approved plan task).
- `TestDataFactory.GetSpendablePointAccount()` uses a 30-day Spendable lifespan; lifecycle assertions must set 365 or assert EarnDate + configured days.
- Existing `ExpirePointsOutcomeTests` / root-only `RuleServiceTests` / `UserPointsTests` do not lock dest ExpirationDate, child-node hydrate, or ProcessEvent bring-current order.

## Increment constraints kept out of discovered-rules

Human-stated for this closeout only: no hosted sweep, no `/points/expire`, no UX product code, no Award signature change, no email/Twilio/data_pipeline adapters, no MassTransit process-event republish, no `PointLedgerTypeStrings` redesign, no new capability ids. These are spec/plan non-goals, not durable team ALWAYS/NEVER.

## Remaining uncertainty

None blocking. HintPath `Backend.*` binaries may be reviewed or pinned outside this tree; that is noted and is not a new rule. Full `git log --oneline -15` was blocked at draft time; the feat-branch story did not change after the interview.
