using System.Text.Json;
using Journeys.Core.Extensions;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Responses;

namespace Journeys.Core.Services;

public sealed class CampaignValidationOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly CampaignTaxonomyRuleValidator _taxonomyRuleValidator;
    private readonly CampaignJourneyPointOutcomePatValidator _pointOutcomePatValidator;

    public CampaignValidationOrchestrator(
        CampaignTaxonomyRuleValidator taxonomyRuleValidator,
        CampaignJourneyPointOutcomePatValidator pointOutcomePatValidator)
    {
        _taxonomyRuleValidator = taxonomyRuleValidator;
        _pointOutcomePatValidator = pointOutcomePatValidator;
    }

    public async Task<CampaignValidationResultDto> ValidateAsync(
        string tenantId,
        CampaignDto campaignDto,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<CampaignValidationFindingDto>();
        var materialized = false;
        Campaign? campaign = campaignDto.FromDto();
        RunShellValidation(campaign, errors);
        RunJsonPreValidators(campaignDto, errors);
        materialized = await RunDefinitionValidationStepsAsync(tenantId, campaign, errors, cancellationToken)
            .ConfigureAwait(false);
        var warnings = CampaignJourneyAdvisoryValidator.Validate(campaign, materialized).ToList();
        var summary = BuildSummary(campaignDto, campaign, errors, warnings);
        var nextSteps = CampaignValidationNextStepsBuilder.Build(errors, warnings, summary);

        return new CampaignValidationResultDto
        {
            IsValid = errors.Count == 0,
            Validation = new CampaignValidationDetailsDto
            {
                Errors = errors,
                Warnings = warnings
            },
            Summary = summary,
            NextSteps = nextSteps
        };
    }

    private static void RunShellValidation(Campaign campaign, List<CampaignValidationFindingDto> errors)
    {
        TryCollect(() => CampaignShellValidator.ValidateStatus(campaign.Status), errors);
        TryCollect(() => CampaignShellValidator.ValidateRequiredFields(campaign), errors);
        TryCollect(() => CampaignShellValidator.ValidateDateRange(campaign), errors);
    }

    private static void RunJsonPreValidators(CampaignDto campaignDto, List<CampaignValidationFindingDto> errors)
    {
        var json = JsonSerializer.Serialize(campaignDto, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        TryCollect(() => CampaignJourneyAuthoringShapeValidator.ValidateCampaignJson(root), errors);
        TryCollect(() => CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(root), errors);
        TryCollect(() => CampaignJourneyNavigationValidator.ValidateCampaignJson(root), errors);
    }

    private async Task<bool> RunDefinitionValidationStepsAsync(
        string tenantId,
        Campaign campaign,
        List<CampaignValidationFindingDto> errors,
        CancellationToken cancellationToken)
    {
        if (campaign.Journey == null)
            return false;

        var materialized = false;

        TryCollect(() => CampaignJourneyAuthoringShapeValidator.ValidateCampaign(campaign), errors);
        TryCollect(() => CampaignJourneyPolymorphicMetadataValidator.ValidateCampaign(campaign), errors);
        TryCollect(() => CampaignJourneyNavigationValidator.ValidateCampaign(campaign), errors);

        var skipMaterialize = errors.Exists(e =>
            string.Equals(e.Code, JourneyRuleShapeRules.ViolationCode, StringComparison.Ordinal)
            || (e.Field?.StartsWith("journey.shape.", StringComparison.OrdinalIgnoreCase) ?? false));

        if (!skipMaterialize)
        {
            try
            {
                CampaignJourneyMaterializer.Materialize(campaign);
                materialized = true;
            }
            catch (Exception ex) when (CampaignJourneyMaterializeValidationHelper.IsJsonMaterializationFailure(ex))
            {
                errors.Add(CampaignValidationErrorMapper.MapEntry(
                    "journey.materialize",
                    CampaignJourneyMaterializeValidationHelper.BuildMaterializeMessage(ex)));
            }
        }

        if (materialized)
            TryCollect(() => CampaignJourneyStructureValidator.Validate(campaign), errors);

        if (materialized)
        {
            try
            {
                await _pointOutcomePatValidator.ValidateAsync(tenantId, campaign, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (APIErrorsException ex)
            {
                CampaignValidationErrorMapper.AddFromException(ex, errors);
            }
        }

        if (materialized)
        {
            try
            {
                await _taxonomyRuleValidator.ValidateAsync(tenantId, campaign, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (APIErrorsException ex)
            {
                CampaignValidationErrorMapper.AddFromException(ex, errors);
            }
        }

        return materialized;
    }

    private static void TryCollect(Action action, List<CampaignValidationFindingDto> errors)
    {
        try
        {
            action();
        }
        catch (APIErrorsException ex)
        {
            CampaignValidationErrorMapper.AddFromException(ex, errors);
        }
    }

    private static CampaignValidationSummaryDto BuildSummary(
        CampaignDto campaignDto,
        Campaign? campaign,
        IReadOnlyList<CampaignValidationFindingDto> errors,
        IReadOnlyList<CampaignValidationFindingDto> warnings)
    {
        var journeyNodeCount = 0;
        var ruleSetCount = 0;
        var hasJourney = campaign?.Journey != null || campaignDto.Journey != null;

        if (campaign?.Journey != null)
            CountJourney(campaign.Journey, ref journeyNodeCount, ref ruleSetCount);
        else if (campaignDto.Journey != null)
            CountJourneyDto(campaignDto.Journey, ref journeyNodeCount, ref ruleSetCount);

        return new CampaignValidationSummaryDto
        {
            ErrorCount = errors.Count,
            WarningCount = warnings.Count,
            JourneyNodeCount = journeyNodeCount,
            RuleSetCount = ruleSetCount,
            HasJourney = hasJourney,
            JourneyDeliveryReady = !hasJourney || ruleSetCount > 0,
            Status = campaignDto.Status,
            EventModelCount = campaignDto.Events?.Count(e => !string.IsNullOrWhiteSpace(e)) ?? 0
        };
    }

    private static void CountJourney(JourneyNode node, ref int nodeCount, ref int ruleSetCount)
    {
        nodeCount++;
        if (node.Rules != null)
            ruleSetCount += node.Rules.Count(r => r != null && CampaignJourneyAuthoringShapeValidator.HasRuleSetPayload(r));

        if (node.Children == null)
            return;

        foreach (var child in node.Children)
        {
            if (child != null)
                CountJourney(child, ref nodeCount, ref ruleSetCount);
        }
    }

    private static void CountJourneyDto(JourneyDto node, ref int nodeCount, ref int ruleSetCount)
    {
        nodeCount++;
        if (node.Rules != null)
            ruleSetCount += node.Rules.Count(r => r != null && CampaignJourneyAuthoringShapeValidator.HasRuleSetDtoPayload(r));

        if (node.Children == null)
            return;

        foreach (var child in node.Children)
        {
            if (child != null)
                CountJourneyDto(child, ref nodeCount, ref ruleSetCount);
        }
    }
}
