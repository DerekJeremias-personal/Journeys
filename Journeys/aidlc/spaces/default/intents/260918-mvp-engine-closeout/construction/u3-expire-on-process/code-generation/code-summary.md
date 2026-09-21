# Code summary — `u3-expire-on-process`

## Files created or modified

- `Journeys.Tests/Services/ProcessCampaignsExpireOnProcessTests.cs` — seven Arrange–Act–Assert cases (lock then bring-current then rules with released `PointLedgers`; notification port still copied; bring-current throw skips rules; lock miss skips bring-current and keeps 500ms lease + retries; `ProcessEventInternalAsync` has no bring-current call; GET/reconcile + `ResettleAccountInternalAsync` unchanged; EventService has no dest clock).
- `Journeys.Core/Services/EventService.cs` — `ProcessCampaignsAsync` is `internal`. After a successful `TryLockAccount` and before `AttachNotificationPort` / `ProcessRulesAsync`, assign `loyaltyAccount.PointLedgers` from `_loyaltyAccountService.BringLoyaltyAccountPointsCurrentInternalAsync`. `AttachNotificationPort` stays immediately before `ProcessRulesAsync`. No bring-current in `ProcessEventInternalAsync` or `ResettleAccountInternalAsync`. GET/reconcile call sites and `SaveLedgerExpirations` untouched.
- Meaning docs: `docs/product/use-cases/process-event.md`, `docs/product/ontology/loyalty-account.md`, `campaign.md`, `event-model.md`; `docs/platform/architecture.md`; `docs/developer/testing.md`.

## Implementation decisions

- Only `ProcessCampaignsAsync` gained the new bring-current call (spec G2 / BR3.4–BR3.5: skip remains populate / `!bOk` / pre-save).
- Direct assign of `PointLedgers` (not `??=`) so this turn's rules see the released hold.
- Existing `catch (Exception)` rethrow fails the event if bring-current throws. Existing `TryLockAccount` lease (Guid key, 500ms) and three retries unchanged.
- No new lock, queue, hosted sweep, `/points/expire`, HTTP, MCP row, or `Journeys.Notification` claim.

## Tests

- New: `FullyQualifiedName~ProcessCampaignsExpireOnProcess` — **7/7 passed** after initializing `StubPointAccountTypeCache` and matching the `internal` `ProcessCampaignsAsync` definition (not the call inside `ProcessEventInternalAsync`).
- Historical `FullyQualifiedName~UserPointsTests` is not this unit’s oracle. Several cases still fail with missing account `"user1"` / adapter fixtures; this unit did not edit `LoyaltyAccountService` GET/reconcile expire. Do not use a bare `dotnet test` of `Journeys.Tests` (BatchJobAdapterIntegrationTests still needs a backend).

## Deviations

- Developer session could not run the runner (pre-tool hook). Conductor captured Red/Green: first run 5 failed (PAT cache + method-body match); Green after those two test-harness fixes is 7/7.
- Optional case 8 (ExpirePoints lease-preserve consumed, not reimplemented) was omitted; case 7 already asserts EventService has no second dest clock.
- Reconcile sites in the source-inspection test are `ReconcileLoyaltyAccountEventsByFileAsync` (~428) and `ReconcileLoyaltyAccountEventsAsync` (~589). `ReconcileLoyaltyAccountEventsByXidAsync` only delegates.
