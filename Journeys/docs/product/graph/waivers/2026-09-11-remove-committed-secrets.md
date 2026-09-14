# Waiver: remove committed secrets

**Reason:** Strip live Azure / Anthropic / Databricks credentials from committed config and `ServiceBusAdapter`. Startup skips Serilog Azure Analytics when the authentication id is unset, and skips blob/Data Lake hosted services when `DataLake:ConnectionString` is unset. No product capability, ontology, or graph edge change.

**Nodes touched by path-map (no meaning change):** `mcp-api`, `campaigns`, `campaign-agent`.
