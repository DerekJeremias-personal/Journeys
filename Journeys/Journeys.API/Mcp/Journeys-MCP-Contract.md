# Journeys MCP server – tools and resources

The Journeys MCP server exposes the following tools and resources for agentic clients (e.g. Journeys.Agent). Base path: `/mcp` (e.g. `https://<api-host>/mcp`).

## Tools (read-only)

| Tool | Description | Parameters |
|------|-------------|------------|
| **GetCampaign** | Get a campaign by tenant, campaign ID, and optional status | tenantId, campaignId, status? (default: Live) |
| **GetCampaignByExtId** | Get a campaign by tenant and external campaign ID (Live or Draft) | tenantId, extCampaignId, status? (default: Live) |
| **ListCampaigns** | List campaigns for a tenant by status (paged) | tenantId, status?, pageSize?, continuationToken? |
| **ListCampaignVersions** | List all versions (Live/Draft/Archive) of a campaign by external ID | tenantId, extCampaignId |
| **GetSegmentsFromCampaign** | Get only the segments for a campaign | tenantId, campaignId, status? |
| **GetJourneyFromCampaign** | Get the journey definition (nodes, navigation) for a campaign | tenantId, campaignId, status? |
| **GetCampaignStats** | Get campaign statistics | tenantId, campaignId |
| **GetAccount** | Get a loyalty account by tenant and account ID | tenantId, accountId, resettleIfNeeded? |
| **GetPointAccountType** | Get a point account type by tenant and ID | tenantId, pointAccountTypeId |
| **ListPointAccountTypes** | List point account types for a tenant (paged) | tenantId, pageSize?, continuationToken? |
| **PreviewTierMove** | Preview effect of moving an account to a tier (read-only; no state change) | tenantId, requestJson (MoveTierRequest) |
| **GetIngestionFolders** | List ingestion folder names for a tenant | tenantId |
| **GetIngestionSummary** | Get chunk summary for a file | tenantId, folderName, fileName |
| **GetCampaignAssistantContext** | Assistant context for a saved campaign (attribute rows, processing contract, sample scaffold) | tenantId, campaignId, status?, includeSampleTemplate? |
| **GetRulesEngineContractSummary** | Tier A rules-engine contract JSON (rule/outcome/provider kinds, evaluator types, enum catalog, required fields, violation codes); no tenant state | _(none)_ |
| **ListExampleCampaigns** | List repo-backed example campaign JSON (non-customer) | tenantId (**must be `mericantires`**) |
| **GetExampleCampaign** | Raw JSON for one example campaign by id | tenantId (**must be `mericantires`**), exampleId |

### GetCampaignAssistantContext response

Top-level campaign digests (dual delivery with WORKFLOW ARTIFACTS — same builder shapes):

| Field | Type | Description |
|-------|------|-------------|
| `campaignShell` | `CampaignShellDigest` | Normalized shell: campaign id, name, status, **EventModelIds**, dates, **HasJourneyPayload**, **ineligibleEventModelIds** (not eventable), warnings |
| `pointAccountManifest` | `PointAccountManifestDigest` | PAT entries referenced in journey outcomes: id, displayLabel, ledgerType, isSpendable, status, **role** (`spendable` \| `tierQualification` \| `escrow` \| `other`) |
| `journey` | `CampaignJourneyArtifactDigest` | Journey summary: node/rule counts, outcome kind counts, **referencedPointAccountTypes** (usage + **inManifest**), **unresolvedPatIds**, warnings |

**WORKFLOW ARTIFACTS** (phased host prompt) also persist **`EventModelContracts`** as a JSON array of `EventProcessingContractDigest`. **`EventModelContract`** (singular) is a deprecated mirror of the same array for one release — prefer **`EventModelContracts`** when both appear.

Each entry in **`eventModels[]`** may include **`processingContract`** (`EventProcessingContractDigest`):

| Field | Description |
|-------|-------------|
| `schemaVersion` | Digest schema version (currently `1`) |
| `eventModelId`, `eventModelName`, `eventModelType` | Event payload model identity |
| `wrapperModelId` | Wrapper (container) model id from event `modelMetaData.Wrapper` |
| `accountLink.symbolPath` | `AccountXIdSymbol` path on the ProcessEvent root JSON (loyalty account lookup via `GetLoyaltyAccountByExtIdAsync`) |
| `accountLink.role` | `"loyalty_external_id"` |
| `naturalKey.symbols` | Ordered symbols from `NaturalKeySymbols` (idempotency key components) |
| `naturalKey.joinWith` | Join delimiter (default `"|"`) |
| `naturalKey.role` | `"idempotency_key"` |
| `timeOfOccurrenceSymbol` | `TimeOfOccurrence` attribute symbol |
| `modelTag` | Backend `ModelDto.tag` (must be `"eventable"` for ProcessEvent) |
| `isProcessEventEligible` | `true` when `modelTag == "eventable"` |
| `isLoyaltyAccountCreationEvent` | `true` when `modelMetaData.IsLoyaltyAccount` is true |
| `processingRole` | `"standard_event"` or `"loyalty_account_creation"` |
| `processingRoleDescription` | Human-readable semantics for the role |
| `warnings` | Non-fatal contract gaps (e.g. missing Wrapper, missing eventable tag) |

When **`includeSampleTemplate`** is true, **`sampleScaffold`** includes:

| Field | Description |
|-------|-------------|
| `fieldRoles` | Map of symbol path → role: `"accountLink"`, `"naturalKey"`, or `"attribute"` |
| `accountLinkSymbolPath` | Same path as `processingContract.accountLink.symbolPath` |
| `naturalKeySymbols` | Same list as `processingContract.naturalKey.symbols` |
| `requiredEventSymbols` | Live attribute symbols included in the template checklist |
| `jsonTemplate` | Minimal JSON fixture for **ProcessEvent** |

**Breaking change:** **`sampleScaffold.jsonTemplate`** is the **root ProcessEvent event payload** — a flat object with placeholders for required symbols. It is **not** wrapped in `{ "event": ... }`. Pass it directly as **`eventJson`** on **ProcessEvent**.

## Tools (mutating)

| Tool | Description | Parameters |
|------|-------------|------------|
| **UpsertCampaign** | Create or update a campaign (JSON payload) | tenantId, campaignJson |
| **DeleteCampaign** | Delete a campaign by id and status (same semantics as HTTP `DELETE …/{tenantId}/{campaignId}`) | tenantId, campaignId, status? (default: Live) |
| **UpsertPointAccountType** | Create or update a point account type (`PointAccountTypeDto` JSON) | tenantId, pointAccountTypeJson |
| **ProcessEvent** | Process an event through the loyalty engine (sandbox/what-if; mutates state) | tenantId, modelName, eventJson, reprocessEvent? |
| **MoveTier** | Move an account to a tier (admin). Gate with AllowMoveTier in agent config. | tenantId, requestJson (MoveTierRequest; include AdminUserId) |

## Resources (read-only)

| URI template | Name | Description |
|--------------|------|-------------|
| `journeys://campaign/{tenantId}/{campaignId}` | Journeys Campaign | Campaign by tenant and ID (Live status) |
| `journeys://campaign/{tenantId}/{campaignId}/segments` | Journeys Campaign Segments | Segments of a campaign (Live status) |
| `journeys://journey/{tenantId}/{campaignId}` | Journeys Journey | Journey definition only (nodes, rules, navigation; Live status) |
| `journeys://account/{tenantId}/{accountId}` | Journeys Loyalty Account | Loyalty account by tenant and ID |
| `journeys://point-account-type/{tenantId}/{pointAccountTypeId}` | Journeys Point Account Type | Point account type by tenant and ID |
| `journeys://rules-engine/campaign-contract/v1` | Rules engine campaign contract (Tier A) | Static JSON: kinds, critical required fields, violation codes (same as **GetRulesEngineContractSummary**) |
| `journeys://examples/campaigns` | ListExampleCampaignsResource | JSON list of example campaign ids/titles/summaries (tenant `mericantires`) |
| `journeys://examples/campaign/{exampleId}` | GetExampleCampaignResource | Raw JSON for one example campaign |

## Error responses (domain validation)

Tools that call Journeys services (reads and writes) can receive the same **`APIErrorsException`** as HTTP when the backend or engine validates domain rules. When the service throws **`APIErrorsException`**, tools return JSON of the form:

```json
{ "errors": { "fieldOrCode": "human-readable message", "...": "..." } }
```

The **`errors`** object matches the HTTP validation body where applicable. Mutating tools (**`ProcessEvent`**, **`UpsertCampaign`**, **`DeleteCampaign`**, **`UpsertPointAccountType`**, **`PreviewTierMove`**, **`MoveTier`**) and read/list tools that call **`ICampaignService`**, **`ILoyaltyAccountService`**, or point-account-type APIs (**`GetCampaign`**, **`ListCampaigns`**, **`GetAccount`**, etc.) use this shape whenever **`APIErrorsException`** is thrown (e.g. adapter **`NOT_FOUND`** or campaign validation).

**`GetAccount`** and **`ProcessEvent`** also catch unexpected service exceptions and return `{ "errors": { "getAccount" | "processEvent": "<TypeName>: <message>" } }` instead of throwing, so agents receive structured failures when possible.

For other failures (e.g. **`ArgumentException`** from invalid JSON arguments on tools that still throw), the MCP host may surface an opaque `isError` string — the campaign agent enrichment layer maps those to `_agentRemediation` when detectable.

## Tenant and safety

- All tools and resources require **tenantId**. The MCP server does not add or override tenant; pass it explicitly.
- **ProcessEvent** changes loyalty state. Restrict use to sandbox/dev or gate behind policy (e.g. Agent `AllowProcessEvent`).
- **MoveTier** changes account tier assignment. Restrict to admin use; Agent filters it when `AllowMoveTier` is `false`.
- **UpsertCampaign** creates/updates campaigns; ensure tenant and payload are validated for your environment.
- **DeleteCampaign** removes a campaign version by id and status; irreversible for that row—use only with explicit user intent.
- **UpsertPointAccountType** creates/updates point account types (same semantics as HTTP `POST .../pointaccounttype/upsert`).

## Campaign agent tool-result digests

When the HTTP Campaign Agent merges Journeys MCP tools, several read/mutation tools return **digested JSON** to the model (and persist the compact payload in Cosmos) when digest flags are enabled (defaults **on**):

| Tool | Config flag | Digest behavior |
|------|-------------|-----------------|
| **GetCampaignAssistantContext** | `CampaignAgent:ReadToolDigestEnabled` | Compact assistant-context digest |
| **UpsertCampaign** | `CampaignAgent:MutationDigestEnabled` | Success → compact mutation ack; failures pass through verbatim |
| **GetRulesEngineContractSummary** | `CampaignAgent:RulesContractDigestEnabled` | Compact Tier A matrix (keeps `criticalRows`, `enumCatalog`, violation codes) |
| **GetExampleCampaign** | `CampaignAgent:ExampleCampaignDigestEnabled` | Skeleton summary (counts + ids); errors pass through |

For the **full** static rules contract (unabridged matrix), use MCP resource **`journeys://rules-engine/campaign-contract/v1`** or call the tool with digests disabled in agent config.

## Dynamic loyalty models (`SaveModel`, `ListModels`, …)

Those tools are **not** on this Journeys `/mcp` server. They are provided by **BackEnd.Web** MCP (e.g. `https://localhost:7155/mcp`). **`Journeys.Agent`** merges them when **`BackendMcpServerUrl`** is set and passes **`AllowSaveModel`** for mutating saves. The HTTP Campaign Agent merges the same Backend MCP using **`CampaignAgent:BackendMcpEndpointUrl`**, typically with a model-catalog allowlist and **`CampaignAgent:AllowBackendSaveModel`** for `SaveModel`. Set **`CampaignAgent:ExposeFullBackendMcpToolSurface`** to `true` to merge **all** Backend MCP tools (not only model definitions).

## Implementation

- **Tools:** `Journeys.API/Mcp/JourneysMcpTools.cs`, `JourneysMcpExampleTools.cs` (example campaigns); Tier A contract payload: `RulesEngineMcpContractSummary.cs`
- **Resources:** `Journeys.API/Mcp/JourneysMcpResources.cs`, `JourneysMcpExampleResources.cs`
- **Example pack:** `Journeys.API/Examples/Journeys/` (`campaigns-index.json`, `Campaigns/*.json`; tenant `mericantires`)
- **Registration:** `Program.cs` – `AddMcpServer().WithHttpTransport().WithTools<JourneysMcpTools>().WithTools<JourneysMcpExampleTools>().WithResources<JourneysMcpResources>().WithResources<JourneysMcpExampleResources>()`, `app.MapMcp("/mcp")`
- **A2A:** `GET /.well-known/agent-card.json`, `POST /a2a/rpc` — see [Journeys-A2A-Contract.md](./Journeys-A2A-Contract.md)
