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

Expected: FAIL â€” `SaveModel` still filtered pre-brief.

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

`IsReadOnlyTool` stays unchanged (`SaveModel` is still not â€œread-onlyâ€ for other gates). Do not allow `DeleteModel`.

- [ ] **Step 4: Re-run Task 2 filters plus `CampaignWorkflowToolFilterTests` (full class)**

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~CampaignWorkflowToolFilterTests|FullyQualifiedName~CampaignWorkflowToolFilterSelectionTests"
```

Expected: PASS. Post-brief Events-incomplete still blocks `UpsertCampaign` / `ProcessEvent` and still allows `SaveModel`.

- [ ] **Step 5: Do not commit** unless the user asked in this message.

---

