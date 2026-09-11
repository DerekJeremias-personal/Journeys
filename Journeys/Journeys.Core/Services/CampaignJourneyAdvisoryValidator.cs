using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.DTO.Responses;

namespace Journeys.Core.Services;

/// <summary>
/// Validate-only advisory warnings for campaign journey authoring.
/// </summary>
public static class CampaignJourneyAdvisoryValidator
{
    public static IReadOnlyList<CampaignValidationFindingDto> Validate(
        Campaign? campaign,
        bool materialized)
    {
        var warnings = new List<CampaignValidationFindingDto>();
        if (campaign?.Journey == null)
            return warnings;

        var nodes = new List<NodeContext>();
        CollectNodes(campaign.Journey, "journey", isRoot: true, nodes);

        foreach (var ctx in nodes)
            VisitNode(ctx, warnings, materialized);

        EmitDuplicateNameWarnings(nodes, warnings);
        EmitDuplicateIdWarnings(nodes, warnings);

        if (materialized)
            EmitDuplicateSignatureWarnings(nodes, warnings);

        return warnings;
    }

    private static void VisitNode(NodeContext ctx, List<CampaignValidationFindingDto> warnings, bool materialized)
    {
        var node = ctx.Node;
        var hasRules = node.Rules?.Any(r => r != null && CampaignJourneyAuthoringShapeValidator.HasRuleSetPayload(r)) == true;
        var hasChildren = node.Children is { Count: > 0 };
        var hasEntry = HasNavigationChannel(node, NavigationType.Entry);
        var hasTransition = HasNavigationChannel(node, NavigationType.Transition);
        var hasAnyNav = HasAnyNavigation(node);

        if (string.IsNullOrWhiteSpace(node.Name) && (hasRules || hasChildren))
        {
            warnings.Add(Warning(
                "WARN_JOURNEY_NODE_UNNAMED",
                ctx.Path,
                node,
                "Journey node has rules or children but no name — add a descriptive name for coaching and debugging."));
        }

        if (!ctx.IsRoot && hasAnyNav && !hasRules)
        {
            warnings.Add(Warning(
                "WARN_JOURNEY_NODE_NO_RULES",
                ctx.Path,
                node,
                "Node has navigation but no rule sets — tier may be unreachable or have no member effects."));
        }

        if (hasRules)
        {
            if (hasTransition && !hasEntry)
            {
                warnings.Add(Warning(
                    "WARN_JOURNEY_NODE_NO_ENTRY",
                    ctx.Path,
                    node,
                    "Rule-bearing node has Transition navigation but no Entry — members may not enter this tier reliably."));
            }

            if (!hasEntry && !hasTransition)
            {
                warnings.Add(Warning(
                    "WARN_JOURNEY_NODE_NO_NAVIGATION",
                    ctx.Path,
                    node,
                    "Rule-bearing node has no Entry or Transition navigation — verify members can reach and fire rule sets."));
            }

            if (node.Rules != null)
            {
                for (var i = 0; i < node.Rules.Count; i++)
                {
                    var ruleSet = node.Rules[i];
                    if (ruleSet == null || !CampaignJourneyAuthoringShapeValidator.HasRuleSetPayload(ruleSet))
                        continue;

                    var ruleSetPath = $"{ctx.Path}/rules[{i}]";
                    var label = !string.IsNullOrWhiteSpace(ruleSet.Name) ? ruleSet.Name : ruleSet.Id ?? "unknown";

                    if (materialized && ruleSet.RuleTree == null)
                    {
                        warnings.Add(Warning(
                            "WARN_JOURNEY_RULESET_NO_RULE_TREE",
                            ruleSetPath,
                            node,
                            $"Rule set '{label}' has no rule tree — add gating logic in ruleJsonElement."));
                    }

                    if (materialized && (ruleSet.Outcomes == null || ruleSet.Outcomes.Count == 0))
                    {
                        warnings.Add(Warning(
                            "WARN_JOURNEY_RULESET_NO_OUTCOMES",
                            ruleSetPath,
                            node,
                            $"Rule set '{label}' has a rule tree but no outcomes — members may pass the rule with no effect."));
                    }
                    else if (!materialized
                             && !HasOutcomesJson(ruleSet.OutcomesJsonElement)
                             && ruleSet.Outcomes is not { Count: > 0 })
                    {
                        warnings.Add(Warning(
                            "WARN_JOURNEY_RULESET_NO_OUTCOMES",
                            ruleSetPath,
                            node,
                            $"Rule set '{label}' appears to have no outcomes — add outcomesJsonElement before save."));
                    }
                }
            }
        }
    }

    private static void EmitDuplicateNameWarnings(List<NodeContext> nodes, List<CampaignValidationFindingDto> warnings)
    {
        var groups = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Node.Name))
            .GroupBy(n => n.Node.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var ids = string.Join(", ", group.Select(n => n.Node.Id ?? n.Node.Name));
            warnings.Add(new CampaignValidationFindingDto
            {
                Code = "WARN_JOURNEY_DUPLICATE_NODE_NAME",
                Path = "journey",
                Message = $"Duplicate journey node name '{group.Key}' on nodes [{ids}] — rename or merge tiers.",
                Severity = "warning"
            });
        }
    }

    private static void EmitDuplicateIdWarnings(List<NodeContext> nodes, List<CampaignValidationFindingDto> warnings)
    {
        var groups = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Node.Id))
            .GroupBy(n => n.Node.Id!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            warnings.Add(new CampaignValidationFindingDto
            {
                Code = "WARN_JOURNEY_DUPLICATE_NODE_ID",
                Path = "journey",
                Message = $"Duplicate journey node id '{group.Key}' — assign unique ids before save.",
                Severity = "warning"
            });
        }
    }

    private static void EmitDuplicateSignatureWarnings(List<NodeContext> nodes, List<CampaignValidationFindingDto> warnings)
    {
        var ruleBearing = nodes
            .Where(n => JourneyNodeSignatureFingerprinter.HasRuleBearingPayload(n.Node))
            .Select(n => (n.Node.Id ?? n.Node.Name ?? "unknown", JourneyNodeSignatureFingerprinter.ComputeSignature(n.Node)))
            .Where(x => !string.IsNullOrEmpty(x.Item2))
            .ToList();

        var groups = ruleBearing
            .GroupBy(x => x.Item2, StringComparer.Ordinal)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var nodeIds = string.Join(", ", group.Select(x => x.Item1));
            warnings.Add(new CampaignValidationFindingDto
            {
                Code = "WARN_JOURNEY_DUPLICATE_NODE_SIGNATURE",
                Path = "journey",
                Message = $"Nodes [{nodeIds}] share structural signature sig={group.Key} — consider merging or differentiating entry criteria.",
                Severity = "warning"
            });
        }
    }

    private static void CollectNodes(JourneyNode node, string path, bool isRoot, List<NodeContext> nodes)
    {
        nodes.Add(new NodeContext(node, path, isRoot));

        if (node.Children == null)
            return;

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] == null)
                continue;
            CollectNodes(node.Children[i], $"{path}/children[{i}]", isRoot: false, nodes);
        }
    }

    private static bool HasNavigationChannel(JourneyNode node, NavigationType type) =>
        node.NavigationCriteria != null
        && node.NavigationCriteria.TryGetValue(type, out var criteria)
        && criteria is SimpleNavigationCriteria simple
        && simple.NavConstraint != null;

    private static bool HasAnyNavigation(JourneyNode node) =>
        node.NavigationCriteria != null
        && node.NavigationCriteria.Values.Any(c =>
            c is SimpleNavigationCriteria simple && simple.NavConstraint != null);

    private static CampaignValidationFindingDto Warning(
        string code,
        string path,
        JourneyNode node,
        string message) =>
        new()
        {
            Code = code,
            Path = path,
            NodeId = node.Id,
            NodeName = node.Name,
            Message = message,
            Severity = "warning"
        };

    private static bool HasOutcomesJson(JsonElement? el) =>
        el != null && el.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private sealed record NodeContext(JourneyNode Node, string Path, bool IsRoot);
}
