# Code generation plan — `u3-expire-on-process`

US3.1 / AC3.1.1–AC3.1.3. After `TryLockAccount`, bring points current before navigation/rules so this event can spend a released hold. Linear JOU-3 after this plan is approved, before any `Journeys.*` write.

## Testing Contract

```json
{
  "version": 1,
  "methodology": "custom",
  "source": "team",
  "ordering": "TDD (failing `Journeys.Tests` case first) on new closeout seams (hydrate/collect-rules, cascade clock, expire-before-rules, NotificationOutcome Calculate/Award); historical tests stay test-after; then `scripts/aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed) before Construction is complete.",
  "scope": "classic",
  "test_strategy": "standard",
  "project_type": "brownfield",
  "applicable_notes": [
    {
      "layer": "org",
      "text": "We treat tests as a first-class deliverable in every Bolt. The specific\nmethodology (TDD, BDD, ATDD, or classic test-after) is affirmed at\npractices-discovery and recorded in `team.md` under this heading with explicit\n`Methodology` and `Ordering` fields; Code Generation resolves those fields\nindependently from coverage, tooling, and scope notes.\n\nWhen no posture has been affirmed, our default per scope is:\n- **Methodology**: test-after\n- **Ordering**: implement each applicable testable layer, then write and run\n  that layer's tests.\n- `mvp`, `enterprise`, `feature`, `infra`, `classic` add an 80% line-coverage\n  floor and CI execution before merge.\n- `bugfix`, `security-patch` add a targeted regression for the specific\n  bug/vulnerability and require the existing suite to remain green.\n- `express` uses the Minimal strategy: requirement-driven unit tests (one per\n  requirement, with a happy-path floor per component); existing tests remain\n  green.\n- `poc`, `refactor`, `workshop` add no extra new-test floor and require the\n  existing suite to remain green.\n\nThe active `Test Strategy` still applies in every scope and determines test\nvolume/types. Scope floors are additive; they never reduce or replace the\nselected strategy.\n\nBuild and Test verifies defined coverage floors and affirmed quality targets;\nthey may not be weakened to make a step pass.\n\nAffirm a stricter posture in `team.md` if the team commits to one."
    },
    {
      "layer": "team",
      "text": "- **Methodology**: custom\n- **Ordering**: TDD (failing `Journeys.Tests` case first) on new closeout seams (hydrate/collect-rules, cascade clock, expire-before-rules, NotificationOutcome Calculate/Award); historical tests stay test-after; then `scripts/aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed) before Construction is complete.\n- **[Affirmed]** Construction verify is `scripts/aidlc-agent-verify-sensor.ps1` (wrapper over `scripts/agent-verify.ps1` plus docs-impact / graph-impact). Fail closed. Prefer `docs/developer/coding-standards.md` and the existing `Journeys.Tests` folder layout (RulesEngine, Services, Mcp, …) with Arrange–Act–Assert.\n- **[Affirmed]** No in-repo coverage floor. Keep the existing suite green. Add `-RunTests` when tests change; do not require `-RunTests` on every engine Construction verify.\n- **[Affirmed]** Keep tests in `Journeys.Tests` only. No second test project. No BDD feature files. UX Vitest is out of increment. `Journeys.Tests` must not reference `Journeys.Notification`; fake `INotificationService` on `RulesEngineState` (same injection style as `loyaltyAccountService`). Do not extend the `RulesService` constructor for that fake.\n- **[Inferred]** Tooling is xUnit 2.9.3 + coverlet.collector 6.0.3. No `coverlet.runsettings` and no coverage-floor file. Coverlet stays a collector, not a gate."
    },
    {
      "layer": "project",
      "text": "- Before completing Construction, run `.\\scripts\\aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed). Fail closed.\n- Prefer `docs/developer/coding-standards.md` and existing `Journeys.Tests` layout."
    }
  ],
  "obligations": {
    "strategy": "standard",
    "strategy_volume": [
      "Five to eight tests per component.",
      "Unit tests plus integration tests for key boundaries.",
      "Add E2E, performance, or security tests when requirements demand them."
    ],
    "scope_floor": [
      "Keep the existing test suite green.",
      "This scope adds no extra new-test floor beyond the selected test strategy."
    ],
    "combination_rule": "Apply every selected-strategy obligation and every scope-floor obligation; neither replaces the other, and a targeted scope regression may add the narrowest necessary test type beyond the strategy default."
  },
  "plan_profile": {
    "methodology": "custom",
    "runner_step": "Verify the existing test runner/configuration and record the exact unit-scoped command.",
    "runner_ready_before_first_test": true,
    "testable_layers": [
      "Data model / database behavior",
      "Repository / data access",
      "Business logic",
      "API / endpoint",
      "Frontend behavior"
    ],
    "steps": [
      "Project structure and production configuration skeleton.",
      "Verify the existing test runner/configuration and record the exact unit-scoped command.",
      "Custom ordering - TDD (failing `Journeys.Tests` case first) on new closeout seams (hydrate/collect-rules, cascade clock, expire-before-rules, NotificationOutcome Calculate/Award); historical tests stay test-after; then `scripts/aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed) before Construction is complete.",
      "Implementation and tests - preserve that exact ordering; do not convert it to layer-local TDD.",
      "Environment/build configuration.",
      "Documentation and traceability."
    ]
  },
  "input_sha256": "sha256:9440a83d78394dec5e1e60f915c637023acf29630ec372dd845823fa634c2b0c",
  "contract_sha256": "sha256:973a78f469c2cec2626b55912fbe615ca7af904b98bd0de54c2627f930d956b8"
}
```

## Story map

| Story / AC | Plan steps |
|------------|------------|
| US3.1 / AC3.1.1 | Steps 3–4 — after `TryLockAccount`, `BringLoyaltyAccountPointsCurrentInternalAsync` then `AttachNotificationPort` then `ProcessRulesAsync`; assign `PointLedgers` |
| US3.1 / AC3.1.2 | Steps 3–4 — same `TryLockAccount` lease + retries; no new lock, queue, sweep, or `/points/expire`; GET/reconcile callers untouched |
| US3.1 / AC3.1.3 | Steps 3–4 — bring-current throw after lock fails the event (`ProcessRulesAsync` not called); skip remains populate / `!bOk` / pre-save (no new call in `ProcessEventInternalAsync`) |

U4 webhook port: keep `AttachNotificationPort` additive. Insert bring-current **between** `TryLockAccount` and `AttachNotificationPort` in `ProcessCampaignsAsync` only. Do not drop the port copy. Do not add bring-current to `ResettleAccountInternalAsync`.

## Steps

- [x] Step 1 — Claim JOU-3 after this plan is approved. Pull Linear copy. Do not edit `Journeys.*` until claimed. (US3.1)
- [x] Step 2 — Verify the existing xUnit runner and record the unit-scoped commands in `unit-test-instructions.md`. Commands: `dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~ProcessCampaignsExpireOnProcess` and `--filter FullyQualifiedName~UserPointsTests`. (runner)
- [x] Step 3 — Red: add `Journeys.Tests/Services/ProcessCampaignsExpireOnProcessTests.cs` (5–8 Arrange–Act–Assert cases). Fake `ILoyaltyAccountService` and `IRulesService` on `EventService`. Mark `ProcessCampaignsAsync` `internal` if needed (`InternalsVisibleTo` already exists). Fail because `ProcessCampaignsAsync` still goes lock → `AttachNotificationPort` → `ProcessRulesAsync` with no bring-current. (AC3.1.1–AC3.1.3)
- [x] Step 4 — Green: in `EventService.ProcessCampaignsAsync` only, after a successful `TryLockAccount` and before `AttachNotificationPort` / `ProcessRulesAsync`, assign `loyaltyAccount.PointLedgers` from existing `_loyaltyAccountService.BringLoyaltyAccountPointsCurrentInternalAsync(tenantId, loyaltyAccount)`. Keep `AttachNotificationPort` immediately before `ProcessRulesAsync`. Do not call bring-current unlocked in `ProcessEventInternalAsync` (spec G2). Do not add a lock, queue, hosted sweep, or `/points/expire`. Do not edit `SaveLedgerExpirations` (C2 dest clock). Do not change GET/reconcile call sites (`EventService` ~428 / ~589) or `ResettleAccountInternalAsync`. Existing `catch (Exception)` rethrow already fails the event if bring-current throws. (BR3.1–BR3.8)
- [x] Step 5 — Test-after: keep `UserPointsTests` green. Those GET/reconcile expire cases are not this unit’s oracle. (AC3.1.2, BR3.8)
- [x] Step 6 — Meaning docs in the same change: `docs/product/use-cases/process-event.md` lock → bring-current → rules; `docs/product/ontology/loyalty-account.md` expire-on-process order; `docs/platform/architecture.md` / `runtime.md` only if the standing ProcessEvent order would otherwise be wrong. `docs-impact` / `graph-impact` in the same change. No new HTTP, MCP row, or `Journeys.Notification` claim. (US3.1)
- [x] Step 7 — Write `source-manifest.json`, `code-summary.md`, `traceability.json`. Run `.\scripts\aidlc-agent-verify-sensor.ps1` with `-Files` for claimed paths and `-RunTests` plus the unit-scoped filters (do not use a bare `dotnet test` of the whole project as this unit’s oracle). Do not complete Linear JOU-3 until a commit SHA exists.

## Out of this unit

- Tree hydrate (U1)
- Earn-date dest clock (U2; already generated — hops inside bring-current use it)
- NotificationOutcome webhook (U4; keep `AttachNotificationPort`)
- New HTTP routes, locks, sweeps, `/points/expire`
- `Journeys.UX`
- GET / reconcile expire rewrite
- Completing Linear JOU-3 without a commit SHA
