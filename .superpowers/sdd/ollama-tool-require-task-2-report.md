# Task 2 Report: Pre-brief `SaveModel` (TDD)

**Date:** 2026-09-17  
**Plan:** G1 of Campaign Agent Ollama tool-require — Task 2 only  
**Status:** Implementation complete; shell blocked in subagent — parent must verify tests

## What I Implemented

Updated `IsPreBriefTool` in `CampaignWorkflowToolFilter` so pre-brief workflow filtering allows `SaveModel` / `save_model` via `CampaignAgentBackendMcp.IsSaveModelToolName`, while still blocking other mutating backend tools (`DeleteModel`) and Journeys mutators (`UpsertCampaign`, `UpsertPointAccountType`).

### `IsPreBriefTool` Backend-model branch

**Before:** non-mutating backend model tools only (`!MutatingBackendTools.Contains(name)`).

**After:** non-mutating backend model tools **or** SaveModel names:

```csharp
if (CampaignWorkflowPhaseNames.BackendModelTools.Contains(name)
    && (!CampaignWorkflowPhaseNames.MutatingBackendTools.Contains(name)
        || CampaignAgentBackendMcp.IsSaveModelToolName(name)))
    return true;
```

`IsReadOnlyTool` unchanged — `SaveModel` remains excluded from the read-only gate (still in `MutatingBackendTools`).

Added `using Journeys.API.CampaignAgent;` for `CampaignAgentBackendMcp`.

## What I Tested and Results

### Test updates (Step 1 — failing assertions first)

| File | Change |
|------|--------|
| `CampaignWorkflowToolFilterTests.cs` | `PreBrief_WarehouseEnabled_AllowsWarehouseAndReads_BlocksMutators`: added `UpsertPointAccountType`, `save_model`, `DeleteModel`; assert `SaveModel`/`save_model` **contained**; assert mutators absent |
| `CampaignWorkflowToolFilterSelectionTests.cs` | Renamed to `EventModelSelection_pre_brief_allows_SaveModel_hides_UpsertCampaign`; assert `SaveModel` present, `UpsertCampaign` absent |

**Execution:** Shell/dotnet was blocked by preToolUse hook in this subagent session. Tests were not executed here. Parent agent should run verification (see below).

## TDD Evidence

### RED (Step 2 — after test edits, before implementation)

**Command:**

```powershell
cd C:\Dev\Journeys\Journeys
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~CampaignWorkflowToolFilterTests.PreBrief_WarehouseEnabled|FullyQualifiedName~CampaignWorkflowToolFilterSelectionTests.EventModelSelection" -o $env:TEMP\journeys-test-out
```

**Expected failing output (before `IsPreBriefTool` change):**

```
Failed Journeys.Tests.CampaignAgent.CampaignWorkflowToolFilterTests.PreBrief_WarehouseEnabled_AllowsWarehouseAndReads_BlocksMutators
  Assert.Contains() Failure: Item not found in collection
  Expected: SaveModel (and save_model)

Failed Journeys.Tests.Workflow.CampaignWorkflowToolFilterSelectionTests.EventModelSelection_pre_brief_allows_SaveModel_hides_UpsertCampaign
  Assert.Contains() Failure: "SaveModel" not in filtered list
```

Prior code excluded all `MutatingBackendTools` pre-brief, including `SaveModel`.

### GREEN (Step 4 — after implementation)

**Command:**

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~CampaignWorkflowToolFilterTests|FullyQualifiedName~CampaignWorkflowToolFilterSelectionTests" -o $env:TEMP\journeys-test-out
```

**Expected passing output:**

```
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14
```

(12 tests in `CampaignWorkflowToolFilterTests` + 2 in `CampaignWorkflowToolFilterSelectionTests`.)

**Regression checks preserved by existing tests:**

- `PostBrief_EventModelsIncomplete_BlocksCampaignMutators` — still allows `SaveModel`, blocks `UpsertCampaign` / `ProcessEvent`
- `PreBrief_WarehouseDisabled_AllowsObjectiveTool_BlocksMutators` — `GetModel` allowed, `UpsertCampaign` blocked
- `DeleteModel` excluded pre-brief via new assertions

**Actual in this session:** Not run (shell blocked). Code matches brief verbatim; no linter issues on touched files.

## Files Changed

| File | Action |
|------|--------|
| `Journeys.Tests/CampaignAgent/CampaignWorkflowToolFilterTests.cs` | Modified — pre-brief SaveModel assertions |
| `Journeys.Tests/Workflow/CampaignWorkflowToolFilterSelectionTests.cs` | Modified — renamed test, expect SaveModel pre-brief |
| `Journeys.API/CampaignAgent/Workflow/CampaignWorkflowToolFilter.cs` | Modified — `IsPreBriefTool` SaveModel exception only |

## Self-Review

- **Scope:** Only `IsPreBriefTool` backend branch changed; `IsReadOnlyTool`, orchestrator, and Task 1 (`OllamaToolRequire`) untouched.
- **Logic:** `SaveModel`/`save_model` pass pre-brief via `IsSaveModelToolName`; `DeleteModel` still blocked (in `MutatingBackendTools`, not SaveModel).
- **Naming:** Uses existing `CampaignAgentBackendMcp.IsSaveModelToolName` — no duplicate string checks.
- **Tests:** Match brief verbatim; post-brief EventModels-gate behavior unchanged (separate code path).
- **Risk:** Low — narrow exception for one tool family in one gate only.

## Parent Verification

Run GREEN command above. If MSB3027 file lock, keep `-o $env:TEMP\journeys-test-out`.
