using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.Core.RulesEngine.Comparitors;

namespace Journeys.Core.Services;

/// <summary>
/// Canonical structural fingerprint for rule-bearing journey nodes (navigation + rule sets).
/// </summary>
public static class JourneyNodeSignatureFingerprinter
{
    public static string ComputeSignature(JourneyNode node)
    {
        var canonical = new StringBuilder();
        AppendNavigation(canonical, node);
        AppendRuleSets(canonical, node);

        if (canonical.Length == 0)
            return string.Empty;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    public static bool HasRuleBearingPayload(JourneyNode node) =>
        node.Rules?.Any(r => r != null && CampaignJourneyAuthoringShapeValidator.HasRuleSetPayload(r)) == true;

    private static void AppendNavigation(StringBuilder sb, JourneyNode node)
    {
        if (node.NavigationCriteria == null || node.NavigationCriteria.Count == 0)
            return;

        foreach (var type in node.NavigationCriteria.Keys.OrderBy(k => k))
        {
            if (node.NavigationCriteria[type] is not SimpleNavigationCriteria simple || simple.NavConstraint == null)
                continue;

            sb.Append("nav:").Append(type).Append('=');
            AppendRuleTree(sb, simple.NavConstraint);
            sb.Append(';');
        }
    }

    private static void AppendRuleSets(StringBuilder sb, JourneyNode node)
    {
        if (node.Rules == null)
            return;

        var ordered = node.Rules
            .Where(r => r != null)
            .OrderBy(r => r!.Name ?? r.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        foreach (var ruleSet in ordered)
        {
            sb.Append("rs:");
            if (ruleSet!.RuleTree != null)
            {
                AppendRuleTree(sb, ruleSet.RuleTree);
            }

            if (ruleSet.Outcomes is { Count: > 0 })
            {
                foreach (var kind in ruleSet.Outcomes.Where(o => o != null).Select(o => o!.Kind).OrderBy(k => k, StringComparer.Ordinal))
                    sb.Append('|').Append(kind);
            }

            sb.Append(';');
        }
    }

    private static void AppendRuleTree(StringBuilder sb, RuleBase? rule)
    {
        if (rule == null)
            return;

        sb.Append(rule.Kind);

        if (rule is CompositeRuleBase composite && composite.Children != null)
        {
            sb.Append('(');
            foreach (var child in composite.Children)
                AppendRuleTree(sb, child);
            sb.Append(')');
            return;
        }

        AppendProviderTypes(sb, rule);
    }

    private static void AppendProviderTypes(StringBuilder sb, object obj)
    {
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.GetIndexParameters().Length > 0)
                continue;

            var value = prop.GetValue(obj);
            if (value == null)
                continue;

            if (value is RuleBase nestedRule)
            {
                sb.Append('.').Append(prop.Name).Append(':');
                AppendRuleTree(sb, nestedRule);
            }
            else if (value is IValueProvider provider)
            {
                sb.Append('.').Append(prop.Name).Append('=').Append(provider.GetType().Name);
                AppendProviderTypes(sb, provider);
            }
            else if (value is IEvaluatable evaluator)
            {
                sb.Append('.').Append(prop.Name).Append('=').Append(evaluator.GetType().Name);
            }
        }
    }
}
