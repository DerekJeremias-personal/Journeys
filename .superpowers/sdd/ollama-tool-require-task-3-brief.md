### Task 3: Orchestrator wire + docs + verify

**Files:**
- Modify: `Journeys.API/CampaignAgent/CampaignAgentOrchestrator.cs` (the `new ChatOptions` block ~601â€“606)
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

Human G3/G4 (MSI tray) is **not** part of this task. After merge-ready code, the operator runs the live smoke in `campaign-agent-llm.md`. If G3 fails, change `OpenAICompatible:Model` and restart â€” do not add prompt text.

- [ ] **Step 5: Do not commit** unless the user asked in this message.

---

