# Task 4 — DI branch in Journeys.API

## Done

- Created `Journeys.API/CampaignAgent/CampaignAgentLlmServiceCollectionExtensions.cs` with `AddCampaignAgentLlm` branching on `CampaignAgentLlmProvider.Resolve`: Anthropic → `AnthropicLlmChatClientFactory` + `AnthropicLlmPromptChatMapper`; OpenAI-compatible → `OpenAICompatibleLlmChatClientFactory` + `PassthroughLlmPromptChatMapper`.
- Added `<ProjectReference Include="..\Journeys.Infra.Llm\Journeys.Infra.Llm.csproj" />` to `Journeys.API.csproj`.
- Replaced three hard-wired Anthropic `AddSingleton` lines in `Program.cs` with `builder.Services.AddCampaignAgentLlm(builder.Configuration);`; kept `AddScoped<ICampaignAgentOrchestrator, CampaignAgentOrchestrator>()` immediately after.
- Removed unused `Backend.Core.Llm` and `Backend.Llm.Anthropic` usings from `Program.cs`.

## Build

Shell blocked in subagent session. Controller should run:

```powershell
dotnet build .\Journeys.API\Journeys.API.csproj -o $env:TEMP\journeys-llm-t4 --verbosity minimal
```

## Not changed

Journeys.Agent, Journeys.UX, Backend.Llm.OpenAICompatible HintPath.
