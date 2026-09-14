# Security and tenancy

## Current behavior (extract)

`Journeys.API` registers a global MVC filter `RequireLoyaltyAccount` (`Program.cs`). Policy implementation: `Journeys.Infra.Auth/AuthZeroExtensions.cs`.

Allow paths today:

1. `/swagger` â€” bypass
2. Campaign-agent routes `/api/v1/{tenantId}/campaign-agent/...` â€” JWT, header `Journeys-API-KEY` matching `ApiKeys`, or `Auth0:{tenant}:isDevelopment`
3. All other MVC actions â€” development flag, or valid `Journeys-API-KEY`, or JWT with a role in `Auth0:{tenant}:AllowedRoles`

JWT is Auth0 (`JwtBearer`). Every business operation stays `TenantId`-scoped (`docs/product/ontology/tenant.md`).

Committed `appsettings*.json` hold empty keys only. Live credentials: user secrets, environment, or gitignored `appsettings.Local.json` (`docs/developer/local-ops.md`). When `DataLake:ConnectionString` is unset, unconfigured blob/Data Lake adapters keep DI valid so the API can start without those secrets.

## Human-gated debt

Auth0 tenant is hardcoded `"hayward"` in `AuthZeroExtensions.cs` (JWT setup and the authorization assertion). Comments say per-tenant config is TODO. **Agents must not change this.** Per-tenant Auth0 is a human-owned fix.

Also human-only (`AGENTS.md`): auth, ledger/money-like outcomes, tenant isolation, merge and release.

## Isolation

Do not add APIs or services that operate globally on member/program data without an explicit, documented exception. Do not log secrets, tokens, API keys, connection strings, or full event payloads (`docs/developer/logging.md`).
