# Runtime map

What each store and adapter is for **today**. If this table and code disagree, fix this file in the same change.

| Concern | Where | Notes |
|---------|-------|--------|
| Campaign / journey / account / event persistence | `Journeys.DAL` + Backend data plane (`Backend.*` DLLs) | Customer event-model instances live in Backend. Do not invent a parallel store |
| MassTransit sagas | Cosmos (`MassTransit:SagaRepository`) | Host, key, database `masstransit`, collection `sagas` in config |
| Inbound files, chunks, archive | Data Lake + blob (`Journeys.Infra.DataLake`, `Journeys.Infra.BlobStorage`) | Hosted jobs register only when `DataLake:ConnectionString` is set |
| Queue | `Journeys.Infra.ServiceBus` | Connection from DI, not source |
| Notifications | `Journeys.Notification` | Adapters only; no campaign/journey rules |
| Auth | `Journeys.Infra.Auth` | Auth0 + API keys (`docs/platform/security.md`) |
| Key-value | `KeyValueStorage` | URL and key from config |

Terraform and Databricks folders are not in this tree (`docs/roadmap/non-goals.md`).
