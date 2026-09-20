# Entities — `u4-notification-webhook`

Closed outbound payload and existing tenant config. Confirmed Looks correct 2026-09-20. No new bus.

```yaml
entities:
  - name: NotificationOutcome
    description: Live RuleSet outcome that POSTs a tenant rest_api webhook on Award.
    attributes:
      - { name: kind, type: string, required: true, unique: false, allowed_values: [NotificationOutcome] }
      - { name: notificationConfigId, type: string, required: true, unique: false, constraints: "GUID string; no inline URL" }
    constraints:
      - Calculate returns null without NotificationConfigId
    relationships:
      - { to: NotificationConfig, cardinality: "N:1", direction: outcome-to-config }

  - name: NotificationConfig
    description: Existing tenant webhook config. AdapterType rest_api only this increment.
    attributes:
      - { name: id, type: string, required: true, unique: true }
      - { name: tenantId, type: string, required: true, unique: false }
      - { name: adapterType, type: string, required: true, unique: false, allowed_values: [rest_api] }
      - { name: eventModelId, type: string, required: false, unique: false }
      - { name: active, type: boolean, required: true, unique: false }
    constraints:
      - event tenant + that tenant's config only
    relationships: []

  - name: NotificationOutcomePayload
    description: Closed DTO posted to the tenant URL. No raw event JSON, ledger rows, or secrets.
    attributes:
      - { name: tenantId, type: string, required: true, unique: false }
      - { name: loyaltyAccountId, type: string, required: true, unique: false }
      - { name: extAccountId, type: string, required: false, unique: false }
      - { name: campaignId, type: string, required: true, unique: false }
      - { name: ruleSetId, type: string, required: true, unique: false }
      - { name: issuingOutcomeId, type: string, required: true, unique: false }
      - { name: issuingOutcomeKind, type: string, required: true, unique: false }
      - { name: eventId, type: string, required: true, unique: false }
      - { name: eventType, type: string, required: true, unique: false }
      - { name: eventModelId, type: string, required: false, unique: false }
      - { name: awardedAtUtc, type: datetime, required: true, unique: false }
    constraints:
      - additional properties ignored by consumers; do not log auth headers
    relationships: []

  - name: OutcomeResult
    description: Existing award result. Loop must honor IsAwarded false.
    attributes:
      - { name: isAwarded, type: boolean, required: true, unique: false }
    constraints: []
    relationships: []
```

## Summary

Config lives on the tenant. Payload is a closed field list. Award signature stays as today; the send port sits on engine state, not the rules-service constructor.
