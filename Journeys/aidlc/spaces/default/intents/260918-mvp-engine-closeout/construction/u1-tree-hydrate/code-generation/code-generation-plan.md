# Code generation plan — `u1-tree-hydrate`

US1.1 / AC1.1.1–AC1.1.3. Collect earn + nav rules across the journey tree. Keep the existing hydrate pipeline. No EventService or ledger change. Linear JOU-1 after this plan is approved, before any `Journeys.*` write.

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
| US1.1 / AC1.1.1 | Steps 3–5 — child earn RuleSets (historical and taxonomic) enter collect; TTL decay + RuleState load apply on a later event |
| US1.1 / AC1.1.2 | Steps 3–5 — nested NavConstraint trees collected; empty Rules does not throw |
| US1.1 / AC1.1.3 | Step 6 — existing root TTL / `RuleServiceTests` stay green; do not make them the new collect oracle |

Advisory Functional Design notes to honor in collect (not new stories): taxonomic members stay in the set (not Historical-only); empty collect still runs the existing pipeline.

## Steps

- [x] Step 1 — Claim JOU-1 after this plan is approved. Pull Linear copy. Do not edit `Journeys.*` until claimed. (US1.1)
- [x] Step 2 — Verify the existing xUnit runner and record the unit-scoped command in `unit-test-instructions.md`. Command: `dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~HydrateCollect` (plus the existing `RuleServiceTests` filter for the historical suite). (runner)
- [x] Step 3 — Red: add `Journeys.Tests/Services/HydrateCollectTests.cs` (5–8 tests) proving child earn collect, nested nav collect, empty Rules no-throw, and taxonomic collect. Fail because `HydrateState` still reads only `campaign.Journey.Rules`. (AC1.1.1, AC1.1.2)
- [x] Step 4 — Green: implement tree collect on `RulesService.HydrateState` via `JourneyNode` helpers. Collect every node’s earn RuleSets, every NavConstraint tree (including nested composites), and recurse Children. Skip empty Rules without throw. Do not invent a second TTL / decay / RuleState / upsert path. (BR1.1–BR1.4)
- [x] Step 5 — Refactor while green. Existing `JourneyNode.FlattenToRulesOfType` walks nav + children but not this node’s earn RuleSets — extend collect so earn + nav + children are all included. Do not change `EventService` or ledgers. (BR1.5)
- [x] Step 6 — Test-after: keep `Journeys.Tests/Services/RuleServiceTests.cs` and existing root historical TTL cases green. Do not retarget them as the collect oracle. (AC1.1.3)
- [x] Step 7 — No new API, repository, migration, frontend, or Docker. Config unchanged. (BR1.5)
- [x] Step 8 — Meaning docs / path-map only if collect changes product meaning; otherwise `docs-impact` / `graph-impact` waiver or same-change update. Write `source-manifest.json`, `code-summary.md`, `traceability.json`. Run `.\scripts\aidlc-agent-verify-sensor.ps1 -RunTests`. (docs)

## Out of this unit

- Earn-date cascade (U2), expire-on-process (U3), NotificationOutcome webhook (U4)
- New HTTP routes, locks, sweeps
- `Journeys.UX`
