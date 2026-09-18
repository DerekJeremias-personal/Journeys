# Review package: uncommitted Task 2 (no commits per human rule)

## Commits
(none)

## Test evidence (controller)
dotnet test filter CampaignWorkflowToolFilterTests|CampaignWorkflowToolFilterSelectionTests -o TEMP: Passed 14/14.

## Diff
diff --git a/Journeys/Journeys.API/CampaignAgent/Workflow/CampaignWorkflowToolFilter.cs b/Journeys/Journeys.API/CampaignAgent/Workflow/CampaignWorkflowToolFilter.cs
index ea72c94..e728648 100644
--- a/Journeys/Journeys.API/CampaignAgent/Workflow/CampaignWorkflowToolFilter.cs
+++ b/Journeys/Journeys.API/CampaignAgent/Workflow/CampaignWorkflowToolFilter.cs
@@ -1,10 +1,11 @@
+using Journeys.API.CampaignAgent;
 using Journeys.Core.Models;
 using Journeys.Core.Utility;
 using Journeys.Core.Workflow;
 using Microsoft.Extensions.AI;
 
 namespace Journeys.API.CampaignAgent.Workflow;
 
 /// <summary>
 /// Removes tools that are not allowed in the current workflow phase (strict mode).
 /// Pre-brief: read-only, warehouse/objective, and non-mutating backend tools.
@@ -104,21 +105,22 @@ public static class CampaignWorkflowToolFilter
         return false;
     }
 
     private static bool IsPreBriefTool(string? name, bool dataWarehouseEnabled)
     {
         if (string.IsNullOrEmpty(name)) return false;
         if (IsReadOnlyTool(name)) return true;
         if (dataWarehouseEnabled && CampaignWorkflowPhaseNames.IsDataWarehouseTool(name)) return true;
         if (!dataWarehouseEnabled && CampaignWorkflowPhaseNames.IsObjectiveProposalTool(name)) return true;
         if (CampaignWorkflowPhaseNames.BackendModelTools.Contains(name)
-            && !CampaignWorkflowPhaseNames.MutatingBackendTools.Contains(name))
+            && (!CampaignWorkflowPhaseNames.MutatingBackendTools.Contains(name)
+                || CampaignAgentBackendMcp.IsSaveModelToolName(name)))
             return true;
         return false;
     }
 
     private static bool IsReadOnlyTool(string? name)
     {
         if (string.IsNullOrEmpty(name))
             return false;
         if (CampaignWorkflowPhaseNames.IsDataWarehouseTool(name))
             return true;
diff --git a/Journeys/Journeys.Tests/CampaignAgent/CampaignWorkflowToolFilterTests.cs b/Journeys/Journeys.Tests/CampaignAgent/CampaignWorkflowToolFilterTests.cs
index ae2de17..d3cf5aa 100644
--- a/Journeys/Journeys.Tests/CampaignAgent/CampaignWorkflowToolFilterTests.cs
+++ b/Journeys/Journeys.Tests/CampaignAgent/CampaignWorkflowToolFilterTests.cs
@@ -11,27 +11,33 @@ public class CampaignWorkflowToolFilterTests
 
     [Fact]
     public void PreBrief_WarehouseEnabled_AllowsWarehouseAndReads_BlocksMutators()
     {
         var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
         var tools = new List<AITool>
         {
             Tool("GetProgramPerformanceSummary"),
             Tool("ListCampaigns"),
             Tool("UpsertCampaign"),
-            Tool("SaveModel")
+            Tool("UpsertPointAccountType"),
+            Tool("SaveModel"),
+            Tool("save_model"),
+            Tool("DeleteModel")
         };
         var filtered = CampaignWorkflowToolFilter.Apply(tools, state, dataWarehouseEnabled: true);
         Assert.Contains(filtered, t => t.Name == "GetProgramPerformanceSummary");
         Assert.Contains(filtered, t => t.Name == "ListCampaigns");
+        Assert.Contains(filtered, t => t.Name == "SaveModel");
+        Assert.Contains(filtered, t => t.Name == "save_model");
         Assert.DoesNotContain(filtered, t => t.Name == "UpsertCampaign");
-        Assert.DoesNotContain(filtered, t => t.Name == "SaveModel");
+        Assert.DoesNotContain(filtered, t => t.Name == "UpsertPointAccountType");
+        Assert.DoesNotContain(filtered, t => t.Name == "DeleteModel");
     }
 
     [Fact]
     public void PreBrief_WarehouseDisabled_AllowsObjectiveTool_BlocksMutators()
     {
         var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
         var tools = new List<AITool>
         {
             Tool("ProposeCampaignDesignBrief"),
             Tool("UpsertCampaign"),
diff --git a/Journeys/Journeys.Tests/Workflow/CampaignWorkflowToolFilterSelectionTests.cs b/Journeys/Journeys.Tests/Workflow/CampaignWorkflowToolFilterSelectionTests.cs
index b76d253..8ec7646 100644
--- a/Journeys/Journeys.Tests/Workflow/CampaignWorkflowToolFilterSelectionTests.cs
+++ b/Journeys/Journeys.Tests/Workflow/CampaignWorkflowToolFilterSelectionTests.cs
@@ -9,32 +9,32 @@ public class CampaignWorkflowToolFilterSelectionTests
     private static List<AITool> Tools() => new()
     {
         Tool("GetModel"),
         Tool("GetAllModels"),
         Tool("ListModels"),
         Tool("SaveModel"),
         Tool("UpsertCampaign")
     };
 
     [Fact]
-    public void EventModelSelection_gate_exposes_readonly_backend_tools_and_hides_SaveModel()
+    public void EventModelSelection_pre_brief_allows_SaveModel_hides_UpsertCampaign()
     {
         var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
         state.Phase = CampaignWorkflowPhase.EventModels;
         state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;
 
         var filtered = CampaignWorkflowToolFilter.Apply(Tools(), state).Select(t => t.Name).ToList();
 
         Assert.Contains("GetModel", filtered);
         Assert.Contains("GetAllModels", filtered);
         Assert.Contains("ListModels", filtered);
-        Assert.DoesNotContain("SaveModel", filtered);
+        Assert.Contains("SaveModel", filtered);
         Assert.DoesNotContain("UpsertCampaign", filtered);
     }
 
     [Fact]
     public void PostBrief_exposes_SaveModel()
     {
         var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
         state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
 
         var filtered = CampaignWorkflowToolFilter.Apply(Tools(), state).Select(t => t.Name).ToList();
