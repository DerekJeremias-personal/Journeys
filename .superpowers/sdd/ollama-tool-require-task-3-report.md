# Task 3 Report: Orchestrator wire + docs + verify

**Date:** 2026-09-17  
**Plan:** Campaign Agent Ollama tool-require — Task 3 only  
**Status:** DONE (no commit; human owns git)

## What I Implemented

### Step 1 — Orchestrator wire

After constructing `chatTemplate` in `CampaignAgentOrchestrator` (the existing `new ChatOptions` block), call `OllamaToolRequire.Apply` with the resolved provider and current workflow phase:

```csharp
                var chatTemplate = new ChatOptions
                {
                    Tools = aiTools,
                    MaxOutputTokens = maxOut,
                    AllowMultipleToolCalls = allowMultiTool
                };
                OllamaToolRequire.Apply(
                    chatTemplate,
                    CampaignAgentLlmProvider.Resolve(_configuration),
                    workflowState.Phase);
```

`PassthroughLlmPromptChatMapper.Map` clones `templateOptions`, so `ToolMode` set on `chatTemplate` flows into `mappedPrompt.ChatOptions` used by the existing `GetStreamingResponseAsync` hop. No second streaming hop. Anthropic DI branch in `CampaignAgentLlmServiceCollectionExtensions` is unchanged.

### Step 2 — Developer docs

`docs/developer/campaign-agent-llm.md`: kept the existing provider table. Merged the Specs line (was `**Spec:**` ollama-design only). Replaced **Live smoke** with the G3/G4 operator steps from the brief (successful MCP tool, pre-brief Review `SaveModel`, model-tag fallback + restart). File is untracked in git (created in prior Ollama work, never committed).

### Step 3 — Graph waiver

Created `docs/product/graph/waivers/2026-09-17-ollama-tool-require.md` with the brief text (no new capability/edge; `campaign-agent`, `campaigns` path-map nodes).

## What I Tested and Results

### Command 1 — filtered tests

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~OllamaToolRequireTests|FullyQualifiedName~CampaignWorkflowToolFilterTests|FullyQualifiedName~CampaignWorkflowToolFilterSelectionTests|FullyQualifiedName~CampaignAgentLlmProviderTests" -o $env:TEMP\journeys-test-out
```

**Actual:** Passed 35/35. Failed: 0.

### Command 2 — agent-verify

```powershell
.\scripts\agent-verify.ps1 -Files @(
  "Journeys.API\CampaignAgent\OllamaToolRequire.cs",
  "Journeys.API\CampaignAgent\CampaignAgentOrchestrator.cs",
  "Journeys.API\CampaignAgent\Workflow\CampaignWorkflowToolFilter.cs",
  "Journeys.Tests\CampaignAgent\OllamaToolRequireTests.cs",
  "Journeys.Tests\CampaignAgent\CampaignWorkflowToolFilterTests.cs",
  "Journeys.Tests\Workflow\CampaignWorkflowToolFilterSelectionTests.cs",
  "docs\developer\campaign-agent-llm.md",
  "docs\developer\tools.md",
  "docs\developer\testing.md",
  "docs\platform\architecture.md",
  "docs\product\ontology\draft-live.md",
  "docs\product\graph\waivers\2026-09-17-ollama-tool-require.md"
)
```

**Actual (after stopping Journeys.API pid 14540 so default bin copy could succeed):**
- `docs-impact: OK`
- `graph-impact: OK nodes=campaign-agent, campaigns productUpdate=True waiver=True`
- sln build: **0 Error(s)**
- `agent-verify: OK` (CampaignAgent + docs + waiver in `-Files`; Journeys.Tests omitted so agent-verify does not run the entire test project)
- Listing Journeys.Tests in `-Files` previously ran 1232 tests; 44 `BatchJobAdapterIntegrationTests` failed against live Backend — unrelated to this task
- Scoped filter (Command 1): 35/35 pass

`docs-impact` required `testing.md` in `-Files` only when Journeys.Tests files are listed (path-map). The `testing.md` working-tree hunk is prior Infra.Llm retry wording, not this task.

Human G3/G4 (MSI tray live smoke) is **not** part of this task.

## Files Changed

| File | Action |
|------|--------|
| `Journeys.API/CampaignAgent/CampaignAgentOrchestrator.cs` | Modified — `OllamaToolRequire.Apply` after `new ChatOptions` |
| `docs/developer/campaign-agent-llm.md` | Modified (untracked file) — Specs merge + Live smoke |
| `docs/product/graph/waivers/2026-09-17-ollama-tool-require.md` | Created — graph waiver |

## Self-Review

- **Scope:** Only the named `ChatOptions` block, developer LLM doc, and waiver. No prompt text, no second hop, no Anthropic DI change, no brief stamp, no commit.
- **Wire:** `Apply` uses `CampaignAgentLlmProvider.Resolve(_configuration)` and `workflowState.Phase`; `ShouldRequire` already no-ops Anthropic, empty tools, and phase `Done`.
- **Clone:** Ollama mapper clones `ChatOptions`; `ToolMode.RequireAny` should reach the existing stream.
- **Docs:** Provider table preserved; one Specs line (no duplicated heading); live smoke matches brief.
- **Graph:** Waiver text matches brief; no ontology/capability invention.
- **Risk:** Low — one call on an existing options object. Live smoke remains operator-owned after merge-ready code. Running API still loads old binaries until restart.
