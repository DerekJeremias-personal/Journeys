**Collaborator:** aidlc-developer-agent

## Contribution

Developer spoke, User Stories, intent `mvp-engine-closeout`. Scope is implementability and story sizing against the existing onion seams (`HydrateState`, `SaveLedgerExpirations`, `ProcessCampaignsAsync` / `TryLockAccount`, `NotificationOutcome`). Do not change `AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)`. Sources: `personas.md`, `stories.md`, `user-stories-questions.md`, `requirements.md`, CodeKB `architecture.md` / `code-structure.md`, and the live Core methods cited below. `Journeys.UX` stays out of increment. No new capability ids. Tests stay in `Journeys.Tests` with TDD on these seams.

The Q1 seam split (one Must story per engine gap, errors and MCP as AC) maps 1:1 to the approved plan units and to independently testable Core methods. None of the Must stories are too large for that mapping. None prescribe a controller, MCP handler, or Notification adapter as the business owner. None change the Award signature in INVEST or AC text.

### US1.1 — HydrateState (implementable, right size)

`RulesService.HydrateState` still collects historical and taxonomy rules from root `campaign.Journey.Rules` only (lines 660–672). `JourneyNode.FlattenToRulesOfType<T>` walks NavConstraint via `OfType<T>()` (no composite flatten) plus `Children`, and does **not** walk earn `Rules`. AC1.1.2’s nested/composite nav case is the real collect bug, not a second story. Keep TTL fetch, decay, `LoyaltyAccount.RuleState` load, and finally upsert inside `HydrateState`; add `CollectEarnAndNavRulesOfType<T>` (or equivalent) on `JourneyNode` and point the two scans at it. Do not extend `EventService`. Child-node second-event AC is a `RuleServiceTests` case, not a new host. Independently testable of ledgers. Sized correctly.

### US2.1 — SaveLedgerExpirations (implementable, right size, human-gated)

The move clock is `SaveLedgerExpirations` overwriting dest `ExpirationDate` with `UtcNow + (PointsLifespanDays ?? 100)` (lines 2484–2486). `EarnDate` is already kept on the same `LedgerEntry`. AC2.1.1–AC2.1.3 fit one method: dest end-date wins, else `EarnDate + days`, else unset; missing dest PAT stays on the source row and logs; do not fail ProcessEvent for that miss. Do **not** retouch the deposit-time `EarnDate + days` path. Do not redesign `PointLedgerTypeStrings`. Do not put date math on `EventService` or `ExpirePointsOutcome`. Ledger / money-like remains human-gated. AC2.1.2 (leave already-moved UtcNow rows) is a non-migration constraint, not extra scope. Sized correctly.

### US3.1 — ProcessCampaignsAsync after TryLockAccount (implementable, right layer)

Today `ProcessEventInternalAsync` populates the account and calls `ProcessCampaignsAsync` with no bring-current (EventService ~867–929). The lock is already here:

```
TryLockAccount(...)
ProcessRulesAsync(...)   // hydrate → navigate → RuleSet → PointBalanceProvider
```

(`EventService.ProcessCampaignsAsync` 1248–1254; lease + 3 retries; `ResettleAccountInternalAsync` has the same lock and is out of increment.)

AC3.1.1–AC3.1.3 correctly place `BringLoyaltyAccountPointsCurrentInternalAsync` **after** that `TryLockAccount` and **before** `ProcessRulesAsync` / navigation. That is the FR3 / NFR5 seam. It is **not** the older spec G2 / plan Task 3 placement (EventService after populate, before `ProcessCampaignsAsync`, unlocked). Construction must not implement the unlocked call site: two ProcessEvents could both expire then contend. No new lock, queue, hosted sweep, or `/points/expire`. GET/reconcile callers stay as they are. Invalid / missing accounts skip bring-current. Throw after the lock fails the event (existing `catch (Exception)` rethrows). Sized correctly; do not merge into US2.1. Cascade hops that run inside bring-current still use US2.1 math — land US2.1 with or before this story in the same increment, not as a later clock rewrite.

### US4.1 — NotificationOutcome (right seam, Award signature must stay)

`NotificationOutcome.CalculateOutcomeAsync` / `AwardOutcomeAsync` still return null. `RulesEngineState` has `LoyaltyAccountService` and no `NotificationService`. `INotificationService.SendNotificationAsync(tenantId, configId, payload)` and `RestApiAdapter` already exist. INVEST is correct: put the service on state the same way `LoyaltyAccountService` is set; copy it on `NavigatePayload`; do **not** add a parameter to `AwardOutcomeAsync`; do not put `HttpClient` on the outcome; payload is a closed `Journeys.DTO` type; no new Kind. Calculate nulls (missing id / inactive / EventModelId mismatch) stay in Calculate. CalculateOnly already returns before the award loop (`ProcessJourneyAsync` 970–971). Fake `INotificationService` on state in `Journeys.Tests`; do not reference `Journeys.Notification`; do not extend the `RulesService` constructor for the fake. Always-register DI and MCP `MatrixVersion` 2026-09-18 + `NotificationConfigId` critical row are AC on this seam (Q3), not a fifth operator story and not a signature change. Host throw-switch is appsettings, not a capability id (AC4.1.4 / NFR6). Sized as one Must story / one plan unit if the award-loop honor below is named.

### US5.1 — docs/graph (not an engine seam)

Should Have, same change set as the Must seams. Ontology / process-event / path-map only. `scripts/docs-impact.ps1`, `scripts/graph-impact.ps1`, and `scripts/aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed). No Core API change. Not too large.

## Positions

- AGREE: One Must story per existing seam (US1.1 `HydrateState` / `JourneyNode` collect, US2.1 `SaveLedgerExpirations`, US3.1 `ProcessCampaignsAsync` after `TryLockAccount`, US4.1 `NotificationOutcome`) matches Q1 and the four plan units; none is too large for that mapping; error edges as AC is the right size.
- AGREE: US3.1 names the live lock (`TryLockAccount` in `ProcessCampaignsAsync` 1248–1254), not an EventService-before-lock helper and not a new mutex. Construction must not follow spec G2 / plan Task 3’s unlocked placement.
- AGREE: US1.1 and US2.1 stay on Core (`HydrateState` + collect helper; `SaveLedgerExpirations` only). Deposit-time EarnDate math and GET/reconcile outer lock stay untouched. Human-gated ledger on US2.1.
- AGREE: US4.1 INVEST keeps `AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)` unchanged and puts `INotificationService` on `RulesEngineState`. MCP and the throw-switch are AC, not a new Kind or a second Award parameter.
- OBJECT: US4.1 AC4.1.3 / AC4.1.4 are not implementable on the current award loop without naming FR4.10. `ProcessJourneyAsync` (RulesService 986–992) sets `IsAwarded = true` on every non-null Award return. If the story stays silent, Construction will “fix” webhook `false` by changing `AwardOutcomeAsync`’s signature or by returning null from Award. Require a Given/When/Then that the loop honors the returned `IsAwarded` (do not force true) and still does not change the Award signature.
