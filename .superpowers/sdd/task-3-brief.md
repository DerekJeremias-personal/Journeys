### Task 3: Security, runtime, logging, coding standards

**Files:**
- Create: `docs/platform/security.md`
- Create: `docs/platform/runtime.md`
- Create: `docs/developer/logging.md`
- Create: `docs/developer/coding-standards.md`
- Modify: `docs/platform/index.md`
- Modify: `docs/developer/index.md`

**Interfaces:**
- Consumes: `Journeys.Infra.Auth/AuthZeroExtensions.cs`, `Journeys.API/Program.cs`, `ConfigureInfra.cs`, path rules
- Produces: platform/developer pages linked from indexes

- [ ] **Step 1: Create `docs/platform/security.md` with this exact body**

```markdown
# Security and tenancy

## Current behavior (extract)

`Journeys.API` registers a global MVC filter `RequireLoyaltyAccount` (`Program.cs`). Policy implementation: `Journeys.Infra.Auth/AuthZeroExtensions.cs`.

Allow paths today:

1. `/swagger` â€” bypass
2. Campaign-agent routes `/api/v1/{tenantId}/campaign-agent/...` â€” JWT, header `Journeys-API-KEY` matching `ApiKeys`, or `Auth0:{tenant}:isDevelopment`
3. All other MVC actions â€” development flag, or valid `Journeys-API-KEY`, or JWT with a role in `Auth0:{tenant}:AllowedRoles`

JWT is Auth0 (`JwtBearer`). Every business operation stays `TenantId`-scoped (`docs/product/ontology/tenant.md`).

Committed `appsettings*.json` hold empty keys only. Live credentials: user secrets, environment, or gitignored `appsettings.Local.json` (`docs/developer/local-ops.md`).

## Human-gated debt

Auth0 tenant is hardcoded `"hayward"` in `AuthZeroExtensions.cs` (JWT setup and the authorization assertion). Comments say per-tenant config is TODO. **Agents must not change this.** Per-tenant Auth0 is a human-owned fix.

Also human-only (`AGENTS.md`): auth, ledger/money-like outcomes, tenant isolation, merge and release.

## Isolation

Do not add APIs or services that operate globally on member/program data without an explicit, documented exception. Do not log secrets, tokens, API keys, connection strings, or full event payloads (`docs/developer/logging.md`).
```

- [ ] **Step 2: Create `docs/platform/runtime.md` with this exact body**

```markdown
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
```

- [ ] **Step 3: Create `docs/developer/logging.md` with this exact body**

```markdown
# Logging

## Sinks

`Journeys.API` uses Serilog (`Program.cs`). Azure Analytics is registered only when `Serilog:WriteTo:0:Args:workspaceId` and `authenticationId` are both set. Otherwise the host logs to the console. `JourneysLog` and `JourneysErrorLog` are config names for those table/id settings â€” not a second logging API.

## What to include

When the call is tenant-scoped, use structured properties and include `TenantId` (existing controllers already do this on errors).

## What not to log (interim)

Do not log secrets, API keys, JWT or raw tokens, connection strings, or full event payloads. Do not add new email or account-xref fields to logs. Do not add new startup lines that print workspace or authentication id prefixes. Existing `Program.cs` startup diagnostics stay until a human removes them.
```

- [ ] **Step 4: Create `docs/developer/coding-standards.md` with this exact body**

```markdown
# Coding standards

Extracted from `.cursor/rules` and existing Core/API habits. Do not invent a second aesthetic.

- Onion: controllers validate and delegate; services in `Journeys.Core`; persistence in `Journeys.DAL`; Azure/Auth/Backend/bus/blob in `Journeys.Infra*`. See `docs/platform/architecture.md`.
- Controller and service in/out types are `Journeys.DTO`. `Journeys.Core.Models` stays internal to Core.
- Public async methods are named `...Async` and return `Task` / `Task<T>`.
- Constructor injection; readonly `_camelCase` fields.
- Validation: `APIErrorsException` with `Dictionary<string, string>`; unexpected errors go through `GlobalExceptionMiddleware`.
- Enums serialize as strings (`JsonStringEnumConverter`).
- Prefer `CancellationToken` on async actions when the service stack supports it.
- Tests live in `Journeys.Tests`, existing folders, Arrangeâ€“Actâ€“Assert (`docs/developer/testing.md`).
- Do not delete existing comments without cause.
- Do not take Azure SDK or Backend client dependencies in Core.
```

- [ ] **Step 5: Replace `docs/platform/index.md` with**

```markdown
# Platform

How Journeys is built. Product *meaning* is `docs/product/`.

1. [Architecture](architecture.md) â€” onion, DDD, projects, Backend DLLs
2. [Overlays](overlays.md) â€” generic layer names mapped to Journeys projects
3. [Runtime](runtime.md) â€” Cosmos, bus, lake, Backend, notifications
4. [Security](security.md) â€” Auth0, API keys, tenancy, human-gated debt
5. [Decisions](decisions/0001-onion-Journeys-rename.md) â€” ADRs
6. Developer ops: `docs/developer/`
```

- [ ] **Step 6: Replace `docs/developer/index.md` with**

```markdown
# Developer

- [Local ops](local-ops.md) â€” restore, build, and local secrets
- [Testing](testing.md)
- [Logging](logging.md) â€” Serilog sinks and redaction
- [Coding standards](coding-standards.md)
- [Tools](tools.md) â€” CampaignContextAudit outside this folder
```

- [ ] **Step 7: Verify**

```powershell
Select-String -Path docs\platform\security.md -Pattern 'hayward','RequireLoyaltyAccount','Journeys-API-KEY' | Measure-Object | Select-Object -ExpandProperty Count
Select-String -Path docs\platform\runtime.md -Pattern "MassTransit","DataLake","Backend" | Measure-Object | Select-Object -ExpandProperty Count
Select-String -Path docs\platform\index.md -Pattern "security.md","runtime.md" | Measure-Object | Select-Object -ExpandProperty Count
```

Expected: each count â‰¥ 3, â‰¥ 3, â‰¥ 2. Do not commit.

---

