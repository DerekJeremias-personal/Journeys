# Code Quality Assessment

## Test coverage

**Directories:** `Journeys.Tests/` (RulesEngine/Outcomes, Services, Mcp, Workflow, CampaignAgent, Controllers, Utility, Stubs, Infra). UX Vitest is out of increment.

**Frameworks:** xUnit 2.9.3. **Coverage config:** coverlet.collector 6.0.3 present; no `coverlet.runsettings` / coverage floor file found.

### Intent-relevant tests present

| Test | What it locks | Gap vs closeout |
|---|---|---|
| `ExpirePointsOutcomeTests` | Lifecycle **balances** | No dest ExpirationDate / EarnDate+365 |
| `RuleServiceTests` | Root HistoricalRule TTL/decay | No child-node hydrate |
| `UserPointsTests` | Deposit/withdraw/expire service calls | Not ProcessEvent order |
| `PointAccountTypeValidatorTests` | Lifespan / `expiresTo` pairing | Sufficient for G3 validator |
| `RulesEngineMcpContractSummaryTests` | `MatrixVersion` `"2026-06-20"` | Must bump with matrix |

### Intent-relevant tests absent

`NotificationOutcomeTests`, `JourneyNodeCollectRulesTests`, child-node hydrate in `RuleServiceTests`, `LoyaltyAccountExpirationCascadeTests`, `EventServiceBringPointsCurrentTests`.

`Journeys.Tests` does not reference `Journeys.Notification` — fake `INotificationService` on state.

Adding `INotificationService` to the `RulesService` constructor will break long explicit constructors in `RuleServiceTests`. Prefer the same injection style as `loyaltyAccountService` already on state.

`TestDataFactory.GetSpendablePointAccount()` uses 30-day Spendable lifespan; the stated program example is 365. Lifecycle tests must set 365 or assert EarnDate + configured days.

## Linting and standards

- No `.editorconfig` observed at scan.
- C# follows `docs/platform/coding-standards.md` / nullable enable / `...Async` / constructor injection.
- UX TypeScript (out of increment).
- `RulesEngineState` unused usings (Amqp Framing, EF ValueGeneration.Internal) — cleanup only if the file is already opened for `NotificationService`.

## CI/CD

- No `.github/` workflows in this tree.
- Local / agent gate: `scripts/agent-verify.ps1`, `scripts/aidlc-agent-verify-sensor.ps1`, `scripts/docs-impact.ps1`, `scripts/graph-impact.ps1`.
- Before Construction stage complete: `.\scripts\aidlc-agent-verify-sensor.ps1` (`-RunTests` when tests change). Non-zero means halt.

## Documentation quality

Present and deep-read: ontology (`outcome`, `loyalty-account`, `journey`, `rule`, `event-model`), `process-event` use case, `path-map.yaml`, platform `architecture.md` / `runtime.md`, approved spec/plan.

**Drift (code canonical):**

| Doc | Drift |
|---|---|
| `outcome.md` | Matches unimplemented Notification — correct today; update when G1 ships |
| `process-event.md` | Already claims notifications fire — false vs code |
| `path-map.yaml` | `Journeys.Notification` `meaningOptional: true` — must become `false` |
| `loyalty-account.md` / `rule.md` | Do not yet describe earn-date cascade or tree hydrate as shipped |

Do not present roadmap follow-ons (hosted sweep, UX, email adapters) as current.

## Technical debt signals (preserve for construction)

Recorded once here; architecture cites this list.

1. `NotificationOutcome` / `WorkflowOutcome` / `RuleStateOutcome` TODO stubs. Only Notification is in increment.
2. `ThirdPartyOutcome` is not `OutcomeBase`.
3. `DisableDataLake` gates `INotificationService` registration.
4. `RulesEngineState` missing `NotificationService`; `NavigatePayload` does not copy it.
5. Award loop overwrites `IsAwarded` to true on non-null Award (architecture decision: honor flag).
6. `JourneyNode.FlattenToRulesOfType` skips `Rules` and does not flatten composite nav.
7. `HydrateState` root-RuleSet only (G4).
8. `SaveLedgerExpirations` UtcNow+100 on move (G3, human-gated).
9. Process-event skips bring-current (G2).
10. `PointBalanceProvider` loads points without resettle/expire.
11. `GetNotificationConfigAsync` ACTIVE-only; empty id throws with wrong `nameof`.
12. Factory `rest_api` only; unused `IRestApiAdapter`.
13. Commented MassTransit process-event publish — do not revive.
14. `LoyaltyAccountService` ~2.6k-line multi-responsibility type.
15. MCP / workflow pins on `MatrixVersion` `"2026-06-20"`.
16. Docs drift on process-event and Notification path-map.

## Risk surfaces (human-gated)

- Ledger / money-like outcomes: `SaveLedgerExpirations` date math only. No `PointLedgerTypeStrings` redesign.
- Tenant isolation: keep `TenantId` on every business operation.
- Auth0 `"hayward"`: do not edit.
- Do not log secrets, full event payloads, webhook auth headers, or raw audit JSON.

## Cross-reference

Component health ratings: [component-inventory.md](component-inventory.md). Business gaps: [business-overview.md](business-overview.md).
