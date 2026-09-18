# Local ops

From `C:\Dev\Journeys\Journeys`:

```powershell
dotnet restore .\Journeys.sln
dotnet build .\Journeys.sln
```

Agent local gate (impact + build):

```powershell
.\scripts\agent-verify.ps1 -Files @("docs/platform/architecture.md") -SkipImpact
```

Use `-SkipImpact` only when you are not proving the bridge. Default is to run `docs-impact` and `graph-impact` (pass `-Files` when there is no git repo).

## Local secrets

Checked-in `appsettings*.json` files must not contain live keys. `Journeys.API` and `Journeys.Agent` already have `UserSecretsId` values. Set local credentials with user secrets or environment variables, for example:

```powershell
dotnet user-secrets set "CampaignAgent:AnthropicApiKey" "<your-key>" --project .\Journeys.API\Journeys.API.csproj
dotnet user-secrets set "ANTHROPIC_API_KEY" "<your-key>" --project .\Journeys.Agent\Journeys.Agent.csproj
dotnet user-secrets set "MassTransit:Transport:Host" "<service-bus-connection-string>" --project .\Journeys.API\Journeys.API.csproj
dotnet user-secrets set "MassTransit:SagaRepository:Key" "<cosmos-key>" --project .\Journeys.API\Journeys.API.csproj
dotnet user-secrets set "DataLake:ConnectionString" "<storage-connection-string>" --project .\Journeys.API\Journeys.API.csproj
dotnet user-secrets set "Serilog:WriteTo:0:Args:authenticationId" "<log-analytics-key>" --project .\Journeys.API\Journeys.API.csproj
```

Alternatively, put overrides in `appsettings.Local.json` (gitignored). `ServiceBusAdapter` takes its connection string from DI — do not hardcode keys in source.

Without a Log Analytics `authenticationId`, `Journeys.API` still starts and writes to the console. Azure Analytics is skipped until that secret is set. Without `DataLake:ConnectionString`, Azure blob clients and the chunk/archive background jobs are not registered. Unconfigured Data Lake and blob adapters are registered instead so `builder.Build()` can construct `LoyaltyAccountService` and related services. The host starts; ingest, file download, and account-report export fail when those code paths run. Account delete skips a missing report file. Campaign-agent tool-audit lines are dropped until storage is configured.

## Linear projection (optional)

The AI-DLC Linear board adapter reads `LINEAR_API_KEY` from the environment (not user secrets, not `appsettings`). See [linear-aidlc-projection.md](linear-aidlc-projection.md).

## Journeys.UX

See [journeys-ux.md](journeys-ux.md) for how to run the Loyalty admin Next app.

## Campaign Agent LLM

See [campaign-agent-llm.md](campaign-agent-llm.md).
