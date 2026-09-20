# Contract summary — MVP engine closeout

**Conversation language:** English  
**Intent:** `mvp-engine-closeout`  
**Owners from Contract Design Q3.** Four formal specs (Q1). Formats from Q2. Versioning from Q4. Failure from Q5. No new host, queue, AWS, or `Journeys.UX`.

## Sources

| Artifact | Path |
|----------|------|
| `unit-of-work.md` | `aidlc/spaces/default/intents/260918-mvp-engine-closeout/inception/units-generation/unit-of-work.md` |
| `unit-of-work-dependency.md` | `aidlc/spaces/default/intents/260918-mvp-engine-closeout/inception/units-generation/unit-of-work-dependency.md` |
| `components.md` | `aidlc/spaces/default/intents/260918-mvp-engine-closeout/inception/domain-design/components.md` |
| `requirements.md` | `aidlc/spaces/default/intents/260918-mvp-engine-closeout/inception/requirements-analysis/requirements.md` |
| Q&A | `contract-design-questions.md` |

## Contracts

| # | Provider Unit | Consumer | Mechanism | Owner |
|---|---------------|----------|-----------|-------|
| C1 | `u3-expire-on-process` | External: ProcessEvent HTTP client (API / MCP caller) | Existing sync REST — `POST /api/Events/{tenantId}/{modelName}/process` (no new routes) | U3 |
| C2 | `u2-earn-date-cascade` | `u3-expire-on-process` (bring-current hops) | In-process shared schema | U2 |
| C3 | `u4-notification-webhook` | External: tenant `rest_api` webhook | Sync HTTPS POST of closed `NotificationOutcomePayload` | U4 |
| C4 | `u4-notification-webhook` | External: MCP / campaign-authoring tools | Published MCP matrix row | U4 |

Related in-process (not a fifth table row; same ProcessEvent turn): hydrate collect is a shared-schema owned by `u1-tree-hydrate` (see C1 appendix). `RulesEngineState.NotificationService` copy from host/`EventService` is U4’s host-side duty, not a new HTTP contract.

No EventBridge, SQS, or new AWS. Tenant webhook uses the existing `Journeys.Notification` `rest_api` adapter.

## C1 ProcessEvent HTTP

Existing `EventsController` route. This increment does **not** add `/points/expire`, `/v2`, or a new process verb. U3 changes only the **order** after the existing lock: `TryLockAccount` → `BringLoyaltyAccountPointsCurrentInternalAsync` → navigate / rules.

```yaml
openapi: 3.1.0
info:
  title: Journeys ProcessEvent (existing; no new routes this increment)
  version: existing
paths:
  /api/Events/{tenantId}/{modelName}/process:
    post:
      operationId: processEvent
      parameters:
        - name: tenantId
          in: path
          required: true
          schema: { type: string }
        - name: modelName
          in: path
          required: true
          schema: { type: string }
        - name: campaignId
          in: query
          required: false
          schema: { type: string, nullable: true }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              additionalProperties: true
              description: Tenant event-model payload (existing JsonElement). Not redesigned here.
      responses:
        "200":
          description: ProcessEvent completed. Body is existing EventPayloadResponseDto (camelCase JSON).
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/EventPayloadResponse"
        "400":
          description: APIErrorsException (validation) or other existing failure. After the lock, a throw (bring-current, dest-PAT infra, webhook throw unless host switch) fails this event.
        "403":
          description: draftTestingNotPermitted (existing).
components:
  schemas:
    EventPayloadResponse:
      type: object
      properties:
        tenantId: { type: string }
        processedEventModelId: { type: string }
        eventNaturalKey: { type: string }
        loyaltyAccountId: { type: string }
        event: { type: object, additionalProperties: true }
        timeOfOccurrence: { type: string, format: date-time }
        lastProcessed: { type: string, format: date-time }
        appliedCampaigns: { type: array, items: { type: string } }
        appliedRuleSetIds: { type: array, items: { type: string } }
        outcomeStates: { type: array, items: { type: object, additionalProperties: true } }
        errors: { type: object, additionalProperties: { type: string } }
      additionalProperties: true
```

**In-process order (U3 owns):** after a valid account is populated, acquire today’s `TryLockAccount`, then bring-current (C2 hops), then `RulesService` (C1 appendix hydrate + evaluate + C3 Award). Invalid / missing / pre-save wrapper accounts skip bring-current. GET / reconcile callers unchanged.

### C1 appendix — hydrate collect (owner `u1-tree-hydrate`)

```yaml
shared-schema:
  name: hydrate-collect
  owner: u1-tree-hydrate
  consumer: RulesService.HydrateState on the existing ProcessEvent path
  collect:
    - every JourneyNode earn RuleSets (Rules)
    - every NavConstraint tree (including nested composites)
    - recursively through Children
  keep:
    - existing TTL fetch, decay, LoyaltyAccount.RuleState load, finally upsert
  must-not:
    - read only root campaign.Journey.Rules
    - throw on empty Rules
    - change EventService or ledgers
```

## C2 Earn-date hop

U3’s bring-current may move due `LedgerEntry` rows via existing `ExpiresToPointAccountTypeId`. U2 owns the dest clock. Human-gated ledger math.

```yaml
shared-schema:
  name: earn-date-cascade-hop
  owner: u2-earn-date-cascade
  consumer: u3-expire-on-process via BringLoyaltyAccountPointsCurrentInternalAsync
  implementation: LoyaltyAccountService.SaveLedgerExpirations
  input:
    LedgerEntry:
      id: string
      earnDate: datetime | null
      expirationDate: datetime | null
      pointAccountTypeId: string
    destPointAccountType:
      id: string
      pointsLifespanDays: integer | null
      pointsLifespanEndDate: datetime | null
      expiresToPointAccountTypeId: string | null
  destExpiration:
    - if dest.pointsLifespanEndDate set: that instant
    - else if dest.pointsLifespanDays set: earnDate + days
    - else if earnDate missing: existing expirationDate last resort; never UtcNow as lifespan base
    - else: dest expiration unset
  keep:
    - LedgerEntry.earnDate unchanged on the hop
  missingDestPat:
    - dest PAT id missing or not loadable for the tenant: leave row in source PAT; log; do not fail the event
  destPatInfraThrow:
    - unexpected persist/load throw fails ProcessEvent (C1 400 / fail-the-event)
  alreadyCascadedUtcNowRows:
    - stay as written until they hop again
  must-not:
    - UtcNow + (days ?? 100) on the move
    - PointLedgerTypeStrings redesign
    - 30-day Spendable factory as the dest-days arrange (use dest PAT with 365 days in tests)
```

## C3 Tenant webhook

Live RuleSet `NotificationOutcome` POSTs the tenant’s existing `NotificationConfig` (`AdapterType = rest_api`). Factory unchanged. No email / Twilio. Award signature unchanged. `INotificationService` on `RulesEngineState`, not on the `RulesService` constructor.

```yaml
openapi: 3.1.0
info:
  title: Tenant NotificationOutcome webhook (outbound)
  version: "2026-09-18"
paths:
  /{tenantConfiguredPath}:
    post:
      operationId: postNotificationOutcome
      description: >
        URL is RestApiConfig.BaseUrl + Endpoint from the tenant NotificationConfig.
        HttpMethod is usually POST. Auth headers come from RestApiConfig (do not log them).
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: "#/components/schemas/NotificationOutcomePayload"
      responses:
        "2xx":
          description: Adapter treats success as send true (Award IsAwarded true).
        default:
          description: >
            Adapter returns false after its existing RetryConfig (no second retry loop in Award).
            Award sets IsAwarded false; siblings stay. If send throws, ProcessEvent fails
            unless the host switch is on (then same as false).
components:
  schemas:
    NotificationOutcomePayload:
      type: object
      additionalProperties: true
      required:
        - tenantId
        - loyaltyAccountId
        - campaignId
        - ruleSetId
        - issuingOutcomeId
        - issuingOutcomeKind
        - eventId
        - eventType
        - awardedAtUtc
      properties:
        tenantId: { type: string }
        loyaltyAccountId: { type: string }
        extAccountId: { type: string, nullable: true }
        campaignId: { type: string }
        ruleSetId: { type: string }
        issuingOutcomeId: { type: string }
        issuingOutcomeKind: { type: string }
        eventId: { type: string }
        eventType: { type: string }
        eventModelId: { type: string, nullable: true }
        awardedAtUtc: { type: string, format: date-time }
      description: Closed DTO in Journeys.DTO/Models. No raw event JSON, ledger rows, or secrets.
```

**Calculate (before send):** `NotificationConfigId` required or Calculate returns null. Missing / inactive config → null. Config `EventModelId` set and ≠ `state.EventModelId` → null. Happy path `IsAwarded` false until Award. CalculateOnly never Awards / POSTs.

**Award:** `SendNotificationAsync(tenantId, configId, payload)`. Always register `INotificationService` (`AddNotificationsServices`; remove `DisableDataLake` gate). Award loop honors returned `IsAwarded` false.

**Tenant isolation:** event tenant + that tenant’s config only.

## C4 MCP NotificationConfigId

Published matrix for campaign-authoring tools. Existing deposit/spend rows stay. Update `RulesEngineMcpContractSummaryTests` only — do not retarget workflow pin fixtures that embed `2026-06-20` as sample JSON.

```yaml
shared-schema:
  name: rules-engine-mcp-contract
  owner: u4-notification-webhook
  type: RulesEngineMcpContractSummary
  matrixVersion: "2026-09-18"
  additiveCriticalRow:
    id: notification_config_id
    subject: outcome
    kinds: [NotificationOutcome]
    required: [NotificationConfigId]
    notes: GUID string on the outcome. No inline URL. Required for Calculate to return a result.
  keep:
    - existing deposit / spend / expire / tag critical rows
    - OutcomeKindDiscriminators.NotificationOutcome already listed in OutcomeKinds
```

## Contract ownership rules

- **C1** — U3 owns ProcessEvent **order** and the existing HTTP surface (no new routes). U1 owns the hydrate collect appendix. Do not change the public path or invent `/v2`.
- **C2** — U2 owns hop/clock schema. U3 consumes it; U3 must not invent a second dest clock.
- **C3** — U4 owns `NotificationOutcomePayload` and Award/send. External webhook consumers do **not** own the field list. Additive JSON: consumers ignore unknown fields (Q4).
- **C4** — U4 owns the MCP matrix bump and the new critical row.
- **Breaking changes** — require a human-approved spec. Additive fields are safe. MCP version is `MatrixVersion`, not Core assembly semver.
- **Retries** — C3 uses existing `RestApiConfig.Retry` only. Do not add a queue, circuit breaker, or a second idempotency store. Reprocess may POST again (at-least-once).
- **Docs (US5.1)** — ride U4 in the same change as the webhook/MCP code.

## Open questions

| Contract | Question | Blocks |
|----------|----------|--------|
| C3 | Exact host/appsettings key name for “webhook throw is non-fatal” | U4 implementation only — behavior is already decided; name can land in Functional Design / Code Generation |
| — | None else. GET/reconcile outer lock stays deferred (FR3.6). | — |
