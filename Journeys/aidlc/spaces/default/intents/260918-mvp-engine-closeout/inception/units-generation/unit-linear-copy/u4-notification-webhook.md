# U4 — `u4-notification-webhook`

## U4 — `u4-notification-webhook`

**Description:** Live RuleSet `NotificationOutcome` POSTs the tenant `rest_api` webhook. MCP `NotificationConfigId` row. Meaning docs/path-map in the same change (US5.1).

**Boundaries:** `NotificationOutcome` Calculate/Award; closed `NotificationOutcomePayload` in `Journeys.DTO`; `RulesEngineState.NotificationService`; award loop honors `IsAwarded`; always-register `INotificationService`. Docs listed in US5.1.

**Constraints:** Do not change `AwardOutcomeAsync` signature. Do not add `INotificationService` to the `RulesService` constructor (tests or production). Production sets the port on `RulesEngineState` / `NavigatePayload` from host/`EventService` the same copy style as `LoyaltyAccountService` on state — not a fifth unit. Fake the port in `Journeys.Tests`. Throw fails ProcessEvent unless the host switch is on. No email/Twilio adapters.
