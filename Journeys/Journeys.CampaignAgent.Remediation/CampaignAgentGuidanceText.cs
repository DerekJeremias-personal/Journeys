namespace Journeys.CampaignAgent.Remediation;

/// <summary>
/// Canonical agent-facing guidance strings shared across coaches and remediation catalogs.
/// </summary>
public static class CampaignAgentGuidanceText
{
    public const string JourneyNavigationRemediation =
        "Add journey.navigation: root Entry plus per-tier Transition (or Entry) with complete navConstraint rules. "
        + "Use PointBalanceProvider (not CurrentBalanceProvider) with tier-qualification pointAccountTypeId. "
        + "Mirror get_example_campaign TierSystem or get_rule_pattern_recipes Pattern B.";

    public const string ProcessEventEmptyAppliedRuleSetsPrefix =
        "process_event applied the campaign but AppliedRuleSetIds is empty — member is not in a journey node with firing rules. ";

    public const string ProcessEventEmptyAppliedRuleSetsTail =
        " UpsertCampaign, confirm ruleSetCount, then re-test process_event.";

    public static string ProcessEventEmptyAppliedRuleSetsRemediation =>
        ProcessEventEmptyAppliedRuleSetsPrefix + JourneyNavigationRemediation + ProcessEventEmptyAppliedRuleSetsTail;

    public static string ValidateNavigationRemediation =>
        JourneyNavigationRemediation + " Then re-validate before upsert_campaign.";

    public static string JourneyNavigationRemediationCatalogHint =>
        JourneyNavigationRemediation
        + " Confirm process_event AppliedRuleSetIds is non-empty before claiming success.";

    public const string RuleSetWrapperShapeHint =
        "journey.rules[] must be RuleSet wrappers (name + ruleJsonElement + outcomesJsonElement), "
        + "not raw Kind/children/outcomes objects.";

    public const string RuleSetWrapperShortHint =
        "journey.rules[] must be RuleSet wrappers (name + ruleJsonElement + outcomesJsonElement). ";

    public const string RuleSetWrapperShapeRemediationHint =
        "journey.rules[] must contain RuleSet objects: each entry needs ruleJsonElement (the Kind rule tree) "
        + "and outcomesJsonElement (outcome array). Do not put Kind, children, or outcomes directly in rules[]. "
        + "Mirror get_example_campaign or governance skeleton; then call get_campaign_assistant_context to confirm ruleSetCount > 0.";

    public const string EmptyJourneyRuleSetMarker = "journey.ruleSetCount is 0";

    public static string EmptyJourneyRuleSetRemediation =>
        "Campaign shell exists but " + EmptyJourneyRuleSetMarker + " — journey is not persisted. "
        + RuleSetWrapperShapeHint + " "
        + "Call get_campaign_assistant_context to confirm ruleSetCount before ProcessEvent or telling the user the campaign is complete.";

    public const string ValidatePassedRuleSetCountZero =
        "validate_campaign: IsValid but ruleSetCount=0 — journey not persisted; author rules/children and re-validate before upsert.";

    public const string JsonCasingPascalCaseRuleProvidersHint =
        "Use PascalCase JSON keys on rule providers (LeftProvider, RightProvider, Evaluator) — camelCase keys are not bound. See JSON CASING CONTRACT.";

    public const string JsonCasingAffectedPatHint =
        "Set AffectedPointAccountTypeIds (PascalCase key) to [\"<pat-guid>\"] from PointAccountManifest.items[].pointAccountTypeId — not manifest alias strings; not pointAccountTypeId on outcomes.";

    public const string JsonCasingAffectedPatKeyCheckHint =
        "If the PAT array looks populated, confirm the key is AffectedPointAccountTypeIds (PascalCase), not affectedPointAccountTypeIds.";
}
