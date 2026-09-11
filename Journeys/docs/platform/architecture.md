# Architecture

Onion (center → edge):

`Journeys.DTO` → `Journeys.Core` → `Journeys.DAL` / `Journeys.Infra*` → `Journeys.API` / `Journeys.Agent` / `Journeys.Notification`  
Tests: `Journeys.Tests`.

Dependencies point inward. Controllers do not contain business logic. Infra talks to Azure and `Backend.*` assemblies.

**Backend DLLs** (not renamed): `..\..\Binaries\Backend.Core.dll`, `Backend.Dto.dll`, `Backend.Llm.Anthropic.dll`.

`WrappedEventPayload` persist JSON uses wrapper-model symbols (all lowercase: `appliedcampaigns`, `outcomestates`, `journeystates`, …), matching `*AndRuleState` attributes. Nested journey/outcome/provider fields are lowercased on persist (`nodememberships`, not `nodeMemberships`). CamelCase CLR names do not bind to List attributes and Backend then casts `DynamicList` to `DynamicEntity`. Event process returns Backend `validationErrors` on `EventPayloadResponseDto.Errors` instead of swallowing them.

**Host:** `Journeys.API` — REST, MCP, campaign-agent HTTP. `Journeys.Agent` — campaign authoring host.

**Secrets:** committed `appsettings*.json` hold empty keys only. Live Azure, Anthropic, Databricks, and similar credentials stay in user secrets, environment variables, or a gitignored `appsettings.Local.json`. Do not hardcode connection strings in Infra adapters.

**Not in this solution:** React/Next UI, terraform, Databricks.
