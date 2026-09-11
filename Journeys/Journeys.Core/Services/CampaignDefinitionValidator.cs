using System.Collections.Generic;
using Journeys.Core.Models;
using Journeys.DTO.Exceptions;

namespace Journeys.Core.Services;

/// <summary>
/// Tier A upsert validation: materialize the journey graph (force JSON deserialization), then run structure and taxonomy validators.
/// </summary>
public sealed class CampaignDefinitionValidator
{
    private readonly CampaignTaxonomyRuleValidator _taxonomyRuleValidator;
    private readonly CampaignJourneyPointOutcomePatValidator _pointOutcomePatValidator;

    public CampaignDefinitionValidator(
        CampaignTaxonomyRuleValidator taxonomyRuleValidator,
        CampaignJourneyPointOutcomePatValidator pointOutcomePatValidator)
    {
        _taxonomyRuleValidator = taxonomyRuleValidator;
        _pointOutcomePatValidator = pointOutcomePatValidator;
    }

    public async Task ValidateAsync(string tenantId, Campaign campaign, CancellationToken cancellationToken = default)
    {
        if (campaign?.Journey == null)
            return;

        CampaignJourneyAuthoringShapeValidator.ValidateCampaign(campaign);
        CampaignJourneyPolymorphicMetadataValidator.ValidateCampaign(campaign);
        CampaignJourneyNavigationValidator.ValidateCampaign(campaign);

        try
        {
            CampaignJourneyMaterializer.Materialize(campaign);
        }
        catch (Exception ex) when (CampaignJourneyMaterializeValidationHelper.IsJsonMaterializationFailure(ex))
        {
            throw CampaignJourneyMaterializeValidationHelper.ToMaterializeException(ex);
        }

        CampaignJourneyStructureValidator.Validate(campaign);

        await _pointOutcomePatValidator.ValidateAsync(tenantId, campaign, cancellationToken).ConfigureAwait(false);

        await _taxonomyRuleValidator.ValidateAsync(tenantId, campaign, cancellationToken).ConfigureAwait(false);
    }
}
