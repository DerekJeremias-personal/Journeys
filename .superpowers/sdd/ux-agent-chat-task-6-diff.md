# Review package Task 6

diff --git a/Journeys/docs/developer/journeys-ux.md b/Journeys/docs/developer/journeys-ux.md
index 6f2f87c..8a35ca7 100644
--- a/Journeys/docs/developer/journeys-ux.md
+++ b/Journeys/docs/developer/journeys-ux.md
@@ -26,6 +26,12 @@ Each Loyalty screen owns its Backend model. Do not send `modelId: "unknown"`.
 | **Campaigns** | `POST campaigns/{tenant}/getall` with page size only | `CampaignAdapter` constant `CAMPAIGN_MODEL_ID` `eeae67ca-7bf9-4d2b-9131-83717b219a3a` (platform campaign container). Model type is always `loyalty`. |
 | **Accounts** | `POST events/{tenant}/LoyaltyAccountDetails/admin/query` | Screen name `LoyaltyAccountDetails` (customer event model). `EventService` loads that schema from the tenant catalog, then queries the **wrapper** model GUID from its `Wrapper` metadata. The loyalty-account container GUID `e2cc2404-c60b-4cbc-9f50-6db7ee58e01d` is used by DAL account adapters, not this list. |
 
+## Agent
+
+Unlabeled chat at `/loyalty/campaigns/agent` (link from Campaigns). Browser POSTs to Next `/api/loyalty/campaign-agent/messages/stream`; the server forwards to `POST /api/v1/{tenantId}/campaign-agent/messages/stream`. Secrets stay on the Next server. LLM provider is API startup config (`docs/developer/campaign-agent-llm.md`), not a UX control.
+
+Live smoke: API + Ollama tray ΓåÆ sign in ΓåÆ Campaigns ΓåÆ Agent ΓåÆ one turn with streamed assistant text and at least one successful MCP tool. Not required: Draft upsert or Live publish.
+
 Catalog list (`/model/all`) sends `modelType: loyalty` and **omits** `ModelId`. Entity routes require a real GUID and never coalesce a missing id to `"unknown"`. After C# adapter changes, **restart `Journeys.API`** in Visual Studio.
 
 ## This specΓÇÖs screens
diff --git a/Journeys/docs/product/graph/path-map.yaml b/Journeys/docs/product/graph/path-map.yaml
index bde795e..9cee0a8 100644
--- a/Journeys/docs/product/graph/path-map.yaml
+++ b/Journeys/docs/product/graph/path-map.yaml
@@ -1,7 +1,10 @@
 # Longest prefix wins. Paths are relative to C:\Dev\Journeys\Journeys and use /.
 entries:
+  - prefix: Journeys.Infra.Llm
+    nodes: [campaign-agent]
+    meaningOptional: false
   - prefix: Journeys.UX
-    nodes: [campaigns]
+    nodes: [campaigns, campaign-agent]
     meaningOptional: false
   - prefix: Journeys.Core/RulesEngine/Outcomes
     nodes: [outcomes]
diff --git a/Journeys/docs/platform/architecture.md b/Journeys/docs/platform/architecture.md
index 6e83439..7eced64 100644
--- a/Journeys/docs/platform/architecture.md
+++ b/Journeys/docs/platform/architecture.md
@@ -20,7 +20,7 @@ Default shape is a **modular monolith**: one deployable API, one Core, one Dto,
 
 ## Layers (center -> edge)
 
-`Journeys.DTO` -> `Journeys.Core` -> `Journeys.DAL` / `Journeys.Infra*` / `Journeys.Notification` -> `Journeys.API` / `Journeys.Agent`. `Journeys.UX` is an HTTP client of `Journeys.API` (not in this C# arrow).
+`Journeys.DTO` -> `Journeys.Core` -> `Journeys.DAL` / `Journeys.Infra*` / `Journeys.Notification` -> `Journeys.API` / `Journeys.Agent`. `Journeys.UX` is an HTTP client of `Journeys.API` (not in this C# arrow); it may proxy Campaign Agent SSE and must not own campaign writes. `Journeys.Infra.Llm` is an HTTP client to OpenAI-compatible endpoints (Ollama) for Campaign Agent; it is not `Backend.Llm.OpenAICompatible`.
 
 Generic names: `Dto` -> `Core` -> `Adapters` -> `API`. This productΓÇÖs Adapters are `Journeys.DAL`, `Journeys.Infra*`, and `Journeys.Notification`. See `overlays.md`.
 
@@ -34,7 +34,7 @@ Dependencies point inward. Controllers do not contain business logic. Infra talk
 | **Core** | `Journeys.Core` | Azure / Cosmos / store HTTP clients. UI types. |
 | **Adapters** | `Journeys.DAL`, `Journeys.Infra*`, `Journeys.Notification` | Product business rules. |
 | **Dto** | `Journeys.DTO` (required) | Business rules. Persistence SDKs. Core domain services. |
-| **UI** | `Journeys.UX` | Project-reference Core or Adapters. Own writes of campaigns/accounts. |
+| **UI** | `Journeys.UX` (may proxy Campaign Agent SSE) | Project-reference Core or Adapters. Own writes of campaigns/accounts. |
 
 Drift control: a later shared-rule change is made in Backend, Journeys, and GoEducation in the same increment; the template is a starter kit only.
 
@@ -50,7 +50,7 @@ Drift control: a later shared-rule change is made in Backend, Journeys, and GoEd
 
 `WrappedEventPayload` persist JSON uses wrapper-model symbols (all lowercase: `appliedcampaigns`, `outcomestates`, `journeystates`, ΓÇª), matching `*AndRuleState` attributes. Nested journey/outcome/provider fields are lowercased on persist (`nodememberships`, not `nodeMemberships`). CamelCase CLR names do not bind to List attributes and Backend then casts `DynamicList` to `DynamicEntity`. Event process returns Backend `validationErrors` on `EventPayloadResponseDto.Errors` instead of swallowing them.
 
-**Host:** `Journeys.API` ΓÇö REST, MCP, campaign-agent HTTP. `Journeys.Agent` ΓÇö campaign authoring host.
+**Host:** `Journeys.API` ΓÇö REST, MCP, campaign-agent HTTP. `Journeys.Agent` ΓÇö campaign authoring host. OpenAI-compatible (Ollama) chat clients from `OpenAICompatibleLlmChatClientFactory` keep `FunctionInvokingChatClient` outermost, with connect-retry inside that layer so `CampaignAgentChatClientStackBuilder` does not add a second function-invocation wrapper.
 
 **Secrets:** committed `appsettings*.json` hold empty keys only. Live Azure, Anthropic, Databricks, and similar credentials stay in user secrets, environment variables, or a gitignored `appsettings.Local.json`. Do not hardcode connection strings in Infra adapters. Serilog Azure Analytics is registered only when workspace id and authentication id are both set; otherwise the host logs to console. When `DataLake:ConnectionString` is set, blob clients, Data Lake, and chunk/archive hosted services register. When it is unset, the host still starts: scoped unconfigured adapters satisfy DI (`IDataLakeAdapter`, `IFileStorageAdapter`, `IFileIngestionAdapter`), chunk/archive jobs are not registered, and blob/Data Lake calls fail at the call site (account-report existence checks and campaign-agent tool-audit appends no-op).
 
diff --git a/Journeys/docs/platform/overlays.md b/Journeys/docs/platform/overlays.md
index 1399f19..e5a2155 100644
--- a/Journeys/docs/platform/overlays.md
+++ b/Journeys/docs/platform/overlays.md
@@ -5,7 +5,7 @@
 | API | `{Product}.API` | `Journeys.API`, `Journeys.Agent` |
 | Core | `{Product}.Core` | `Journeys.Core` |
 | Dto | `{Product}.Dto` | `Journeys.DTO` |
-| Adapters | `{Product}.Adapters` | `Journeys.DAL` + `Journeys.Infra*` + `Journeys.Notification` |
-| UI | `{Product}.UX` | `Journeys.UX` (HTTP to API only; not a csproj) |
+| Adapters | `{Product}.Adapters` | `Journeys.DAL` + `Journeys.Infra*` + `Journeys.Notification` + `Journeys.Infra.Llm` |
+| UI | `{Product}.UX` | `Journeys.UX` (HTTP to API only; not a csproj; may proxy Campaign Agent SSE) |
 
 Dto is required. Adapter path rules remain `dal-adapters.mdc` and `infra-adapters.mdc`; `adapters.mdc` is an alias.
