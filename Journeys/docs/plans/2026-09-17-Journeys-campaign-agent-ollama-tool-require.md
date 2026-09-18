# Campaign Agent Ollama tool require Implementation Plan

> **Execution:** After approval, say execute and the intent name. `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic`. Do **not** use superpowers:subagent-driven-development. Linear unit issues land before any `Journeys.*` code. Do not `--review none` or Express.

**Goal:** On the Ollama Campaign Agent path, require a tool call from the already-filtered set, and allow `SaveModel` before a campaign design brief, so 8B cannot silently narrate APIs and a model-only persist can succeed.

**Architecture:** `CampaignWorkflowToolFilter` still chooses legal tools from workflow flags. New `OllamaToolRequire` sets `ChatOptions.ToolMode = ChatToolMode.RequireAny` when the provider is OpenAI-compatible, the filtered list is non-empty, and phase is not `Done`. Anthropic leaves `ToolMode` unset (auto). No synthetic brief, no extra streaming hop.

**Tech Stack:** net8, `Microsoft.Extensions.AI` 9.9.0 (`ChatOptions.ToolMode`, `ChatToolMode.RequireAny`), xUnit in `Journeys.Tests`.

**Spec:** `docs/specs/2026-09-17-Journeys-campaign-agent-ollama-tool-require-design.md`

## Global Constraints

- Do not invent capability ids. Existing: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- Do not stamp or auto-fill `CampaignDesignBriefProposed`.
- Do not add a second streaming hop when the model returns text-only.
- Do not parse user utterances for intent.
- Do not change `IsBriefCaptured`, Events-gate, PAT, or journey filters except the pre-brief `SaveModel` allow.
- Do not change `Journeys.Agent`, `Journeys.UX`, or HintPath `Backend.Llm.OpenAICompatible`.
- Do not add live Ollama tests. Do not hit `192.168.1.191` from CI.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.
- Never log tokens, API keys, connection strings, or full tool payloads.
- File-scoped namespaces, nullable enable, constructor injection, readonly `_camelCase`.

## File map

| Path | Responsibility |
|------|----------------|
| `Journeys.API/CampaignAgent/OllamaToolRequire.cs` | Predicate + apply `ToolMode` on `ChatOptions` |
| `Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs` | G2 unit tests |
| `Journeys.API/CampaignAgent/Workflow/CampaignWorkflowToolFilter.cs` | G1 pre-brief `SaveModel` |
| `Journeys.Tests/CampaignAgent/CampaignWorkflowToolFilterTests.cs` | Invert pre-brief `SaveModel` assert; still block campaign mutators / `DeleteModel` |
| `Journeys.Tests/Workflow/CampaignWorkflowToolFilterSelectionTests.cs` | Pre-brief EventModelSelection now allows `SaveModel` |
| `Journeys.API/CampaignAgent/CampaignAgentOrchestrator.cs` | Call `OllamaToolRequire.Apply` after building `ChatOptions` |
| `docs/developer/campaign-agent-llm.md` | Live G3/G4 + model-tag fallback |
| `docs/product/graph/waivers/2026-09-17-ollama-tool-require.md` | graph-impact: no new capability / ontology |

---

### Task 1: `OllamaToolRequire` (TDD)

**Files:**
- Create: `Journeys.API/CampaignAgent/OllamaToolRequire.cs`
- Test: `Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs`

**Interfaces:**
- Consumes: `CampaignAgentLlmProviderKind`, `CampaignWorkflowPhase`, `ChatOptions` (`Microsoft.Extensions.AI`)
- Produces:
  - `OllamaToolRequire.ShouldRequire(CampaignAgentLlmProviderKind provider, int filteredToolCount, CampaignWorkflowPhase phase)` → `bool`
  - `OllamaToolRequire.Apply(ChatOptions options, CampaignAgentLlmProviderKind provider, CampaignWorkflowPhase phase)` → void; sets `options.ToolMode = ChatToolMode.RequireAny` iff `ShouldRequire` is true using `options.Tools?.Count ?? 0`; otherwise leaves `ToolMode` unchanged

- [ ] **Step 1: Write failing tests** `Journeys.Tests/CampaignAgent/OllamaToolRequireTests.cs`

```csharp
using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.CampaignAgent;

public class OllamaToolRequireTests
{
    [Fact]
    public void ShouldRequire_true_for_ollama_with_tools_and_not_done()
    {
        Assert.True(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 2, CampaignWorkflowPhase.DataAnalysis));
    }

    [Theory]
    [InlineData(CampaignWorkflowPhase.EventModels)]
    [InlineData(CampaignWorkflowPhase.CampaignBuild)]
    [InlineData(CampaignWorkflowPhase.Verification)]
    public void ShouldRequire_true_for_other_non_done_phases(CampaignWorkflowPhase phase)
    {
        Assert.True(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 1, phase));
    }

    [Fact]
    public void ShouldRequire_false_for_anthropic()
    {
        Assert.False(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.Anthropic, 3, CampaignWorkflowPhase.DataAnalysis));
    }

    [Fact]
    public void ShouldRequire_false_when_no_tools()
    {
        Assert.False(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 0, CampaignWorkflowPhase.DataAnalysis));
    }

    [Fact]
    public void ShouldRequire_false_when_done()
    {
        Assert.False(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 2, CampaignWorkflowPhase.Done));
    }

    [Fact]
    public void Apply_sets_RequireAny_when_should_require()
    {
        var options = new ChatOptions { Tools = [new TestTool("list_campaigns")] };
        OllamaToolRequire.Apply(
            options, CampaignAgentLlmProviderKind.OpenAICompatible, CampaignWorkflowPhase.DataAnalysis);
        Assert.Equal(ChatToolMode.RequireAny, options.ToolMode);
    }

    [Fact]
    public void Apply_leaves_ToolMode_unset_for_anthropic()
    {
        var options = new ChatOptions { Tools = [new TestTool("list_campaigns")] };
        OllamaToolRequire.Apply(
            options, CampaignAgentLlmProviderKind.Anthropic, CampaignWorkflowPhase.DataAnalysis);
        Assert.Null(options.ToolMode);
    }

    private sealed class TestTool(string name) : AITool
    {
        public override string Name => name;
        public override string Description => name;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~OllamaToolRequireTests"
```

If MSB3027 file lock (running `Journeys.API`): add `-o $env:TEMP\journeys-test-out`.

Expected: FAIL compile (`OllamaToolRequire` does not exist).

- [ ] **Step 3: Write minimal implementation** `Journeys.API/CampaignAgent/OllamaToolRequire.cs`

```csharp
using Journeys.Core.Models;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

public static class OllamaToolRequire
{
    public static bool ShouldRequire(
        CampaignAgentLlmProviderKind provider,
        int filteredToolCount,
        CampaignWorkflowPhase phase)
    {
        if (provider != CampaignAgentLlmProviderKind.OpenAICompatible)
            return false;
        if (filteredToolCount <= 0)
            return false;
        if (phase == CampaignWorkflowPhase.Done)
            return false;
        return true;
    }

    public static void Apply(
        ChatOptions options,
        CampaignAgentLlmProviderKind provider,
        CampaignWorkflowPhase phase)
    {
        ArgumentNullException.ThrowIfNull(options);
        var count = options.Tools?.Count ?? 0;
        if (ShouldRequire(provider, count, phase))
            options.ToolMode = ChatToolMode.RequireAny;
    }
}
```

- [ ] **Step 4: Re-run tests**

Same `dotnet test` filter as Step 2.

Expected: PASS.

- [ ] **Step 5: Do not commit** unless the user asked in this message.

---

### Task 2: Pre-brief `SaveModel` (TDD)

**Files:**
- Modify: `Journeys.API/CampaignAgent/Workflow/CampaignWorkflowToolFilter.cs` (`IsPreBriefTool`)
- Modify: `Journeys.Tests/CampaignAgent/CampaignWorkflowToolFilterTests.cs`
- Modify: `Journeys.Tests/Workflow/CampaignWorkflowToolFilterSelectionTests.cs`

**Interfaces:**
- Consumes: `CampaignWorkflowPhaseNames.BackendModelTools`, `MutatingBackendTools`, `CampaignAgentBackendMcp.IsSaveModelToolName`
- Produces: pre-brief `Apply` includes `SaveModel` / `save_model`; still excludes `UpsertCampaign`, `UpsertPointAccountType`, `DeleteModel`

- [ ] **Step 1: Update failing assertions**

In `CampaignWorkflowToolFilterTests.PreBrief_WarehouseEnabled_AllowsWarehouseAndReads_BlocksMutators`, add `DeleteModel` and `UpsertPointAccountType` to the input list. Change the `SaveModel` assert to **contains**. Keep campaign mutators and `DeleteModel` absent:

```csharp
[Fact]
public void PreBrief_WarehouseEnabled_AllowsWarehouseAndReads_BlocksMutators()
{
    var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
    var tools = new List<AITool>
    {
        Tool("GetProgramPerformanceSummary"),
        Tool("ListCampaigns"),
        Tool("UpsertCampaign"),
        Tool("UpsertPointAccountType"),
        Tool("SaveModel"),
        Tool("save_model"),
        Tool("DeleteModel")
    };
    var filtered = CampaignWorkflowToolFilter.Apply(tools, state, dataWarehouseEnabled: true);
    Assert.Contains(filtered, t => t.Name == "GetProgramPerformanceSummary");
    Assert.Contains(filtered, t => t.Name == "ListCampaigns");
    Assert.Contains(filtered, t => t.Name == "SaveModel");
    Assert.Contains(filtered, t => t.Name == "save_model");
    Assert.DoesNotContain(filtered, t => t.Name == "UpsertCampaign");
    Assert.DoesNotContain(filtered, t => t.Name == "UpsertPointAccountType");
    Assert.DoesNotContain(filtered, t => t.Name == "DeleteModel");
}
```

In `CampaignWorkflowToolFilterSelectionTests.EventModelSelection_gate_exposes_readonly_backend_tools_and_hides_SaveModel`: that state has **no** brief, so it is pre-brief. Rename to `EventModelSelection_pre_brief_allows_SaveModel_hides_UpsertCampaign` and expect `SaveModel` present, `UpsertCampaign` absent. Keep GetModel / GetAllModels / ListModels.

```csharp
[Fact]
public void EventModelSelection_pre_brief_allows_SaveModel_hides_UpsertCampaign()
{
    var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
    state.Phase = CampaignWorkflowPhase.EventModels;
    state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;

    var filtered = CampaignWorkflowToolFilter.Apply(Tools(), state).Select(t => t.Name).ToList();

    Assert.Contains("GetModel", filtered);
    Assert.Contains("GetAllModels", filtered);
    Assert.Contains("ListModels", filtered);
    Assert.Contains("SaveModel", filtered);
    Assert.DoesNotContain("UpsertCampaign", filtered);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~CampaignWorkflowToolFilterTests.PreBrief_WarehouseEnabled|FullyQualifiedName~CampaignWorkflowToolFilterSelectionTests.EventModelSelection"
```

Expected: FAIL — `SaveModel` still filtered pre-brief.

- [ ] **Step 3: Change `IsPreBriefTool`** in `CampaignWorkflowToolFilter.cs`

Replace the Backend-model branch:

```csharp
        if (CampaignWorkflowPhaseNames.BackendModelTools.Contains(name)
            && !CampaignWorkflowPhaseNames.MutatingBackendTools.Contains(name))
            return true;
        return false;
```

with:

```csharp
        if (CampaignWorkflowPhaseNames.BackendModelTools.Contains(name)
            && (!CampaignWorkflowPhaseNames.MutatingBackendTools.Contains(name)
                || CampaignAgentBackendMcp.IsSaveModelToolName(name)))
            return true;
        return false;
```

`IsReadOnlyTool` stays unchanged (`SaveModel` is still not “read-only” for other gates). Do not allow `DeleteModel`.

- [ ] **Step 4: Re-run Task 2 filters plus `CampaignWorkflowToolFilterTests` (full class)**

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~CampaignWorkflowToolFilterTests|FullyQualifiedName~CampaignWorkflowToolFilterSelectionTests"
```

Expected: PASS. Post-brief Events-incomplete still blocks `UpsertCampaign` / `ProcessEvent` and still allows `SaveModel`.

- [ ] **Step 5: Do not commit** unless the user asked in this message.

---

### Task 3: Orchestrator wire + docs + verify

**Files:**
- Modify: `Journeys.API/CampaignAgent/CampaignAgentOrchestrator.cs` (the `new ChatOptions` block ~601–606)
- Modify: `docs/developer/campaign-agent-llm.md`
- Create: `docs/product/graph/waivers/2026-09-17-ollama-tool-require.md`

**Interfaces:**
- Consumes: `OllamaToolRequire.Apply`, `CampaignAgentLlmProvider.Resolve(_configuration)`, `workflowState.Phase`
- Produces: Ollama turns with tools get `ToolMode.RequireAny`; Anthropic unchanged

- [ ] **Step 1: After constructing `chatTemplate`, apply require**

Replace:

```csharp
                var chatTemplate = new ChatOptions
                {
                    Tools = aiTools,
                    MaxOutputTokens = maxOut,
                    AllowMultipleToolCalls = allowMultiTool
                };
```

with:

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

Do not add a second streaming hop. Do not change the Anthropic DI branch.

- [ ] **Step 2: Update `docs/developer/campaign-agent-llm.md`**

Keep the existing provider table. Replace **Live smoke** with:

```markdown
**Specs:** `docs/specs/2026-09-16-Journeys-campaign-agent-ollama-design.md`, `docs/specs/2026-09-17-Journeys-campaign-agent-ollama-tool-require-design.md`

## Live smoke

Anonymous-dev Campaign Agent SSE is already allowed in Development. UX at `/loyalty/campaigns/agent`. Conversation rows need the `AgentMessage` Backend model (`docs/developer/journeys-ux.md`).

1. New thread on `TestTenant1`: one turn must produce at least one **successful** MCP tool (`list_campaigns`, `list_models` / `GetModel`, or `SaveModel`). Assistant-only text is a fail. Ollama turns (except phase `Done`) send `ChatToolMode.RequireAny` so the model cannot skip tools.
2. Follow-up, **no** campaign brief: persist a Review loyalty model (`id`, `userid`, `comments`, `NumStars`, `reviewdate`) via `SaveModel`. Confirm it in Backend. `UpsertCampaign` stays unavailable until a real brief (`ProposeCampaignDesignBrief` / warehouse).
3. If step 1 fails on `llama3.1:8b`, set `CampaignAgent:OpenAICompatible:Model` to a tool-capable tag already on the MSI tray, **restart** `Journeys.API`, retry 1 then 2. Provider and model are startup-only.

Not required: Draft campaign upsert, Live publish, CI Ollama.
```

Prepend the Specs line under the existing title (merge with the current `**Spec:**` line rather than duplicating the heading).

- [ ] **Step 3: Write graph waiver** `docs/product/graph/waivers/2026-09-17-ollama-tool-require.md`

```markdown
# Waiver: Ollama tool require and pre-brief SaveModel

**Reason:** Campaign Agent Ollama now requires a function call from the filtered tool set, and `SaveModel` is legal before a campaign design brief. No new capability, ontology term, or graph edge. Campaign persist still uses a real brief.

**Nodes touched by path-map (no meaning change):** `campaign-agent`, `campaigns`.
```

- [ ] **Step 4: Build + docs-impact + graph-impact**

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~OllamaToolRequireTests|FullyQualifiedName~CampaignWorkflowToolFilterTests|FullyQualifiedName~CampaignWorkflowToolFilterSelectionTests|FullyQualifiedName~CampaignAgentLlmProviderTests"
.\scripts\agent-verify.ps1 -Files @(
  "Journeys.API\CampaignAgent\OllamaToolRequire.cs",
  "Journeys.API\CampaignAgent\CampaignAgentOrchestrator.cs",
  "Journeys.API\CampaignAgent\Workflow\CampaignWorkflowToolFilter.cs",
  "Journeys.Tests\CampaignAgent\OllamaToolRequireTests.cs",
  "Journeys.Tests\CampaignAgent\CampaignWorkflowToolFilterTests.cs",
  "Journeys.Tests\Workflow\CampaignWorkflowToolFilterSelectionTests.cs",
  "docs\developer\campaign-agent-llm.md",
  "docs\developer\tools.md",
  "docs\platform\architecture.md",
  "docs\product\ontology\draft-live.md",
  "docs\product\graph\waivers\2026-09-17-ollama-tool-require.md"
)
```

Expected: tests PASS; `agent-verify: OK`.

Human G3/G4 (MSI tray) is **not** part of this task. After merge-ready code, the operator runs the live smoke in `campaign-agent-llm.md`. If G3 fails, change `OpenAICompatible:Model` and restart — do not add prompt text.

- [ ] **Step 5: Do not commit** unless the user asked in this message.

---

## Spec coverage

| Spec | Task |
|------|------|
| G1 SaveModel pre-brief | Task 2 |
| G2 Ollama RequireAny | Task 1 + Task 3 |
| G3 live MCP | Task 3 docs (human) |
| G4 live SaveModel | Task 3 docs (human) |
| No synthetic brief / no hop / no utterance parser | Global constraints |
| Tests no live Ollama | Task 1–3 filters |
| docs-impact / graph-impact | Task 3 |
