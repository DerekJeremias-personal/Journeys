# Functional Design Questions — `u4-notification-webhook`

Ingested from approved spec, US4.1 / AC4.1.1–AC4.1.9, US5.1, C3–C4. No frontend. Execute-from-spec (2026-09-19).

## Q1. Payload and port

A. Closed `NotificationOutcomePayload` in `Journeys.DTO`; `INotificationService` on `RulesEngineState` (host/`EventService` copy); Award signature unchanged (recommended)
B. Put `INotificationService` on the `RulesService` constructor
X. Other (please specify)

[Answer]: A. Closed `NotificationOutcomePayload` in `Journeys.DTO`; `INotificationService` on `RulesEngineState` (host/`EventService` copy); Award signature unchanged (2026-09-19, **Mode:** Chat)

## Q2. Fail / skip

A. Calculate null on missing config / model mismatch; send false → `IsAwarded` false, siblings kept; throw fails ProcessEvent unless host switch; existing `RestApiConfig.Retry` only (recommended)
B. Throw is always non-fatal
X. Other (please specify)

[Answer]: A. Calculate null on missing config / model mismatch; send false → `IsAwarded` false, siblings kept; throw fails ProcessEvent unless host switch; existing `RestApiConfig.Retry` only (2026-09-19, **Mode:** Chat)

## Q3. MCP and docs

A. `MatrixVersion` `2026-09-18` + `NotificationConfigId` critical row; do not retarget `2026-06-20` pin fixtures; US5.1 docs/graph in the same change (recommended)
B. Skip MCP / docs this unit
X. Other (please specify)

[Answer]: A. `MatrixVersion` `2026-09-18` + `NotificationConfigId` critical row; do not retarget `2026-06-20` pin fixtures; US5.1 docs/graph in the same change (2026-09-19, **Mode:** Chat)

## Consolidated Summary Confirmation

Does this all look correct before I generate the artifact?

- Looks correct
- Request changes

[Answer]: Looks correct
