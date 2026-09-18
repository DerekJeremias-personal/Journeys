# Task 2 Report: `Journeys.Infra.Llm` runtime options + Ollama HTTP patch (TDD)

**Date:** 2026-09-16  
**Plan:** `docs/plans/2026-09-16-Journeys-campaign-agent-ollama.md` — Task 2 only  
**Status:** Complete (files written; shell blocked in subagent — controller must sln-add if needed and run tests)

## Files created

| File | Action |
|------|--------|
| `Journeys.Infra.Llm/Journeys.Infra.Llm.csproj` | Created — verbatim from plan |
| `Journeys.Infra.Llm/OpenAICompatibleRuntimeOptions.cs` | Created — Backend port + ConnectRetrySeconds + `CAMPAIGN_AGENT_OPENAI_*` |
| `Journeys.Infra.Llm/OllamaChatCompletionsOptionsHandler.cs` | Created — Backend port, namespace only |
| `Journeys.Infra.Llm/OpenAICompatibleRequestOptionsApplier.cs` | Created — Backend port, namespace only |
| `Journeys.Infra.Llm/OpenAICompatibleRequestOptionsChatClient.cs` | Created — Backend port, namespace only |
| `Journeys.Tests/Infra.Llm/OpenAICompatibleRuntimeOptionsTests.cs` | Created |
| `Journeys.Tests/Infra.Llm/OpenAICompatibleRequestOptionsApplierTests.cs` | Created |
| `Journeys.Tests/Infra.Llm/OllamaChatCompletionsOptionsHandlerTests.cs` | Created |

## Files modified

| File | Action |
|------|--------|
| `Journeys.sln` | Project `{B2E91F40-7C3A-4A8D-9E16-5F1C8A0D3B27}` nested under `Journeys.Infra` `{310A580B-7130-4D07-B2BF-7C6CDBC1B24B}` |
| `Journeys.Tests/Journeys.Tests.csproj` | ProjectReference `..\Journeys.Infra.Llm\Journeys.Infra.Llm.csproj` |

## Implementation notes

- Record: `(int? NumCtx, string? ReasoningEffort, TimeSpan? RequestTimeout = null, int ConnectRetrySeconds = 0)`
- `ConnectRetrySeconds` from `CampaignAgent:OpenAICompatible:ConnectRetrySeconds` then `CAMPAIGN_AGENT_OPENAI_CONNECT_RETRY_SECONDS`. Omitted → 0. Negative or > 600 throws.
- Env fallbacks: `CAMPAIGN_AGENT_OPENAI_NUM_CTX`, `CAMPAIGN_AGENT_OPENAI_REASONING_EFFORT`, `CAMPAIGN_AGENT_OPENAI_REQUEST_TIMEOUT_SECONDS` (not `BACKEND_AGENT_*`).
- No `Model` on this type. No HintPath to `Backend.Llm.OpenAICompatible`. HintPath `..\..\Binaries\Backend.Core.dll` only.
- Handler test uses `InvokingHandler` + `CaptureHandler`; class named `OpenAICompatibleOllamaChatCompletionsOptionsHandlerTests` so filter `FullyQualifiedName~OpenAICompatible` includes it.

## Tests (expected)

Filter: `FullyQualifiedName~OpenAICompatible`  
Count: **24** (18 options + 5 applier + 1 handler)

```powershell
cd C:\Dev\Journeys\Journeys
$out = Join-Path $env:TEMP "journeys-ollama-t2"
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~OpenAICompatible -o $out --verbosity minimal
```

**Subagent:** Shell was blocked by preToolUse hook. Tests were not executed here. Controller should run the command above. If `dotnet sln add` is still desired, it should be a no-op now that the project is nested by hand.

## Constraints honored

- [x] Namespace `Journeys.Infra.Llm`; no HintPath `Backend.Llm.OpenAICompatible`
- [x] No Journeys.Agent / Journeys.UX changes
- [x] No factory / connect-retry (Task 3)
- [x] File-scoped namespaces, nullable enable
- [x] Did not hit 192.168.1.191; no live Ollama tests
- [x] No git commit

## Concerns

1. **Tests not run in this session** (shell blocked). Controller must confirm PASS with isolated `-o`.
2. **Docs/graph** not updated (Task 6). Infra-only; `docs-impact` / `graph-impact` not run (shell blocked).
3. `Backend.Core` and `Microsoft.Extensions.AI.OpenAI` are on the csproj per plan but unused until Task 3.
4. `dotnet sln add` was not run; sln was nested by hand. Re-running `dotnet sln add` should be safe if the controller prefers the CLI path.
