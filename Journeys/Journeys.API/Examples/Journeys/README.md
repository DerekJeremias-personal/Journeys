# Journeys example campaigns (MCP)

- **Tenant:** bundled examples use `mericantires` (fictitious, non-customer).
- **Index:** `campaigns-index.json` lists `id`, `title`, `summary`, and `relativeFile` (path under `Examples/Journeys/`).
- **Add examples:** drop JSON under `Campaigns/`, add an `items[]` entry, rebuild/run.
- **MCP:** `journeys://examples/campaigns`, `journeys://examples/campaign/{exampleId}` and tools `ListExampleCampaigns`, `GetExampleCampaign` (tenantId must be `mericantires`).
