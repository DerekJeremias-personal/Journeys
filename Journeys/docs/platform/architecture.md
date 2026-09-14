# Architecture

## When onion applies

If this solution **stores user-boundary data**, it must be a full local onion. The product owns those writes through its own adapter implementations.

User-boundary data includes learner- or tenant-owned records such as education goals, achievements, progressions, reminders, career items, profiles, and similar personal or program state.

A UI that only displays another product’s data is not a reason to skip onion in the product that stores it. A UI never becomes the write authority.

## DDD preference (not microservices)

Default shape is a **modular monolith**: one deployable API, one Core, one Dto, many domain modules.

- Split a new process or repo only for a hard reason (separate team, separate scale, separate compliance). A new noun is not a reason.
- **Ubiquitous language** comes from `docs/product/`. Do not invent capability names.
- **Bounded contexts** are folders/modules inside `Journeys.Core` (and matching adapter packages), not separate hosts.
- The product that owns a user-boundary aggregate is the only write authority for that aggregate. Other solutions may call its API; they do not write the aggregate behind its back.
- This preference does **not** imply event sourcing, CQRS, or a message bus. Those require a later ADR.

## Layers (center -> edge)

`Journeys.DTO` -> `Journeys.Core` -> `Journeys.DAL` / `Journeys.Infra*` / `Journeys.Notification` -> `Journeys.API` / `Journeys.Agent`

Generic names: `Dto` -> `Core` -> `Adapters` -> `API`. This product’s Adapters are `Journeys.DAL`, `Journeys.Infra*`, and `Journeys.Notification`. See `overlays.md`.

Tests: `Journeys.Tests`.

Dependencies point inward. Controllers do not contain business logic. Infra talks to Azure and `Backend.*` assemblies.

| Layer | This product | Must not |
|---|---|---|
| **API** | `Journeys.API`, `Journeys.Agent` | Business rules. Inject adapters by default. |
| **Core** | `Journeys.Core` | Azure / Cosmos / store HTTP clients. UI types. |
| **Adapters** | `Journeys.DAL`, `Journeys.Infra*`, `Journeys.Notification` | Product business rules. |
| **Dto** | `Journeys.DTO` (required) | Business rules. Persistence SDKs. Core domain services. |
| **UI** | Not in this solution | Project-reference Core or Adapters. |

Drift control: a later shared-rule change is made in Backend, Journeys, and GoEducation in the same increment; the template is a starter kit only.

## Conflict rule

1. Explicit user instruction in the current turn wins.
2. For layering, DDD, and UI/API boundaries, this file and `.cursor/rules` win.
3. For product behavior, `docs/product/` wins.

## Journeys-specific

**Backend DLLs** (not renamed): `..\..\Binaries\Backend.Core.dll`, `Backend.Dto.dll`, `Backend.Llm.Anthropic.dll`.

`WrappedEventPayload` persist JSON uses wrapper-model symbols (all lowercase: `appliedcampaigns`, `outcomestates`, `journeystates`, …), matching `*AndRuleState` attributes. Nested journey/outcome/provider fields are lowercased on persist (`nodememberships`, not `nodeMemberships`). CamelCase CLR names do not bind to List attributes and Backend then casts `DynamicList` to `DynamicEntity`. Event process returns Backend `validationErrors` on `EventPayloadResponseDto.Errors` instead of swallowing them.

**Host:** `Journeys.API` — REST, MCP, campaign-agent HTTP. `Journeys.Agent` — campaign authoring host.

**Secrets:** committed `appsettings*.json` hold empty keys only. Live Azure, Anthropic, Databricks, and similar credentials stay in user secrets, environment variables, or a gitignored `appsettings.Local.json`. Do not hardcode connection strings in Infra adapters. Serilog Azure Analytics is registered only when workspace id and authentication id are both set; otherwise the host logs to console. Blob, Data Lake, chunk, and archive hosted services register only when `DataLake:ConnectionString` is set.

**Not in this solution:** React/Next UI, terraform, Databricks.
