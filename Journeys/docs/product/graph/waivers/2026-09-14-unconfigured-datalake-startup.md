# Waiver: unconfigured Data Lake adapters

**Reason:** When `DataLake:ConnectionString` is unset, register unconfigured blob/Data Lake adapters so DI can construct `LoyaltyAccountService` and the host can start. Chunk/archive jobs stay unregistered. No product capability, ontology, or graph edge change.

**Nodes touched by path-map (no meaning change):** `mcp-api`, `campaigns`, `campaign-agent`.
