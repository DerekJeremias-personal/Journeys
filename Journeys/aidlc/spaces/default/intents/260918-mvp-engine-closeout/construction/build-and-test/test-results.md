# Test results

Recorded 2026-09-21T18:50:00Z. Working directory `C:\Dev\Journeys\Journeys`.

## Build status

**Success.** `dotnet restore Journeys.sln` then `dotnet build Journeys.sln -c Debug --no-restore` exit 0 (0 errors; existing warning noise).

## Test results

| Command | Passed | Failed | Skipped | Total | Exit |
|---------|--------|--------|---------|-------|------|
| `FullyQualifiedName~HydrateCollect` | 8 | 0 | 0 | 8 | 0 |
| `FullyQualifiedName~LoyaltyAccountExpirationCascade` | 8 | 0 | 0 | 8 | 0 |
| `FullyQualifiedName~ProcessCampaignsExpireOnProcess` | 7 | 0 | 0 | 7 | 0 |
| `FullyQualifiedName~NotificationOutcomeTests` | 9 | 0 | 0 | 9 | 0 |
| `FullyQualifiedName~RulesEngineMcpContractSummaryTests` | 16 | 0 | 0 | 16 | 0 |
| `FullyQualifiedName~RuleServiceTests` | 14 | 0 | 0 | 14 | 0 |
| `FullyQualifiedName~ExpirePointsOutcomeTests` | 8 | 0 | 0 | 8 | 0 |
| `FullyQualifiedName~UserPointsTests` | 22 | 17 | 0 | 39 | 1 |

**Totals (deduped commands above):** passed 70, failed 17, skipped 0. All 17 failures are `UserPointsTests` historical GET/reconcile fixtures — not the expire-on-process oracle.

## Failure details

Shared pattern: `No loyaltyaccount found for the provided accountid: user1` from `LoyaltyAccountService.DepositPointsAsync` / `ExpireLoyaltyAccountPointsByEarnDate`. Also:

- `BringPointsCurrent_WithInvalidParameters_ThrowsArgumentNullException` — expected `ArgumentNullException` for null/empty account id; none thrown.
- `BringPointsCurrent_ExpiresOldPoints` — `NullReferenceException` in the test at line 299.

These fixtures predate this increment. `docs/developer/testing.md` already says `UserPointsTests` is not the ProcessEvent expire oracle.

## Coverage

No in-repo coverage floor. Coverlet was not used as a gate.

## Target Verification Matrix

| Target ID | Source | Expected | Actual | Evidence | Owning Stage | Verdict |
|-----------|--------|----------|--------|----------|--------------|---------|
| TC-BUILD | build-instructions.md | sln build exit 0 | 0 errors | `dotnet build Journeys.sln -c Debug` | build-and-test | Met |
| TC-U1-ORACLE | u1 unit-test-instructions | HydrateCollect 5–8 facts all pass | 8/8 | `dotnet test --filter FullyQualifiedName~HydrateCollect` | build-and-test | Met |
| TC-U2-ORACLE | u2 unit-test-instructions | Cascade 5–8 facts all pass | 8/8 | `dotnet test --filter FullyQualifiedName~LoyaltyAccountExpirationCascade` | build-and-test | Met |
| TC-U3-ORACLE | u3 unit-test-instructions | Expire-on-process 5–8 facts all pass | 7/7 | `dotnet test --filter FullyQualifiedName~ProcessCampaignsExpireOnProcess` | build-and-test | Met |
| TC-U4-ORACLE | u4 unit-test-instructions | NotificationOutcome 8 facts | 9/9 | `dotnet test --filter FullyQualifiedName~NotificationOutcomeTests` | build-and-test | Met |
| TC-U4-MCP | u4 / AC4.1.6 | MCP summary all pass | 16/16 | `dotnet test --filter FullyQualifiedName~RulesEngineMcpContractSummaryTests` | build-and-test | Met |
| TC-INT-BOUNDARY | integration-test-instructions.md | U3 + U4 oracles pass | both exit 0 | same two filters | build-and-test | Met |
| TC-VERIFY | Testing Contract | `aidlc-agent-verify-sensor.ps1` exit 0 | OK (docs-impact, graph-impact, build) | `-Files` closeout product+docs (no `Journeys.Tests` paths) | build-and-test | Met |
| TC-NFR7 | NFR7 / Testing Contract | Custom TDD oracles; no coverage floor; no Notification project ref in tests | oracles green; tests fake `INotificationService` | this file + `Journeys.Tests` project refs | build-and-test | Met |
| TC-HIST-U1 | u1 unit-test-instructions | RuleServiceTests green | 14/14 | filter `RuleServiceTests` | build-and-test | Met |
| TC-HIST-U2 | u2 unit-test-instructions | ExpirePointsOutcomeTests green | 8/8 | filter `ExpirePointsOutcomeTests` | build-and-test | Met |
| TC-HIST-U3 | u3 unit-test-instructions | UserPointsTests historical green | 22 passed, 17 failed (`user1` missing; null-arg asserts) | filter `UserPointsTests` | build-and-test | Not Met |
| TC-HIST-U4 | u4 unit-test-instructions | MCP historical green | 16/16 | same as TC-U4-MCP | build-and-test | Met |
| TC-PERF | performance-test-instructions.md | No performance NFR | no load command | performance-test-instructions.md | build-and-test | N/A |
| TC-SEC-SCAN | security-test-instructions.md | No new scanners | none invented | security-test-instructions.md | build-and-test | N/A |

## Loop-Back Log

None. Halt-and-ask presented before any loop-back entry (autonomy unset; historical fixture debt is not a closeout-unit code defect).
