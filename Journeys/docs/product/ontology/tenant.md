# Ontology: tenant

Journeys is multi-tenant. A **tenant** is the isolation boundary for customer-owned event models, campaigns, journeys, accounts, ledgers, and outcomes. Constraint id: `tenant-id-required`. It is **not** a capability — do not invent `tenants` as a capability id.

Every business function is scoped by **`TenantId`**. Do not add APIs, MCP tools, Core services, or DAL reads that operate globally on member/program data without an explicit, documented exception.

Persona: `tenant-admin` (tenant-scoped Auth0 / API key access and configuration). Isolation, auth, and ledger/money-like outcomes are human-gated (`AGENTS.md`, `docs/platform/security.md`).

## What `TenantId` is

The string that partitions **all program data**. It travels on the HTTP route (typically `{tenantId}`), into Core method arguments, onto `TenantedModelBase` / `DtoModelBase`, and into Backend/DAL queries. Persistence upserts normalize it to **lowercase** (`Journeys.DAL/Adapters/BaseAdapter.cs`). Backend 404 JSON must not invent `modelId: "unknown"` when a model id was missing; omit or leave empty.

It is **not**:

- The Auth0 config key `"hayward"` (JWT authority/audience lookup). That name is hardcoded debt in `Journeys.Infra.Auth/AuthZeroExtensions.cs`. **Agents must not change it.** Per-tenant Auth0 is a human-owned fix. Do not treat `"hayward"` as this product’s `TenantId`.
- An AI-DLC space, intent, or `aidlc/` record. Product tenancy lives here; AI-DLC is execution state only.
- A prompt-only label. If a tool or service mutates campaigns, accounts, or events, `tenantId` is a required argument, not implied by chat.

## Shape

| Piece | Meaning | Code |
|-------|---------|------|
| **`TenantId` on operations** | First-class argument on Core services, DAL adapters, MCP data tools, and controllers. | `ICampaignService`, `ILoyaltyAccountService`, `IRulesService`, `IEventService`, … |
| **Tenanted entity** | Stored records carry `TenantId` (and etag). | `Journeys.Core/Models/TenantedModelBase.cs`, `Journeys.DTO/Models/DtoModelBase.cs` |
| **Tenant registry** | Backend catalog of tenant *rows* (name, marketing context, warehouse config, test-account allowlist). HTTP is global `api/Tenant`, **not** `api/{tenantId}/...`. | `ITenantDataAdapter`, `Journeys.Infra.Backend/TenantAdapter.cs` |
| **External reference key** | Deterministic id includes tenant: `{tenantId}\|type\|mapFromId` (normalized). | `ExternalReferenceAdapter` |

Campaign fetch, account fetch, event process, point ledgers, historical rule TTL, taxonomy, ingest, and notifications all take the same `TenantId`. A campaign id or account id is **not** unique across tenants.

## Documented exception: tenant registry

`ITenantDataAdapter` is the **only** Core-facing API that is not itself scoped under `api/{tenantId}/...`. It lists and saves **tenant records**, not member data.

Using `GetAllTenantsAsync` / `GetManyTenantsAsync` to walk **loyalty accounts, campaigns, or events** is forbidden. Using it to resolve the registry row for the current route key (name or id) is allowed.

Warehouse config (`DatawarehouseService`) loads the registry row by **name** (`GetTenantByNameAsync(tenantId)`), then saves that row. That is still tenant-scoped business data sitting *on* the registry record — pass the same `TenantId` the caller used; do not scan other tenants.

## HTTP, MCP, and the campaign agent

- **REST:** `{tenantId}` is a route value on business actions (events, accounts, campaigns, ingest, campaign-agent). Controllers validate and pass it through; they do not invent a second scope.
- **MCP data tools / resources:** `tenantId` is required (`journeys://campaign/{tenantId}/...`, `get_campaign`, `upsert_campaign`, account and PAT tools). Empty tenant id is an error.
- **MCP catalogs with no tenant state (allowed):** `get_rules_engine_contract_summary`, `get_rule_pattern_recipes`, and equivalent `journeys://rules-engine/...` resources. Those are engine contracts, not member/program data.
- **Campaign agent:** route `tenantId` is the tenancy key for every Core call. Registry lookup for LLM context defaults to **by name** (`CampaignAgent:TenantLookupByName`). The LLM slice is marketing / end-consumer copy plus `CampaignTestAccountExtIds` — not a second write path. Verification against live draft events is blocked unless the account external id is on that allowlist (`CampaignTestAccountAllowlist`, `TenantVerificationContextLoader`).

Do not merge two tenants’ context into one agent turn. Do not persist the LLM slice as campaign JSON.

## Runtime isolation

1. Every DAL `Get*` / `Set*` takes `tenantId` and passes it to the Backend data plane. There is no “all accounts” query without a tenant.
2. Rules evaluation hydrates and persists under `engineState.TenantId` (`docs/product/ontology/rule.md`).
3. Historical rule TTL, journey membership, and point ledgers are per account **inside** a tenant.
4. Logs for tenant-scoped calls include structured `TenantId`. Do not log secrets, tokens, API keys, or full event payloads (`docs/developer/logging.md`).
5. Data Lake / blob credentials are not a tenancy boundary. The API can start without `DataLake:ConnectionString`; tenant-scoped Core calls still require `TenantId`. Ingest and file export fail at those call sites until storage is configured (`docs/developer/local-ops.md`).

## Governance for agents

- **Always pass `TenantId`.** New Core methods, MCP tools, and adapters that touch program data take it as an argument. Do not default it from Auth0 `"hayward"`, from `appsettings`, or from a previous turn.
- **Do not redesign isolation.** Partitioning, registry vs data-plane split, and Auth0 per-tenant config are human-gated.
- **Do not invent a second tenant store** in Cosmos, blobs, or prompts. Customer instances stay in the Backend data plane (`docs/platform/runtime.md`).
- **Registry writes** (`SaveTenantAsync`, warehouse secrets on the tenant row) are configuration, still tenant-keyed, and still human-gated when they touch credentials.
- **Test accounts** used for draft verification must be on `CampaignTestAccountExtIds`. Do not process arbitrary live members from the agent.
- If this file and code disagree, **code is canonical** — update this ontology in the same change.

## Related

`campaign.md`, `loyalty-account.md`, `event-model.md`, `rule.md`, `draft-live.md`. Platform: `docs/platform/security.md`. Constraint taxonomy: `docs/product/taxonomies/constraints.md`.
