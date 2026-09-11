using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Responses;
using Journeys.DTO.Exceptions;

namespace Journeys.Tests.Utility;

public class CampaignValidationNextStepsBuilderTests
{
    [Fact]
    public void Build_affirm_zero_rule_sets_does_not_authorize_upsert()
    {
        var summary = new CampaignValidationSummaryDto
        {
            HasJourney = true,
            RuleSetCount = 0,
            JourneyNodeCount = 1,
            Status = CampaignStatusStrings.Draft
        };

        var steps = CampaignValidationNextStepsBuilder.Build(
            Array.Empty<CampaignValidationFindingDto>(),
            Array.Empty<CampaignValidationFindingDto>(),
            summary);

        Assert.Contains(steps, s => s.Contains("0 rule sets", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, s => s.Contains("children[]", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(steps, s =>
            s.Contains("validated successfully", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_affirm_positive_rule_sets_authorizes_upsert()
    {
        var summary = new CampaignValidationSummaryDto
        {
            HasJourney = true,
            RuleSetCount = 3,
            Status = CampaignStatusStrings.Draft
        };

        var steps = CampaignValidationNextStepsBuilder.Build(
            Array.Empty<CampaignValidationFindingDto>(),
            Array.Empty<CampaignValidationFindingDto>(),
            summary);

        Assert.Contains(steps, s => s.Contains("ruleSetCount=3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, s => s.Contains("upsert_campaign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_affirm_mode_returns_non_empty_steps_when_clean()
    {
        var summary = new CampaignValidationSummaryDto
        {
            HasJourney = true,
            RuleSetCount = 2,
            Status = CampaignStatusStrings.Draft
        };

        var steps = CampaignValidationNextStepsBuilder.Build(
            Array.Empty<CampaignValidationFindingDto>(),
            Array.Empty<CampaignValidationFindingDto>(),
            summary);

        Assert.NotEmpty(steps);
        Assert.Contains(steps, s => s.Contains("ruleSetCount=2", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, s => s.Contains("upsert_campaign", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, s => s.Contains("process_event(campaignId)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_remediate_mode_maps_navigation_errors()
    {
        var errors = new[]
        {
            new CampaignValidationFindingDto
            {
                Code = "JOURNEY_NAV_ROOT_ENTRY_REQUIRED",
                Path = "journey",
                Message = "missing entry"
            }
        };

        var steps = CampaignValidationNextStepsBuilder.Build(
            errors,
            Array.Empty<CampaignValidationFindingDto>(),
            new CampaignValidationSummaryDto());

        Assert.Contains(steps, s => s.Contains("Entry/Transition", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, s => s.Contains("re-validate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_improve_mode_addresses_duplicate_warnings()
    {
        var warnings = new[]
        {
            new CampaignValidationFindingDto
            {
                Code = "WARN_JOURNEY_DUPLICATE_NODE_NAME",
                Message = "duplicate"
            }
        };

        var steps = CampaignValidationNextStepsBuilder.Build(
            Array.Empty<CampaignValidationFindingDto>(),
            warnings,
            new CampaignValidationSummaryDto { WarningCount = 1 });

        Assert.Contains(steps, s => s.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_remediate_missing_affected_pat_uses_PascalCase_key()
    {
        var errors = new[]
        {
            new CampaignValidationFindingDto
            {
                Code = "TIER_A_DEPOSIT_MISSING_AFFECTED_PAT",
                Field = "journey.validation.0"
            }
        };

        var steps = CampaignValidationNextStepsBuilder.Build(
            errors,
            Array.Empty<CampaignValidationFindingDto>(),
            new CampaignValidationSummaryDto());

        Assert.Contains(steps, s => s.Contains("AffectedPointAccountTypeIds", StringComparison.Ordinal));
        Assert.DoesNotContain(steps, s => s.Contains("affectedPointAccountTypeIds with", StringComparison.Ordinal));
    }
}
