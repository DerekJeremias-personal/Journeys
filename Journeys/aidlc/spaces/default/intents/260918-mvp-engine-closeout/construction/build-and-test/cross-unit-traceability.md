# Cross-unit traceability

**Verdict:** PASS for every three-segment AC (24/24 OK, target files exist). FR and NFR are covered through the story→unit map and those same targets. No AC is uncovered.

Sources: `inception/requirements-analysis/requirements.md`, `inception/user-stories/stories.md`, and each unit’s `construction/*/code-generation/traceability.json`.

## Acceptance criteria

| ID | Unit | Target | Status |
|----|------|--------|--------|
| AC1.1.1 | u1-tree-hydrate | `Journeys.Tests/Services/HydrateCollectTests.cs` | OK |
| AC1.1.2 | u1-tree-hydrate | `Journeys.Tests/Services/HydrateCollectTests.cs` | OK |
| AC1.1.3 | u1-tree-hydrate | `Journeys.Tests/Services/RuleServiceTests.cs` | OK |
| AC2.1.1 | u2-earn-date-cascade | `Journeys.Tests/Services/LoyaltyAccountExpirationCascadeTests.cs` | OK |
| AC2.1.2 | u2-earn-date-cascade | same | OK |
| AC2.1.3 | u2-earn-date-cascade | same | OK |
| AC2.1.4 | u2-earn-date-cascade | same | OK |
| AC2.1.5 | u2-earn-date-cascade | same | OK |
| AC2.1.6 | u2-earn-date-cascade | same | OK |
| AC3.1.1 | u3-expire-on-process | `Journeys.Tests/Services/ProcessCampaignsExpireOnProcessTests.cs` | OK |
| AC3.1.2 | u3-expire-on-process | same | OK |
| AC3.1.3 | u3-expire-on-process | same | OK |
| AC4.1.1 | u4-notification-webhook | `Journeys.Tests/RulesEngine/Outcomes/NotificationOutcomeTests.cs` | OK |
| AC4.1.2 | u4-notification-webhook | same | OK |
| AC4.1.3 | u4-notification-webhook | same | OK |
| AC4.1.4 | u4-notification-webhook | same | OK |
| AC4.1.5 | u4-notification-webhook | same | OK |
| AC4.1.6 | u4-notification-webhook | `Journeys.Tests/Mcp/RulesEngineMcpContractSummaryTests.cs` | OK |
| AC4.1.7 | u4-notification-webhook | `NotificationOutcomeTests.cs` | OK |
| AC4.1.8 | u4-notification-webhook | same | OK |
| AC4.1.9 | u4-notification-webhook | same | OK |
| AC5.1.1 | u4-notification-webhook | `docs/product/ontology/outcome.md` | OK |
| AC5.1.2 | u4-notification-webhook | `docs/product/graph/path-map.yaml` | OK |
| AC5.1.3 | u4-notification-webhook | `docs/product/use-cases/process-event.md` | OK |

## Functional requirements

Code-generation JSON lists AC/BR only. Coverage here is story ownership plus the AC target above.

| ID | Unit | Via | Status |
|----|------|-----|--------|
| FR1, FR1.1–FR1.4 | u1-tree-hydrate | AC1.1.1–AC1.1.3 | OK |
| FR2, FR2.1–FR2.6 | u2-earn-date-cascade | AC2.1.1–AC2.1.6 | OK |
| FR3, FR3.1–FR3.4, FR3.6 | u3-expire-on-process | AC3.1.1–AC3.1.3 | OK |
| FR3.5 | u2 + u3 | AC2.1.6, AC3.1.x | OK |
| FR4, FR4.1–FR4.10 | u4-notification-webhook | AC4.1.1–AC4.1.9 | OK |
| FR5 | u4-notification-webhook | AC4.1.6 | OK |
| FR6 | u4-notification-webhook | AC5.1.1–AC5.1.3 | OK |

## Non-functional requirements

| ID | Unit | Target / justification | Status |
|----|------|------------------------|--------|
| NFR1 Tenant isolation | u4 | `NotificationOutcomeTests.cs` + TenantId on process path | OK |
| NFR2 Secrets / payload hygiene | u4 | Closed `NotificationOutcomePayload`; no secret logs added | OK |
| NFR3 At-least-once | u4 | Reprocess may POST again; no second idempotency store | OK |
| NFR4 Ledger human gate | u2 | Human merge/review; `SaveLedgerExpirations` unchanged in shape | OK |
| NFR5 Concurrency | u3 | `ProcessCampaignsExpireOnProcessTests.cs` (existing lock) | OK |
| NFR6 Webhook-throw switch | u4 | `NotificationOutcomeTests.cs` | OK |
| NFR7 Test posture | all | This stage’s oracles; no coverage floor | OK |

## Uncovered elements

None for AC/FR/NFR. `UserPointsTests` historical failures are recorded in `test-results.md` and are not an uncovered AC (AC3.1.x is `ProcessCampaignsExpireOnProcessTests.cs`).
