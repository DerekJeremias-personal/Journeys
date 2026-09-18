# Task 5 — Ollama addendum + Development diet

## Done

- Created `Journeys.API/CampaignAgent/SystemPrompt.Ollama.txt` with the exact four-line addendum body.
- Copied `Content Include` for `CampaignAgent\SystemPrompt.Ollama.txt` in `Journeys.API.csproj` (same CopyToOutput/Publish as `SystemPrompt.txt`).
- `CampaignAgentPromptComposer` now takes readonly `IConfiguration _configuration`. After loading `SystemPrompt.txt`, if `CampaignAgentLlmProvider.Resolve` is `OpenAICompatible`, loads `SystemPrompt.Ollama.txt` (`personaOllama`) and appends `"\n\n" + ollama.Trim()` to persona. No other governance files forked.
- Tests: `CreateComposer` passes `IConfiguration` (default empty builder = Anthropic). New `BuildAsync_AppendsOllamaAddendum_WhenProviderOllama` plus optional DoesNotContain counterpart. `ApiContentRoot` walks ancestors from BaseDirectory and CurrentDirectory for `CampaignAgent/SystemPrompt.txt`.
- `appsettings.Development.json` `CampaignAgent`: `Provider` Ollama, `ExposeFullBackendMcpToolSurface` false, diet keys, `OpenAICompatible` (Model `llama3.1:8b`, ConnectRetrySeconds 60, BaseUrl MSI). Existing McpEndpointUrl and other keys kept.
- Added `Journeys.Tests/CampaignAgent/CampaignAgentDevelopmentJsonTests.cs` (same ancestor walker for `Journeys.API/appsettings.Development.json`).

## Tests

Shell blocked `dotnet` in this subagent session. Controller should run:

```powershell
$out = Join-Path $env:TEMP "journeys-ollama-t5"
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~CampaignAgent -o $out --verbosity minimal
```

## Not changed

Journeys.Agent, Journeys.UX. No git commit. Docs/graph are Task 6.
