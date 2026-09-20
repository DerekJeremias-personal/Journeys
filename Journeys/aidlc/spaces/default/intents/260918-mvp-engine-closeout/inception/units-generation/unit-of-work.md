# Units of work — MVP engine closeout

Four `library` units inside the existing `Journeys.API` modular monolith. Docs/graph (US5.1) fold into U4. No new host, capability id, or UI.

## Unit index

| Unit ID | Directory | Kind | Complexity | Deploy |
|---------|-----------|------|------------|--------|
| U1 | `u1-tree-hydrate` | library | M | embedded |
| U2 | `u2-earn-date-cascade` | library | M | embedded (ledger human-gated) |
| U3 | `u3-expire-on-process` | library | M | embedded |
| U4 | `u4-notification-webhook` | library | L | embedded |

## U1 — `u1-tree-hydrate`

**Description:** Tree-wide hydrate so child-node and nav historical/taxonomic rules decay like root RuleSets.

**Boundaries:** `RulesService.HydrateState` and `JourneyNode` collect helpers. Does not change ledgers or NotificationOutcome.

**Responsibilities:** Collect earn + nav rules recursively; keep existing TTL/decay/RuleState/upsert; TDD on the collect seam.

**Constraints:** Do not lock root-only `RuleServiceTests` as the new oracle. No `EventService` change.

## U2 — `u2-earn-date-cascade`

**Description:** PAT cascade keeps `EarnDate` and sets dest expiration from dest end-date, else earn + days, else unset.

**Boundaries:** `LoyaltyAccountService.SaveLedgerExpirations` only. Deposit-time EarnDate math stays. No `PointLedgerTypeStrings` redesign.

**Responsibilities:** Earn-date dest clock; missing dest PAT stays in source; dest-PAT infra throw fails ProcessEvent; already-cascaded UtcNow rows stay.

**Constraints:** Human-gated ledger/money-like. Arrange dest PAT with 365 days — not the 30-day Spendable factory.

## U3 — `u3-expire-on-process`

**Description:** On ProcessEvent, bring points current after `TryLockAccount` and before navigation/rules so this event can spend a released hold.

**Boundaries:** `EventService.ProcessCampaignsAsync` order. Calls existing `BringLoyaltyAccountPointsCurrentInternalAsync`. No new lock, sweep, or `/points/expire`.

**Constraints:** Depends on U2 for hop math inside bring-current. Do not implement unlocked pre-`ProcessCampaignsAsync` placement (spec G2). GET/reconcile callers unchanged. Invalid/missing accounts skip bring-current.

## U4 — `u4-notification-webhook`

**Description:** Live RuleSet `NotificationOutcome` POSTs the tenant `rest_api` webhook. MCP `NotificationConfigId` row. Meaning docs/path-map in the same change (US5.1).

**Boundaries:** `NotificationOutcome` Calculate/Award; closed `NotificationOutcomePayload` in `Journeys.DTO`; `RulesEngineState.NotificationService`; award loop honors `IsAwarded`; always-register `INotificationService`. Docs listed in US5.1.

**Constraints:** Do not change `AwardOutcomeAsync` signature. Do not add `INotificationService` to the `RulesService` constructor (tests or production). Production sets the port on `RulesEngineState` / `NavigatePayload` from host/`EventService` the same copy style as `LoyaltyAccountService` on state — not a fifth unit. Fake the port in `Journeys.Tests`. Throw fails ProcessEvent unless the host switch is on. No email/Twilio adapters.

## Shared constraints

- Existing capability ids only. `Journeys.UX` out.
- Construction verify: `scripts/aidlc-agent-verify-sensor.ps1` (`-RunTests` when tests change).
- One Linear issue per unit after this stage’s human approve; no `Journeys.*` product code until `-ApproveCreate`.
