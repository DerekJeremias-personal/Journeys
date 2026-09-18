# Campaign Agent Ollama (`Journeys.Infra.Llm`) Implementation Plan

> **Execution:** After approval, say execute and the intent name. `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic`. Do **not** use superpowers:subagent-driven-development. Linear unit issues land before any `Journeys.*` code. Do not `--review none` or Express.

**Goal:** Add a Journeys-owned OpenAI-compatible LLM leaf and a startup `CampaignAgent:Provider` switch so Development Campaign Agent talks to MSI Ollama (`llama3.1:8b`) without an Anthropic key, with an 8B prompt/tool diet and connect retry.

**Architecture:** `Journeys.Infra.Llm` is the analog of `Backend.Llm.OpenAICompatible` (reimplement, do not HintPath that DLL). `Journeys.API` Program.cs branches DI like Backend `BackendAgent:Provider`. Anthropic path still uses `Backend.Llm.Anthropic`. Composer appends `SystemPrompt.Ollama.txt` only on the Ollama path. Connect-retry wraps the Ollama `IChatClient` only.

**Tech Stack:** net8, `Microsoft.Extensions.AI` 9.9.0, `Microsoft.Extensions.AI.OpenAI` 9.9.0-preview.1.25458.4, xUnit in `Journeys.Tests`. `ILlmChatClientFactory` / `PassthroughLlmPromptChatMapper` from `Backend.Core.dll`.

**Spec:** `docs/specs/2026-09-16-Journeys-campaign-agent-ollama-design.md`

## Global Constraints

- Do not invent capability ids. Existing: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- Do not HintPath or project-reference `Backend.Llm.OpenAICompatible`.
- Do not change `Journeys.Agent` AnthropicClient or add Campaign Agent UI in `Journeys.UX`.
- Do not change Auth0 `"hayward"`.
- Do not fork every CampaignAgent governance `.txt` for Ollama — only `SystemPrompt.Ollama.txt`.
- Do not add live Ollama tests. Do not hit `192.168.1.191` from CI.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.
- Never log tokens, API keys, connection strings, or full event payloads.
- `Provider` change requires process restart (select-at-startup only).
- File-scoped namespaces, nullable enable, constructor injection, readonly `_camelCase`.

## File map

| Path | Responsibility |
|------|----------------|
| `Journeys.API/CampaignAgent/CampaignAgentLlmProvider.cs` | Parse `CampaignAgent:Provider` / `CAMPAIGN_AGENT_PROVIDER` |
| `Journeys.Infra.Llm/*.cs` | OpenAI-compatible factory, Ollama JSON patch, connect retry |
| `Journeys.API/CampaignAgent/CampaignAgentLlmServiceCollectionExtensions.cs` | DI branch |
| `Journeys.API/Program.cs` | Call the extension instead of hard-wired Anthropic |
| `Journeys.API/CampaignAgent/SystemPrompt.Ollama.txt` | 8B addendum |
| `Journeys.API/CampaignAgent/CampaignAgentPromptComposer.cs` | Append addendum when provider is OpenAI-compatible |
| `Journeys.API/appsettings.Development.json` | MSI Ollama + diet flags |
| `docs/developer/campaign-agent-llm.md` | How to run |
| `docs/product/graph/path-map.yaml` | `Journeys.Infra.Llm` → `campaign-agent` |

---

### Task 1: `CampaignAgentLlmProvider` (TDD)

**Files:**
- Create: `Journeys.API/CampaignAgent/CampaignAgentLlmProvider.cs`
- Test: `Journeys.Tests/CampaignAgent/CampaignAgentLlmProviderTests.cs`

**Interfaces:**
- Consumes: `IConfiguration` keys `CampaignAgent:Provider` and `CAMPAIGN_AGENT_PROVIDER`
- Produces: `enum CampaignAgentLlmProviderKind { Anthropic, OpenAICompatible }` and `CampaignAgentLlmProvider.Parse(string? value)`, `CampaignAgentLlmProvider.Resolve(IConfiguration configuration)`

- [ ] **Step 1: Write failing tests** `Journeys.Tests/CampaignAgent/CampaignAgentLlmProviderTests.cs`

```csharp
using Journeys.API.CampaignAgent;
using Microsoft.Extensions.Configuration;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentLlmProviderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Anthropic")]
    [InlineData("anthropic")]
    public void Parse_defaults_or_anthropic(string? value)
    {
        Assert.Equal(CampaignAgentLlmProviderKind.Anthropic, CampaignAgentLlmProvider.Parse(value));
    }

    [Theory]
    [InlineData("OpenAICompatible")]
    [InlineData("openaiCompatible")]
    [InlineData("Ollama")]
    [InlineData("ollama")]
    public void Parse_openai_compatible_aliases(string value)
    {
        Assert.Equal(CampaignAgentLlmProviderKind.OpenAICompatible, CampaignAgentLlmProvider.Parse(value));
    }

    [Fact]
    public void Parse_unknown_throws_campaign_agent_message()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CampaignAgentLlmProvider.Parse("AzureOpenAI"));
        Assert.Contains("Campaign Agent", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Backend Agent", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Anthropic", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OpenAICompatible", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ollama", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_uses_section_then_env_key_when_section_blank()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CAMPAIGN_AGENT_PROVIDER"] = "Ollama"
            })
            .Build();
        Assert.Equal(CampaignAgentLlmProviderKind.OpenAICompatible, CampaignAgentLlmProvider.Resolve(config));
    }

    [Fact]
    public void Resolve_section_wins_when_set()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CampaignAgent:Provider"] = "Anthropic",
                ["CAMPAIGN_AGENT_PROVIDER"] = "Ollama"
            })
            .Build();
        Assert.Equal(CampaignAgentLlmProviderKind.Anthropic, CampaignAgentLlmProvider.Resolve(config));
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL** (type not found)

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~CampaignAgentLlmProviderTests
```

- [ ] **Step 3: Implement** `Journeys.API/CampaignAgent/CampaignAgentLlmProvider.cs`

```csharp
using Microsoft.Extensions.Configuration;

namespace Journeys.API.CampaignAgent;

public enum CampaignAgentLlmProviderKind
{
    Anthropic,
    OpenAICompatible
}

public static class CampaignAgentLlmProvider
{
    public static CampaignAgentLlmProviderKind Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration["CampaignAgent:Provider"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = configuration["CAMPAIGN_AGENT_PROVIDER"];
        return Parse(raw);
    }

    public static CampaignAgentLlmProviderKind Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CampaignAgentLlmProviderKind.Anthropic;

        return value.Trim() switch
        {
            var v when v.Equals("Anthropic", StringComparison.OrdinalIgnoreCase)
                => CampaignAgentLlmProviderKind.Anthropic,
            var v when v.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase)
                => CampaignAgentLlmProviderKind.OpenAICompatible,
            var v when v.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
                => CampaignAgentLlmProviderKind.OpenAICompatible,
            _ => throw new InvalidOperationException(
                "Unknown Campaign Agent LLM provider. Allowed values: Anthropic, OpenAICompatible, Ollama.")
        };
    }
}
```

- [ ] **Step 4: Re-run filter — expect PASS**

- [ ] **Step 5: Do not commit** unless the user asks in that message.

---

### Task 2: `Journeys.Infra.Llm` runtime options + Ollama HTTP patch (TDD)

**Files:**
- Create: `Journeys.Infra.Llm/Journeys.Infra.Llm.csproj`
- Create: `Journeys.Infra.Llm/OpenAICompatibleRuntimeOptions.cs`
- Create: `Journeys.Infra.Llm/OllamaChatCompletionsOptionsHandler.cs`
- Create: `Journeys.Infra.Llm/OpenAICompatibleRequestOptionsApplier.cs`
- Create: `Journeys.Infra.Llm/OpenAICompatibleRequestOptionsChatClient.cs`
- Modify: `Journeys.sln` (add project; nest under solution folder `Journeys.Infra`)
- Modify: `Journeys.Tests/Journeys.Tests.csproj` (ProjectReference `Journeys.Infra.Llm`)
- Test: `Journeys.Tests/Infra.Llm/OpenAICompatibleRuntimeOptionsTests.cs`
- Test: `Journeys.Tests/Infra.Llm/OllamaChatCompletionsOptionsHandlerTests.cs`
- Test: `Journeys.Tests/Infra.Llm/OpenAICompatibleRequestOptionsApplierTests.cs`

**Interfaces:**
- Consumes: `IConfiguration` section `CampaignAgent` plus env `CAMPAIGN_AGENT_OPENAI_*`
- Produces: `OpenAICompatibleRuntimeOptions.Parse(IConfiguration, string section)` record `(int? NumCtx, string? ReasoningEffort, TimeSpan? RequestTimeout, int ConnectRetrySeconds)`

Namespace: `Journeys.Infra.Llm`. HintPath `..\..\Binaries\Backend.Core.dll` on the csproj (interface assembly only).

- [ ] **Step 1: Create csproj** `Journeys.Infra.Llm/Journeys.Infra.Llm.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Journeys.Infra.Llm</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.AI" Version="9.9.0" />
    <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.9.0-preview.1.25458.4" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="Backend.Core">
      <HintPath>..\..\Binaries\Backend.Core.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: `dotnet sln add`**

```powershell
cd C:\Dev\Journeys\Journeys
dotnet sln .\Journeys.sln add .\Journeys.Infra.Llm\Journeys.Infra.Llm.csproj --solution-folder Journeys.Infra
```

Add `<ProjectReference Include="..\Journeys.Infra.Llm\Journeys.Infra.Llm.csproj" />` to `Journeys.Tests.csproj`.

- [ ] **Step 3: Write failing `OpenAICompatibleRuntimeOptionsTests`**

Cover: missing NumCtx → null; `16384` → 16384; `100` throws range; `ReasoningEffort` `off`/`low`; `bogus` throws; `RequestTimeoutSeconds` omitted → null; `0` → `Timeout.InfiniteTimeSpan`; `1800` → 1800s; `ConnectRetrySeconds` omitted → 0; `60` → 60; env `CAMPAIGN_AGENT_OPENAI_MODEL` is **not** in this type (model is factory). Use in-memory config with section `CampaignAgent`.

- [ ] **Step 4: Implement `OpenAICompatibleRuntimeOptions.cs`** — copy logic from `C:\Dev\Backend\Backend.Llm.OpenAICompatible\OpenAICompatibleRuntimeOptions.cs` into namespace `Journeys.Infra.Llm`, but:
  - Add `int ConnectRetrySeconds` (default 0). Parse `CampaignAgent:OpenAICompatible:ConnectRetrySeconds` then `CAMPAIGN_AGENT_OPENAI_CONNECT_RETRY_SECONDS`. Invalid (negative or > 600) throws. 0 means no retry.
  - Env fallbacks for NumCtx / ReasoningEffort / RequestTimeoutSeconds use `CAMPAIGN_AGENT_OPENAI_NUM_CTX`, `CAMPAIGN_AGENT_OPENAI_REASONING_EFFORT`, `CAMPAIGN_AGENT_OPENAI_REQUEST_TIMEOUT_SECONDS` (not `BACKEND_AGENT_*`).
  - Keep `MinNumCtx = 2048`, `MaxNumCtx = 128000`, timeout 0 or 30–7200, `DefaultNetworkTimeout = 15 minutes`.

- [ ] **Step 5: Implement handler + applier + request options chat client** — port `OllamaChatCompletionsOptionsHandler.cs`, `OpenAICompatibleRequestOptionsApplier.cs`, `OpenAICompatibleRequestOptionsChatClient.cs` from Backend with namespace `Journeys.Infra.Llm` only (behavior unchanged).

- [ ] **Step 6: Handler test** — `HttpRequestMessage` POST `http://x/v1/chat/completions` with body `{"model":"m"}` and runtime `NumCtx: 16384`, `ReasoningEffort: "off"`; after `SendAsync` through handler wrapping `StubHandler` that captures the body, assert `options.num_ctx` is 16384 and `think` is false.

- [ ] **Step 7: Applier test** — `Apply(null, runtime with NumCtx 16384)` sets `AdditionalProperties["num_ctx"]` to 16384 without mutating a passed-in `ChatOptions` instance (clone).

- [ ] **Step 8: `dotnet test` filter `FullyQualifiedName~OpenAICompatible` — PASS**

- [ ] **Step 9: Do not commit** unless the user asks in that message.

---

### Task 3: `OpenAICompatibleLlmChatClientFactory` + connect retry (TDD)

**Files:**
- Create: `Journeys.Infra.Llm/OpenAICompatibleLlmChatClientFactory.cs`
- Create: `Journeys.Infra.Llm/OpenAICompatibleConnectRetryChatClient.cs`
- Test: `Journeys.Tests/Infra.Llm/OpenAICompatibleLlmChatClientFactoryTests.cs`
- Test: `Journeys.Tests/Infra.Llm/OpenAICompatibleConnectRetryChatClientTests.cs`

**Interfaces:**
- Consumes: `ILlmChatClientFactory` (`Backend.Core.Llm`), `OpenAICompatibleRuntimeOptions`
- Produces: `OpenAICompatibleLlmChatClientFactory(IConfiguration configuration, string configurationSection, ILogger<OpenAICompatibleLlmChatClientFactory>? logger = null)` with `CreateChatClient(): IChatClient`. When `ConnectRetrySeconds > 0`, the returned client is wrapped in `OpenAICompatibleConnectRetryChatClient`.

- [ ] **Step 1: Factory tests** (in-memory config, **do not** call Ollama)

Missing `CampaignAgent:OpenAICompatible:Model` and no `CAMPAIGN_AGENT_OPENAI_MODEL` → `CreateChatClient` throws `InvalidOperationException` containing `OpenAI-compatible model is missing`.

Blank BaseUrl → resolved default `http://localhost:11434/v1` (assert via a small internal or `internal` test hook: add `internal static` `ResolveEndpoint(IConfiguration, string section)` returning `(string BaseUrl, string ApiKey, string Model)` used by `CreateChatClient`, `InternalsVisibleTo` Journeys.Tests).

Blank ApiKey → `ollama`.

Model from env `CAMPAIGN_AGENT_OPENAI_MODEL` when config model blank.

- [ ] **Step 2: Implement factory** — port `OpenAICompatibleLlmChatClientFactory.cs` from Backend into `Journeys.Infra.Llm`:
  - Env keys: `CAMPAIGN_AGENT_OPENAI_BASE_URL`, `CAMPAIGN_AGENT_OPENAI_API_KEY`, `CAMPAIGN_AGENT_OPENAI_MODEL`
  - Defaults: BaseUrl `http://localhost:11434/v1`, ApiKey `ollama`
  - `CreateChatClient` builds `OpenAIClient` + `AsIChatClient()` + `OpenAICompatibleRequestOptionsChatClient` + `UseFunctionInvocation()` as in Backend
  - If `runtime.ConnectRetrySeconds > 0`, wrap with `new OpenAICompatibleConnectRetryChatClient(inner, baseUrl, TimeSpan.FromSeconds(runtime.ConnectRetrySeconds), TimeSpan.FromSeconds(2), logger)`
  - Do not log ApiKey

- [ ] **Step 3: Connect-retry tests** using a fake `IChatClient`:
  - First `GetResponseAsync` throws `HttpRequestException` with `HttpRequestError.ConnectionError` (or `SocketException`); second succeeds → one success, two inner calls. `ConnectRetrySeconds` 5, delay 0 in tests via constructor `TimeSpan retryDelay` (use `TimeSpan.Zero` in tests).
  - `HttpRequestException` with message containing `401` or `StatusCode` 401 → **no** retry (one call).
  - After `GetStreamingResponseAsync` yields one update, a later throw is **not** retried (mid-stream).

- [ ] **Step 4: Implement `OpenAICompatibleConnectRetryChatClient`**

Retry `GetResponseAsync` and the **first** `GetStreamingResponseAsync` enumeration only while `Stopwatch` elapsed `< retryWindow` and `cancellationToken` not cancelled, on `IsUnreachable(Exception)`: `HttpRequestException` without HTTP status 4xx, `SocketException`, `IOException` with "refused"/"reset"/"unreachable" in message (ordinal ignore case). Wait `retryDelay` (2s in production). After window: throw `InvalidOperationException` `$"Ollama unreachable at {baseUrl} after retry window."` wrapping last exception. Never include ApiKey.

- [ ] **Step 5: Tests PASS**

- [ ] **Step 6: Do not commit** unless the user asks in that message.

---

### Task 4: DI branch in `Journeys.API`

**Files:**
- Create: `Journeys.API/CampaignAgent/CampaignAgentLlmServiceCollectionExtensions.cs`
- Modify: `Journeys.API/Journeys.API.csproj` (ProjectReference `Journeys.Infra.Llm`)
- Modify: `Journeys.API/Program.cs` (replace hard-wired Anthropic singleton block)

**Interfaces:**
- Consumes: `CampaignAgentLlmProvider.Resolve`, `OpenAICompatibleLlmChatClientFactory`, existing `AnthropicLlmChatClientFactory`
- Produces: `IServiceCollection AddCampaignAgentLlm(this IServiceCollection services, IConfiguration configuration)`

- [ ] **Step 1: Implement extension**

```csharp
using Backend.Core.Llm;
using Backend.Llm.Anthropic;
using Journeys.Infra.Llm;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

public static class CampaignAgentLlmServiceCollectionExtensions
{
    public static IServiceCollection AddCampaignAgentLlm(this IServiceCollection services, IConfiguration configuration)
    {
        var kind = CampaignAgentLlmProvider.Resolve(configuration);
        if (kind == CampaignAgentLlmProviderKind.Anthropic)
        {
            services.AddSingleton(sp =>
                new AnthropicLlmChatClientFactory(
                    sp.GetRequiredService<IConfiguration>(),
                    "CampaignAgent",
                    sp.GetService<ILogger<AnthropicLlmChatClientFactory>>()));
            services.AddSingleton<ILlmChatClientFactory>(sp =>
                sp.GetRequiredService<AnthropicLlmChatClientFactory>());
            services.AddSingleton<ILlmPromptChatMapper, AnthropicLlmPromptChatMapper>();
        }
        else
        {
            services.AddSingleton(sp =>
                new OpenAICompatibleLlmChatClientFactory(
                    sp.GetRequiredService<IConfiguration>(),
                    "CampaignAgent",
                    sp.GetService<ILogger<OpenAICompatibleLlmChatClientFactory>>()));
            services.AddSingleton<ILlmChatClientFactory>(sp =>
                sp.GetRequiredService<OpenAICompatibleLlmChatClientFactory>());
            services.AddSingleton<ILlmPromptChatMapper, PassthroughLlmPromptChatMapper>();
        }

        return services;
    }
}
```

- [ ] **Step 2: In `Program.cs`**, replace the three `AddSingleton` lines for `AnthropicLlmChatClientFactory` / `ILlmChatClientFactory` / `ILlmPromptChatMapper` with `builder.Services.AddCampaignAgentLlm(builder.Configuration);` Keep `AddScoped<ICampaignAgentOrchestrator, CampaignAgentOrchestrator>();` immediately after.

- [ ] **Step 3: `dotnet build .\Journeys.API\Journeys.API.csproj`** — expect exit 0 (or file-lock only if API is running; then build with `-o $env:TEMP\journeys-llm-verify`).

- [ ] **Step 4: Do not commit** unless the user asks in that message.

---

### Task 5: Ollama addendum + Development diet

**Files:**
- Create: `Journeys.API/CampaignAgent/SystemPrompt.Ollama.txt`
- Modify: `Journeys.API/Journeys.API.csproj` (Content Include like `SystemPrompt.txt`)
- Modify: `Journeys.API/CampaignAgent/CampaignAgentPromptComposer.cs` (inject `IConfiguration`; append Ollama file to persona when `Resolve` is OpenAICompatible)
- Modify: `Journeys.Tests/CampaignAgent/CampaignAgentPromptComposerTests.cs` (`CreateComposer` takes `IConfiguration`; new fact)
- Modify: `Journeys.API/appsettings.Development.json` (`CampaignAgent` keys from spec §8)

**Interfaces:**
- Consumes: `CampaignAgentLlmProvider.Resolve`
- Produces: persona segment includes Ollama addendum text only on OpenAI-compatible provider

- [ ] **Step 1: Create `SystemPrompt.Ollama.txt` exact body**

```
OLLAMA / SMALL MODEL:
- One tool call per turn. Prefer digest tools over raw payloads.
- Do not paste full JSON catalogs or event payloads into the reply.
- If the campaign brief is not captured yet, start with a read (list_campaigns or model list).
```

Copy the `Content Include` item for `SystemPrompt.txt` in the csproj and add `CampaignAgent\SystemPrompt.Ollama.txt`.

- [ ] **Step 2: Composer** — add `IConfiguration _configuration` constructor parameter. After `var persona = await GetCachedFileContentAsync(PersonaFileName, ...)`, if `CampaignAgentLlmProvider.Resolve(_configuration) == CampaignAgentLlmProviderKind.OpenAICompatible`, load `SystemPrompt.Ollama.txt` with cache segment `personaOllama` and append `"\n\n" + ollama.Trim()` to `persona`.

- [ ] **Step 3: Update `CreateComposer`** to pass `new ConfigurationBuilder().Build()` (Anthropic). Add test `BuildAsync_AppendsOllamaAddendum_WhenProviderOllama` with in-memory `CampaignAgent:Provider` = `Ollama` and `Assert.Contains("OLLAMA / SMALL MODEL", ctx.Instructions)`. Existing composer tests must still pass (no addendum).

- [ ] **Step 4: Development.json** — inside existing `"CampaignAgent": { ... }` add/override (do not remove `McpEndpointUrl` or other keys):

```json
"Provider": "Ollama",
"ExposeFullBackendMcpToolSurface": false,
"AllowMultipleToolCallsPerModelResponse": false,
"MaxOutputTokens": 2048,
"HistoryMaxChars": 40000,
"HistoryMaxUserTurns": 12,
"MaxPersistedToolResultChars": 4000,
"OpenAICompatible": {
  "BaseUrl": "http://192.168.1.191:11434/v1",
  "ApiKey": "ollama",
  "Model": "llama3.1:8b",
  "NumCtx": 16384,
  "ReasoningEffort": "off",
  "RequestTimeoutSeconds": 1800,
  "ConnectRetrySeconds": 60
}
```

Set `ExposeFullBackendMcpToolSurface` to **false** (it is `true` today).

- [ ] **Step 5: Test that committed Development JSON has the diet** — `Journeys.Tests/CampaignAgent/CampaignAgentDevelopmentJsonTests.cs` reads `Journeys.API/appsettings.Development.json` via `Path.Combine` from the test assembly or content root, `JsonDocument`, assert `CampaignAgent.Provider` is `Ollama`, `ExposeFullBackendMcpToolSurface` is false, `OpenAICompatible.Model` is `llama3.1:8b`, `ConnectRetrySeconds` is 60.

- [ ] **Step 6: `dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~CampaignAgent`** — PASS

- [ ] **Step 7: Do not commit** unless the user asks in that message.

---

### Task 6: Docs, graph, overlays

**Files:**
- Create: `docs/developer/campaign-agent-llm.md`
- Modify: `docs/developer/index.md`
- Modify: `docs/developer/local-ops.md`
- Modify: `docs/platform/overlays.md`
- Modify: `docs/platform/architecture.md`
- Modify: `docs/product/graph/path-map.yaml`
- Modify: `scripts/path-docs-map.yaml`

- [ ] **Step 1: Create `docs/developer/campaign-agent-llm.md`**

```markdown
# Campaign Agent LLM providers

`Journeys.API` Campaign Agent selects the LLM at **startup** from `CampaignAgent:Provider` (or `CAMPAIGN_AGENT_PROVIDER`). Restart the API to change provider. There is no hot reload.

**Spec:** `docs/specs/2026-09-16-Journeys-campaign-agent-ollama-design.md`

## Values

| Config | Client |
|--------|--------|
| unset / `Anthropic` | `Backend.Llm.Anthropic` (`AnthropicLlmChatClientFactory`). Requires `CampaignAgent:AnthropicApiKey` / `ANTHROPIC_API_KEY`. |
| `Ollama` or `OpenAICompatible` | `Journeys.Infra.Llm` (`OpenAICompatibleLlmChatClientFactory`). No Anthropic key. |

Do not HintPath `Backend.Llm.OpenAICompatible`. `Journeys.Agent` is not switched in this spec.

## Development (MSI Ollama)

`appsettings.Development.json` points at `http://192.168.1.191:11434/v1`, model `llama3.1:8b`. Leave the Ollama tray running on the MSI. Connect retry: 60s, 2s apart.

## Live smoke

Anonymous-dev Campaign Agent SSE is already allowed in Development. Pass: assistant text and at least one successful MCP tool (`list_campaigns` or digest). Not required: Draft upsert or Live publish.
```

- [ ] **Step 2: Index bullet** `- [Campaign Agent LLM](campaign-agent-llm.md) — Anthropic vs Ollama`

- [ ] **Step 3: `local-ops.md` append `## Campaign Agent LLM` pointing at `campaign-agent-llm.md`**

- [ ] **Step 4: Overlays adapters cell** — add `+ \`Journeys.Infra.Llm\`` to the Adapters this-product cell (HTTP to Ollama `/v1`; not a Backend LLM DLL).

- [ ] **Step 5: Architecture** — Adapters “Must not” row unchanged. After the C# arrow sentence, add: `Journeys.Infra.Llm` is an HTTP client to OpenAI-compatible endpoints (Ollama) for Campaign Agent; it is not `Backend.Llm.OpenAICompatible`. Backend DLLs sentence still lists only `Backend.Llm.Anthropic.dll` for LLM.

- [ ] **Step 6: `path-map.yaml` top of entries:**

```yaml
  - prefix: Journeys.Infra.Llm
    nodes: [campaign-agent]
    meaningOptional: false
```

`scripts/path-docs-map.yaml` top:

```yaml
  - prefix: Journeys.Infra.Llm
    docs:
      - docs/developer/campaign-agent-llm.md
      - docs/platform/architecture.md
      - docs/platform/overlays.md
```

- [ ] **Step 7: Confirm no new capability ids** — `Select-String` on `docs/product/graph/nodes.yaml` for `id:` — seven capabilities unchanged; no `Backend.Llm.OpenAICompatible` node.

- [ ] **Step 8: Do not commit** unless the user asks in that message.

---

### Task 7: Verify + human smoke

**Files:** none new.

- [ ] **Step 1:** From `C:\Dev\Journeys\Journeys`

```powershell
$files = @(
  "Journeys.Infra.Llm/Journeys.Infra.Llm.csproj",
  "Journeys.API/Program.cs",
  "docs/developer/campaign-agent-llm.md",
  "docs/platform/architecture.md",
  "docs/platform/overlays.md",
  "docs/product/graph/path-map.yaml",
  "scripts/path-docs-map.yaml"
)
.\scripts\docs-impact.ps1 -Files $files
.\scripts\graph-impact.ps1 -Files $files
```

Expected: both exit 0.

- [ ] **Step 2:** `dotnet test .\Journeys.Tests\Journeys.Tests.csproj` — all PASS (or document file-lock / skip only if environment-blocked).

- [ ] **Step 3:** `dotnet build .\Journeys.sln` — 0 CS errors (use `-o $env:TEMP\journeys-llm-verify` if API has DLLs locked).

- [ ] **Step 4: Manual (human):** MSI Ollama up, API Development restarted, Campaign Agent SSE, one successful tool call.

- [ ] **Step 5: Do not commit** unless the user asks in that message.

---

## Spec coverage (self-review)

| Spec | Task |
|------|------|
| G1 Provider parse | 1, 4 |
| G2 Journeys.Infra.Llm analog | 2, 3 |
| G3 No Anthropic factory on Ollama | 4 |
| G4 MSI Dev block | 5 |
| G5 Diet + addendum | 5 |
| G6 Live smoke | 7 (human) |
| Connect retry | 3 |
| Tests no live Ollama | 1–3, 5, 7 |
| Docs/graph | 6 |
| Non-goals Agent/UX/Backend OpenAICompatible DLL | none implemented |
