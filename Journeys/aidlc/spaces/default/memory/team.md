# Team-Level Rules

> This team's affirmed practices and corrections. Loaded after `org.md` as
> strict-additive guidance; contradictions with broader policy are rejected.
> Populated by the practices-discovery affirmation gate. Edit at the gate,
> not directly.

## Way of Working

- **[Affirmed]** Session reading order is `AGENTS.md`, then the product/platform slice, then path rules, then the approved spec and plan. Product meaning lives in `docs/product/`. `aidlc/` is execution state only. Do not create a second graph.
- **[Affirmed]** Plan non-trivial work with `/brainstorming` (spec + plan). Superpowers `writing-plans` SDD header is void; use `.agents/skills/journeys-plan-to-aidlc`. Execute with `/aidlc classic` after the human says `execute` plus an intent name. Do not re-brainstorm an ingested spec/plan. Do not run Superpowers subagent-driven-development or executing-plans.
- **[Affirmed]** Humans approve non-trivial design, merge, and release. Agents never merge to `main` and never release.
- **[Affirmed]** One intent per workflow. Reviewers are the shipped ensemble: `architecture-reviewer`, `product-lead`, plus `quality` and `devsecops` on stages that dispatch them. Required receipts must pass. Do not use Express or `--review none` on production work.
- **[Affirmed]** Linear is a projection of AI-DLC units. After `units-generation`, one Linear issue per unit; `upsert` waits on human `-ApproveCreate`. Do not poll Linear for work. Do not invent capability ids.
- **[Affirmed]** Accept longer `feat/*` branches when a slice needs more than a couple of days; still squash to `main`. Humans merge. Visible history (`feat/journeys-ux-loyalty-shell` off `main`: `3e3ae18` → `df022fb` → `55ba6be`) is feature branches to `main`, not GitFlow (`develop` / `release/*` absent).
- **[Inferred]** Remote named in developer docs: `DerekJeremias-personal/Journeys`. Git repo root is `C:\Dev\Journeys` (parent of this onion folder).

## Walking Skeleton

- **[Affirmed]** Any thin end-to-end slice must go through Core services and DTOs. Do not add a parallel write path in MCP or Campaign Agent handlers.
- **[Affirmed this increment]** `Journeys.UX` is out of increment. Notification adapters stay in `Journeys.Notification`. Controllers stay thin.
- **[Affirmed]** Skip a second walking-skeleton ceremony. The API + Core spine already exists: `Journeys.API` hosts process-event → Core (`EventService` / `RulesService` / outcomes) → DAL / Infra / Notification. This intent closes four Core gaps (hydrate, earn-date cascade, expire-on-process, `NotificationOutcome`); it does not stand up a new host.

## Testing Posture

- **Methodology**: custom
- **Ordering**: TDD (failing `Journeys.Tests` case first) on new closeout seams (hydrate/collect-rules, cascade clock, expire-before-rules, NotificationOutcome Calculate/Award); historical tests stay test-after; then `scripts/aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed) before Construction is complete.
- **[Affirmed]** Construction verify is `scripts/aidlc-agent-verify-sensor.ps1` (wrapper over `scripts/agent-verify.ps1` plus docs-impact / graph-impact). Fail closed. Prefer `docs/developer/coding-standards.md` and the existing `Journeys.Tests` folder layout (RulesEngine, Services, Mcp, …) with Arrange–Act–Assert.
- **[Affirmed]** No in-repo coverage floor. Keep the existing suite green. Add `-RunTests` when tests change; do not require `-RunTests` on every engine Construction verify.
- **[Affirmed]** Keep tests in `Journeys.Tests` only. No second test project. No BDD feature files. UX Vitest is out of increment. `Journeys.Tests` must not reference `Journeys.Notification`; fake `INotificationService` on `RulesEngineState` (same injection style as `loyaltyAccountService`). Do not extend the `RulesService` constructor for that fake.
- **[Inferred]** Tooling is xUnit 2.9.3 + coverlet.collector 6.0.3. No `coverlet.runsettings` and no coverage-floor file. Coverlet stays a collector, not a gate.

## Change Control

<!-- Affirmed by the team. Mode: strict or relaxed. Strict here holds for every intent and cannot be changed from chat. -->

## Deployment

- **[Affirmed]** No terraform or Databricks folders in this tree. Skip AWS/CDK platform stages unless the human names infrastructure work that belongs outside this sln.
- **[Affirmed]** Merge and release are human-only. Auth, ledger/money-like outcomes, and tenant isolation stay human-gated. Auth0 tenant `"hayward"` is human-gated debt; agents must not change it.
- **[Affirmed]** This tree has no CD. Humans merge to `main`. Deploy/release stays human-gated outside this folder. Do not affirm org deploy-on-merge.
- **[Affirmed]** Scanning is absent here. Do not invent in-tree SAST, DAST, secret, or dependency scanners this increment. Construction verify is not a security scanner.
- **[Inferred]** Environment topology: local (`dotnet` sln, user secrets / `appsettings.Local.json`, API typically `https://localhost:7001`) plus Azure-backed adapters (blob, Data Lake, Service Bus, Cosmos sagas) when connection strings are set. UX port 3000 exists but is out of increment.

## Code Style

- **[Affirmed]** Follow `docs/developer/coding-standards.md`, `docs/platform/coding-standards.md`, and matching `.cursor/rules`. Do not invent a second aesthetic. Do not reformat files you did not change. Do not delete existing comments without cause.
- **[Affirmed]** Onion: controllers validate and map HTTP; Core owns business rules; DAL is persistence; Azure / Auth / Backend / bus / blob live in `Journeys.Infra*`; Notification is adapters only (`rest_api`). Controller and service in/out types are `Journeys.DTO`.
- **[Affirmed]** `Journeys.Core.Models` stays internal to Core. Do not leak Core models across the HTTP/MCP boundary. New closeout contracts (for example `NotificationOutcomePayload`) go under `Journeys.DTO/Models/`.
- **[Affirmed]** Assembly and folder name is `Journeys.DTO`. Platform coding-standards writes `Journeys.Dto` for the same package — that is doc casing drift, not a second project. Do not create a `Journeys.Dto` namespace, folder, or csproj.
- **[Affirmed]** HTTP/DTO JSON is camelCase. Document id fields are `id`. Enums serialize as strings (`JsonStringEnumConverter`). Persist wrapper JSON keeps lowercase Backend symbols (`appliedcampaigns`, `outcomestates`, …); do not “fix” those to camelCase.
- **[Affirmed]** Outcome types use `Kind` discriminators (`OutcomeKindDiscriminators`), not `$type`. Nested providers may still use `$type`. Do not introduce `$type` on new outcomes.
- **[Affirmed]** File-scoped namespaces, nullable enable, implicit usings, net8.0, public async `...Async` returning `Task` / `Task<T>`, constructor injection, readonly `_camelCase` fields. Put `CancellationToken` last; default to `default` when callers may omit it.
- **[Affirmed]** Host composition stays in `Journeys.API`: keep `Program.cs` thin and call extension methods. Register request-scoped services as Scoped unless there is a clear Singleton or Transient reason. Controllers stay thin.
- **[Affirmed]** Do not take Azure SDK or Backend client dependencies in Core. Ports live in `Journeys.Core/Interfaces/` (`I*` consumed by Core). Implementations stay in DAL / Infra* / Notification adapters.
- **[Affirmed]** Two-phase outcomes stay `CalculateOutcomeAsync` then `AwardOutcomeAsync`. Do not rename or reshape that pair.
- **[Affirmed]** Validation uses existing `APIErrorsException` with `Dictionary<string, string>`. Unexpected errors go through `GlobalExceptionMiddleware`. Do not invent a new exception hierarchy or a Result type that the repo does not already use on these seams. Fail closed at the HTTP boundary; do not swallow exceptions in Core.
- **[Affirmed]** New production types follow existing folders: domain services in `Journeys.Core/Services/`; rules engine under `Journeys.Core/RulesEngine/` (Journey / Engine / Outcomes / Rules / Providers); DTOs in `Journeys.DTO/{Models,Requests,Responses}/`; notification adapters in `Journeys.Notification/Adapters/`. New tests live in `Journeys.Tests` and mirror the area folder with Arrange–Act–Assert.
- **[Affirmed]** Do not add `.editorconfig` or `Directory.Build.props` this increment. C# style is the coding-standards docs, not a second formatter config. Org Prettier/ESLint defaults apply only to `Journeys.UX` (out of increment).
## Forbidden

<!-- Team-specific forbidden patterns -->

## Mandated

<!-- Team-specific mandates -->

## Corrections

<!-- Self-learning loop appends here. -->
