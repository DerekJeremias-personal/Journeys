# Build and Test summary

## Overall status

Prerequisites: .NET 8 SDK, restored `Journeys.sln`, no live Azure required for closeout oracles.

Build status: Success (`dotnet build Journeys.sln` exit 0)  
Test status: Closeout oracles green; historical `UserPointsTests` 17 failed (`user1` fixtures)  
Construction verify: Success (`aidlc-agent-verify-sensor.ps1 -Files` closeout product+docs)

## Test type inventory

| Type | Generated | Why |
|------|-----------|-----|
| Unit (per-unit oracles) | Yes — commands in each `unit-test-instructions.md` | Custom TDD on four closeout seams |
| Integration (Standard) | `integration-test-instructions.md` | Key ProcessEvent / MCP boundaries |
| Performance | `performance-test-instructions.md` | No performance NFR; no executable load command |
| Security | `security-test-instructions.md` | NFR1/2/5/6 via existing oracles; no new scanners |

## Coverage expectations per unit

| Unit | New facts | Oracle filter |
|------|-----------|---------------|
| u1-tree-hydrate | 5–8 | `HydrateCollect` |
| u2-earn-date-cascade | 5–8 | `LoyaltyAccountExpirationCascade` |
| u3-expire-on-process | 5–8 | `ProcessCampaignsExpireOnProcess` |
| u4-notification-webhook | 8 (optional 9th DI) | `NotificationOutcomeTests` |

No in-repo coverage floor. Coverlet is a collector, not a gate.

## Target Verification Matrix

| Target ID | Source | Expected | Actual | Evidence | Owning Stage | Verdict |
|-----------|--------|----------|--------|----------|--------------|---------|
| TC-BUILD | build-instructions.md | `dotnet build Journeys.sln` exit 0 | 0 errors | `dotnet build` | build-and-test | Met |
| TC-U1-ORACLE | u1 unit-test-instructions / Testing Contract volume | `HydrateCollect` all pass, 5–8 facts | 8/8 | filter `HydrateCollect` | build-and-test | Met |
| TC-U2-ORACLE | u2 unit-test-instructions | `LoyaltyAccountExpirationCascade` all pass, 5–8 facts | 8/8 | filter `LoyaltyAccountExpirationCascade` | build-and-test | Met |
| TC-U3-ORACLE | u3 unit-test-instructions | `ProcessCampaignsExpireOnProcess` all pass, 5–8 facts | 7/7 | filter `ProcessCampaignsExpireOnProcess` | build-and-test | Met |
| TC-U4-ORACLE | u4 unit-test-instructions | `NotificationOutcomeTests` all pass, 8 facts | 9/9 | filter `NotificationOutcomeTests` | build-and-test | Met |
| TC-U4-MCP | u4 unit-test-instructions / AC4.1.6 | `RulesEngineMcpContractSummaryTests` all pass | 16/16 | filter `RulesEngineMcpContractSummaryTests` | build-and-test | Met |
| TC-INT-BOUNDARY | integration-test-instructions.md | U3 + U4 oracles both pass (lock → bring-current → notify port → rules) | both exit 0 | same two filters | build-and-test | Met |
| TC-VERIFY | Testing Contract `notes.team.construction_verify` | `.\scripts\aidlc-agent-verify-sensor.ps1` exit 0 | OK | `-Files` closeout product+docs | build-and-test | Met |
| TC-NFR7 | requirements.md NFR7 / Testing Contract | Custom TDD oracles + no coverage floor + no `Journeys.Notification` test ref | met | oracles + project refs | build-and-test | Met |
| TC-HIST-U1 | u1 unit-test-instructions | `RuleServiceTests` all pass | 14/14 | filter `RuleServiceTests` | build-and-test | Met |
| TC-HIST-U2 | u2 unit-test-instructions | `ExpirePointsOutcomeTests` all pass | 8/8 | filter `ExpirePointsOutcomeTests` | build-and-test | Met |
| TC-HIST-U3 | u3 unit-test-instructions | `UserPointsTests` historical GET/reconcile stay green | 22 passed, 17 failed | filter `UserPointsTests` | build-and-test | Not Met |
| TC-HIST-U4 | u4 unit-test-instructions | Same as TC-U4-MCP | 16/16 | filter `RulesEngineMcpContractSummaryTests` | build-and-test | Met |
| TC-PERF | performance-test-instructions.md | No performance NFR; no load command | N/A — no measurable performance target in inventory | performance-test-instructions.md | build-and-test | N/A |
| TC-SEC-SCAN | security-test-instructions.md / team affirmation | No new in-tree SAST/DAST this increment | N/A — scanners forbidden this increment | security-test-instructions.md | build-and-test | N/A |

`N/A` is used only for TC-PERF and TC-SEC-SCAN, which the inventory found had no applicable executable target.

## Readiness

- Build-ready: Yes
- Test-ready: Closeout oracles yes; historical `UserPointsTests` no
- Deployment-ready: No — humans merge and release; this tree has no CD.

## Known limitations

- Full `Journeys.Tests` without a filter is not this increment’s oracle (`BatchJobAdapterIntegrationTests` and similar backend fixtures).
- Code Generation advisory findings (U4 award-row uniqueness, production send-throw, U2 lease-preserve, U1 Exit/Transition collect) remain accepted risk from the prior gate; they are not weakened here.
- Domain-design drift warning is advisory; this stage does not redo Inception.
