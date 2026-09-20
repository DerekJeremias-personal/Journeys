# Rules — `u4-notification-webhook`

Confirmed Looks correct 2026-09-20.

```yaml
rules:
  - id: BR4.1
    statement: NotificationConfigId is required for Calculate to return a result.
    category: validation
    applies_to: NotificationOutcome
    trigger: Calculate
    logic: IF NotificationConfigId is missing THEN Calculate returns null and the event is not failed for that reason.
    violation: Award POSTs without a config.
    source: FR4.1, AC4.1.2

  - id: BR4.2
    statement: Missing, inactive, or EventModelId-mismatched config yields null Calculate.
    category: validation
    applies_to: NotificationConfig
    trigger: Calculate
    logic: IF config is missing or inactive OR config.EventModelId is set and not equal to state EventModelId THEN return null.
    violation: POST to the wrong tenant model or a dead config.
    source: FR4.2, FR4.3, AC4.1.2

  - id: BR4.3
    statement: Award POSTs only the closed payload for this event's tenant and that tenant's config.
    category: policy
    applies_to: NotificationOutcomePayload
    trigger: Award when Calculate returned a result and CalculateOnly is false
    logic: IF send runs THEN the body is the closed field list; no event body, ledger rows, or secrets; tenant isolation holds.
    violation: Leak or cross-tenant POST.
    source: FR4.4, NFR1, NFR2, AC4.1.1

  - id: BR4.4
    statement: Send false leaves IsAwarded false and keeps sibling awards.
    category: policy
    applies_to: OutcomeResult
    trigger: Award after adapter returns false
    logic: IF send returns false THEN IsAwarded is false; sibling point or tag awards are not rolled back; reprocess may POST again.
    violation: Forced true or rolled-back siblings.
    source: FR4.5, AC4.1.3, AC4.1.9

  - id: BR4.5
    statement: Send throw fails ProcessEvent unless the host switch is on.
    category: policy
    applies_to: ProcessEvent
    trigger: Award send throws
    logic: IF send throws AND the default fail-closed setting is on THEN fail the event. IF the host switch is on THEN treat as send false (BR4.4).
    violation: Silent drop or always-fatal regardless of switch.
    source: FR4.6, FR4.10, AC4.1.4, AC4.1.7

  - id: BR4.6
    statement: CalculateOnly never Awards or POSTs.
    category: constraint
    applies_to: Award
    trigger: CalculateOnly true
    logic: IF CalculateOnly is true THEN Calculate may resolve; Award and HTTP send do not run.
    violation: Preview sends a webhook.
    source: FR4.7, AC4.1.5

  - id: BR4.7
    statement: MCP matrix lists NotificationConfigId as required on NotificationOutcome.
    category: constraint
    applies_to: MCP contract summary
    trigger: Increment ships
    logic: IF this unit ships THEN MatrixVersion is 2026-09-18 and the NotificationConfigId critical row exists; deposit and spend rows stay; do not retarget 2026-06-20 pin fixtures.
    violation: Authoring tools omit the required id.
    source: FR5, AC4.1.6

  - id: BR4.8
    statement: The notification port always resolves in host composition, including when DataLake is disabled.
    category: constraint
    applies_to: host registration
    trigger: Application start
    logic: IF notification services are added THEN INotificationService resolves even when DisableDataLake is true.
    violation: Award cannot send in that host mode.
    source: FR4.8, AC4.1.8

  - id: BR4.9
    statement: The award loop honors returned IsAwarded false and does not change the Award signature.
    category: constraint
    applies_to: ProcessJourney award loop
    trigger: Award returns a non-null result
    logic: IF Award returns IsAwarded false THEN the loop records false. Award still takes engine state, loyalty-account port, and cancellation only.
    violation: Forced true or a fifth constructor argument on RulesService.
    source: FR4.9, AC4.1.9

  - id: BR5.1
    statement: Meaning docs and path-map update in the same change as the webhook seam.
    category: policy
    applies_to: docs/product and path-map
    trigger: Increment offered for merge
    logic: IF FR1–FR4 have landed THEN ontology, process-event use case, and path-map describe the shipped seams; Notification prefix is not meaning-optional; verify scripts pass.
    violation: Code ships with stale meaning.
    source: FR6, AC5.1.1, AC5.1.2, AC5.1.3
```

## Summary

| ID | Category | Trigger |
|----|----------|---------|
| BR4.1 | validation | Missing config id |
| BR4.2 | validation | Inactive / model mismatch |
| BR4.3 | policy | Closed tenant-scoped payload |
| BR4.4 | policy | Send false |
| BR4.5 | policy | Send throw |
| BR4.6 | constraint | CalculateOnly |
| BR4.7 | constraint | MCP row |
| BR4.8 | constraint | Always register port |
| BR4.9 | constraint | Honor IsAwarded |
| BR5.1 | policy | Docs increment-close |
