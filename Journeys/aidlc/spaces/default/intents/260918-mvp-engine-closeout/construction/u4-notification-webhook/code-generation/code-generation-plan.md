# Code generation plan — `u4-notification-webhook`

US4.1 / AC4.1.1–AC4.1.9 and US5.1 / AC5.1.1–AC5.1.3. Live RuleSet `NotificationOutcome` POSTs the tenant `rest_api` webhook. MCP `NotificationConfigId` row. Meaning docs in the same change. Linear JOU-4 after this plan is approved, before any `Journeys.*` write.

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
| US4.1 / AC4.1.1 | Steps 3–4 — Award sends closed `NotificationOutcomePayload` for this tenant and that tenant's config only |
| US4.1 / AC4.1.2 | Steps 3–4 — Calculate null on missing id, missing/inactive config, EventModelId mismatch, or config not found for this tenant |
| US4.1 / AC4.1.3, AC4.1.9 | Steps 3–4 — send false → `IsAwarded` false; award loop does not force true; siblings kept |
| US4.1 / AC4.1.4, AC4.1.7 | Steps 3–4 — send throw fails ProcessEvent unless `Journeys:NotificationOutcome:TreatSendThrowAsFalse` is on (then same as send false) |
| US4.1 / AC4.1.5 | Steps 3–4 — CalculateOnly never Awards or POSTs |
| US4.1 / AC4.1.6 | Step 5 — MCP `MatrixVersion` 2026-09-18 + `NotificationConfigId` critical row; do not retarget 2026-06-20 pin fixtures |
| US4.1 / AC4.1.8 | Step 4 — `AddNotificationsServices` always registers `INotificationService` (drop `DisableDataLake` gate) |
| US5.1 / AC5.1.1–AC5.1.3 | Step 6 — ontology, process-event, path-map `Journeys.Notification` `meaningOptional: false`; verify scripts |

## Steps

- [x] Step 1 — Claim JOU-4 after this plan is approved. Pull Linear copy. Do not edit `Journeys.*` until claimed. (US4.1, US5.1)
- [x] Step 2 — Verify the existing xUnit runner and record the unit-scoped commands in `unit-test-instructions.md`. Commands: `dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~NotificationOutcomeTests` and `--filter FullyQualifiedName~RulesEngineMcpContractSummaryTests`. (runner)
- [x] Step 3 — Red: add `Journeys.Tests/RulesEngine/Outcomes/NotificationOutcomeTests.cs` (8 Arrange–Act–Assert cases). Fake `INotificationService` on `RulesEngineState`. Do not reference `Journeys.Notification`. Do not extend the `RulesService` constructor. Fail because Calculate/Award still return null and the award loop still sets `IsAwarded = true`. (AC4.1.1–AC4.1.5, AC4.1.7, AC4.1.9)
- [x] Step 4 — Green: add closed `NotificationOutcomePayload` under `Journeys.DTO/Models/`. Add `RulesEngineState.NotificationService` and copy it in `NavigatePayload` the same way as `LoyaltyAccountService`. Production host/`EventService` sets the port on state; do not add it to the `RulesService` constructor. Calculate: required `NotificationConfigId`; load via `GetNotificationConfigAsync(state.TenantId, configId)`; missing/inactive/EventModelId mismatch → null. Happy Calculate returns `IsAwarded` false. Award: `SendNotificationAsync(tenantId, configId, payload)` with the closed field list only. Send true → `IsAwarded` true. Send false → `IsAwarded` false. Send throw → fail ProcessEvent unless `Journeys:NotificationOutcome:TreatSendThrowAsFalse` is copied onto state (`TreatNotificationSendThrowAsFalse`); then treat as send false. Stop forcing `IsAwarded = true` in `ProcessJourneyAsync`. Drop the `DisableDataLake` gate in `AddNotificationsServices`. Do not change the Award signature. No email/Twilio. No new Kind. (BR4.1–BR4.6, BR4.8, BR4.9)
- [x] Step 5 — Test-after: bump `RulesEngineMcpContractSummary.MatrixVersion` to `2026-09-18` and add the `NotificationConfigId` critical row. Update `RulesEngineMcpContractSummaryTests` only. Do not retarget workflow pin fixtures that embed `2026-06-20`. Deposit/spend rows stay. (AC4.1.6, BR4.7)
- [x] Step 6 — Meaning docs in the same change: `docs/product/ontology/outcome.md`, `loyalty-account.md`, `rule.md`, `docs/product/use-cases/process-event.md`; `docs/product/graph/path-map.yaml` prefix `Journeys.Notification` `meaningOptional: false`; `docs/platform/runtime.md` only if the DI note would otherwise be wrong. `docs-impact` / `graph-impact` in the same change. (US5.1, BR5.1)
- [x] Step 7 — Write `source-manifest.json`, `code-summary.md`, `traceability.json`. Run `.\scripts\aidlc-agent-verify-sensor.ps1` with `-Files` for claimed paths and `-RunTests` plus the unit-scoped filters (do not use a bare `dotnet test` of the whole project as this unit's oracle).

## Out of this unit

- Tree hydrate (U1)
- Earn-date cascade (U2)
- Expire-on-process order (U3)
- New HTTP routes, locks, sweeps, email/Twilio adapters
- `Journeys.UX`
- `RulesService` constructor change
- Retargeting `2026-06-20` pin fixtures
