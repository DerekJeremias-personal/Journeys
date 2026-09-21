# Testing

Automated tests live in `Journeys.Tests`. Follow existing folders (RulesEngine, Services, Workflow).

Tree-wide hydrate collect is covered by `Services/HydrateCollectTests.cs` (filter `FullyQualifiedName~HydrateCollect`). Existing root historical TTL cases stay in `Services/RuleServiceTests.cs` and are not the collect oracle.

Earn-date dest clock is covered by `Services/LoyaltyAccountExpirationCascadeTests.cs` (filter `FullyQualifiedName~LoyaltyAccountExpirationCascade`). Existing `ExpirePointsOutcomeTests` stay historical and are not the cascade oracle.

Expire-on-process (lock → bring-current → rules) is covered by `Services/ProcessCampaignsExpireOnProcessTests.cs` (filter `FullyQualifiedName~ProcessCampaignsExpireOnProcess`). Existing `UserPointsTests` stay historical GET/reconcile expire cases and are not that oracle.

NotificationOutcome Calculate/Award is covered by `RulesEngine/Outcomes/NotificationOutcomeTests.cs` (filter `FullyQualifiedName~NotificationOutcomeTests`). MCP `NotificationConfigId` row is `Mcp/RulesEngineMcpContractSummaryTests.cs` (`FullyQualifiedName~RulesEngineMcpContractSummaryTests`); do not treat `2026-06-20` pin fixtures as that oracle.

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj
```

`agent-verify.ps1` runs tests when a changed file is under `Journeys.Tests\` or `-RunTests` is set.

`Configuration/ConfigureInfraTests.cs` asserts the API still registers Data Lake and blob adapters when `DataLake:ConnectionString` is missing, so `builder.Build()` does not fail.

`Infra/BackendModelIdTests.cs` asserts entity/catalog JSON never emits `modelId` `"unknown"` and that catalog list bodies send `modelType: loyalty` without a model id.

`Infra.Llm/OpenAICompatibleConnectRetryChatClientTests.cs` covers connect-error retry (including streaming before the first update) and no retry for HTTP 4xx or InvalidResponse/5xx. `OpenAICompatibleLlmChatClientFactoryTests.cs` stays offline.

Wrapper persist tests in `Utility/WrappedEventPayloadPersistTests.cs` assert canonical lowercase JSON symbols, nested engine-state names (`nodememberships`), and that empty list properties are omitted.
