# Code generation plan — `u2-earn-date-cascade`

US2.1 / AC2.1.1–AC2.1.6. PAT cascade keeps `EarnDate` and sets dest expiration from dest end-date, else earn + dest days, else unset. Human-gated ledger math. Linear JOU-2 after this plan is approved, before any `Journeys.*` write.

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
| US2.1 / AC2.1.1 | Steps 3–4 — hop keeps EarnDate; dest expiration is EarnDate + dest days when dest has days and no end-date |
| US2.1 / AC2.1.4 | Steps 3–4 — dest PointsLifespanEndDate wins over days |
| US2.1 / AC2.1.5 | Steps 3–4 — neither dest end-date nor dest days → dest expiration unset (no 100-day default) |
| US2.1 / AC2.1.3 | Steps 3–4 — missing / not-found dest PAT leaves the row in source |
| US2.1 / AC2.1.6 | Steps 3–4 — dest-PAT infra throw fails ProcessEvent |
| US2.1 / AC2.1.2 | Step 5 — already-cascaded UtcNow dest dates stay until they hop again |

Contract Design R-01 (Accepted risk): FR2.3 / BR2.4 neither clock → dest expiration unset. FR2.2 last-resort existing `ExpirationDate` nests under dest days only (BR2.5). Never `UtcNow` as the lifespan base.

## Steps

- [x] Step 1 — Claim JOU-2 after this plan is approved. Pull Linear copy. Do not edit `Journeys.*` until claimed. Ledger math is human-gated; this plan is the formula under review. (US2.1)
- [x] Step 2 — Verify the existing xUnit runner and record the unit-scoped command in `unit-test-instructions.md`. Command: `dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~LoyaltyAccountExpirationCascade` (plus `FullyQualifiedName~ExpirePointsOutcomeTests` for historical outcome cases). (runner)
- [x] Step 3 — Red: add `Journeys.Tests/Services/LoyaltyAccountExpirationCascadeTests.cs` (5–8 tests). Arrange dest PAT with 365 days — not the 30-day Spendable factory. Fail because `SaveLedgerExpirations` still uses `UtcNow` (and `?? 100`) as the move base. (AC2.1.1, AC2.1.3–AC2.1.6)
- [x] Step 4 — Green: change dest clock only inside `LoyaltyAccountService.SaveLedgerExpirations`. Keep EarnDate. Dest expiration: dest end-date if set; else EarnDate + dest days; else unset. If dest days exist and EarnDate is missing, use existing ExpirationDate as last resort; never UtcNow as lifespan base. Missing dest PAT: log, leave row in source, continue. Unexpected dest-PAT load/persist throw: do not swallow (fails ProcessEvent). Keep dest ledger move + ETag upsert. Do not change deposit-time EarnDate math. Do not redesign `PointLedgerTypeStrings`. (BR2.1–BR2.7)
- [x] Step 5 — Test-after: keep `ExpirePointsOutcomeTests` green with earn-date expectations; do not retarget them as the cascade oracle. Rows already written with the old UtcNow dest clock stay until they hop again. (AC2.1.2, BR2.8)
- [x] Step 6 — No new API, repository, migration, frontend, Docker, EventService, or hydrate change. U3 still owns bring-current order. (unit boundary)
- [x] Step 7 — Meaning docs: `docs/product/ontology/loyalty-account.md` earn-date dest clock. `docs-impact` / `graph-impact` in the same change. Write `source-manifest.json`, `code-summary.md`, `traceability.json`. Run `.\scripts\aidlc-agent-verify-sensor.ps1` with `-Files` for claimed paths; unit-scoped filters for the Red/Green loop (do not use a bare `dotnet test` of the whole project as this unit’s oracle).

## Out of this unit

- Tree hydrate (U1, already generated)
- Expire-on-process order (U3)
- NotificationOutcome webhook (U4)
- New HTTP routes, locks, sweeps
- `Journeys.UX`
