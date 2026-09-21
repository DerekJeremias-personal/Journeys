# Runtime map

What each store and adapter is for **today**. If this table and code disagree, fix this file in the same change.

| Concern | Where | Notes |
|---------|-------|--------|
| Campaign / journey / account / event persistence | `Journeys.DAL` + Backend data plane (`Backend.*` DLLs) | Customer event-model instances live in Backend. Do not invent a parallel store |
| MassTransit sagas | Cosmos (`MassTransit:SagaRepository`) | Host, key, database `masstransit`, collection `sagas` in config |
| Inbound files, chunks, archive | Data Lake + blob (`Journeys.Infra.DataLake`, `Journeys.Infra.BlobStorage`) | Hosted jobs and live clients register only when `DataLake:ConnectionString` is set. Otherwise unconfigured adapters keep DI valid; storage calls fail or no-op at the call site (`docs/developer/local-ops.md`) |
| Queue | `Journeys.Infra.ServiceBus` | Connection from DI, not source |
| Notifications | `Journeys.Notification` | Adapters only; no campaign/journey rules. Host always registers `INotificationService` (`AddNotificationsServices` is not gated on `DisableDataLake`). |
| Auth | `Journeys.Infra.Auth` | Auth0 + API keys (`docs/platform/security.md`) |
| Key-value | `KeyValueStorage` | URL and key from config. Model catalog list (`/model/all`) is `modelType: loyalty` and omits `ModelId`. Entity routes (`/entity/{modelType}/{modelId}/…`) use the screen/adapter GUID (Campaigns: `CampaignAdapter` campaign model; Accounts query: wrapper GUID for `LoyaltyAccountDetails`). Never send `modelId` `"unknown"`. |
| OpenAI-compatible LLM (Ollama) | `Journeys.Infra.Llm` | Factory stack is `AsIChatClient` then request-options then connect-retry (when `ConnectRetrySeconds` > 0) then `UseFunctionInvocation` as outermost. Retry only connect, refused, reset, or DNS — not 4xx, 5xx, or InvalidResponse. |

Terraform and Databricks folders are not in this tree (`docs/roadmap/non-goals.md`).
