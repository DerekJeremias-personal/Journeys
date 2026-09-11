using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;
using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Microsoft.Extensions.Options;

namespace Journeys.Core.Services;

public sealed class CampaignAssistantContextService : ICampaignAssistantContextService
{
    private const int MaxAttributesPerModel = 250;

    private readonly ICampaignAdapter _campaignAdapter;
    private readonly IModelAdapter _modelAdapter;
    private readonly ICampaignService _campaignService;
    private readonly IOptions<CampaignUpsertValidationOptions> _options;

    public CampaignAssistantContextService(
        ICampaignAdapter campaignAdapter,
        IModelAdapter modelAdapter,
        ICampaignService campaignService,
        IOptions<CampaignUpsertValidationOptions> options)
    {
        _campaignAdapter = campaignAdapter;
        _modelAdapter = modelAdapter;
        _campaignService = campaignService;
        _options = options;
    }

    public async Task<CampaignAssistantContextDto?> GetContextAsync(
        string tenantId,
        string campaignId,
        string? status,
        bool includeSampleTemplate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            return null;

        status ??= CampaignStatusStrings.Live;
        var campaign = await _campaignAdapter.FetchCampaignAsync(tenantId, campaignId, status).ConfigureAwait(false);
        if (campaign == null)
            return null;

        var ctx = new CampaignAssistantContextDto
        {
            TenantId = tenantId,
            CampaignId = campaign.Id ?? campaignId,
            Status = campaign.Status ?? status,
            AllowedModelAttributeTypes = ModelAttributeDtoTypeDiscriminators.All
        };

        var warnings = new List<string>();

        ctx.CampaignShell = CampaignShellDigestBuilder.BuildFromCampaign(campaign);

        var walk = JourneyDigestWalkUtility.Walk(campaign.Journey);
        var patIds = walk.ReferencedPatIds.Keys.ToList();
        var patDtos = new List<PointAccountTypeDto>();
        foreach (var id in patIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            try
            {
                var pat = await _campaignService.FetchPointAccountType(tenantId, id).ConfigureAwait(false);
                if (pat != null) patDtos.Add(pat);
            }
            catch
            {
                warnings.Add($"Could not load PAT {id} for assistant context.");
            }
        }

        ctx.PointAccountManifest = PointAccountManifestBuilder.BuildFromPatDtos(patDtos);
        var manifestIds = ctx.PointAccountManifest.Items.Select(i => i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ctx.Journey = CampaignJourneyArtifactDigestBuilder.BuildFromCampaign(campaign, manifestIds);

        var requiredSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (campaign.Events == null || campaign.Events.Count == 0)
        {
            warnings.Add("Campaign.Events is empty; no event model symbols to scaffold.");
        }
        else
        {
            foreach (var rawId in campaign.Events)
            {
                if (string.IsNullOrWhiteSpace(rawId))
                    continue;
                var id = rawId.Trim();
                var (loaded, resolvedType) = await TryLoadEventModelAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
                var emCtx = new EventModelAssistantContextDto { ModelId = id, ModelType = resolvedType };
                if (loaded == null)
                {
                    emCtx.LoadWarning =
                        $"Model {id} could not be loaded with any configured EventModelTypeCandidates; check tenant and model id.";
                    warnings.Add(emCtx.LoadWarning);
                    ctx.EventModels.Add(emCtx);
                    continue;
                }

                emCtx.Name = loaded.Name;
                emCtx.ModelType ??= loaded.ModelType;

                var contract = EventProcessingContractBuilder.BuildFromModel(loaded);
                emCtx.ProcessingContract = contract;
                foreach (var w in contract.Warnings)
                    warnings.Add($"Model {id}: {w}");

                if (!contract.IsProcessEventEligible)
                {
                    warnings.Add(
                        $"Model {id}: tag is not 'eventable'; not valid for ProcessEvent or Campaign.Events.");
                }

                if (string.Equals(
                        contract.ProcessingRole,
                        EventModelEligibilityValidation.RoleLoyaltyAccountCreation,
                        StringComparison.Ordinal))
                {
                    warnings.Add(
                        $"Model {id}: loyalty account creation event — do not use as default Verification fixture.");
                }

                ctx.EventModels.Add(emCtx);

                var attrs = loaded.Attributes;
                if (attrs == null)
                    continue;

                var n = 0;
                foreach (var attr in attrs)
                {
                    if (attr == null || string.IsNullOrWhiteSpace(attr.Symbol))
                        continue;
                    if (++n > MaxAttributesPerModel)
                    {
                        warnings.Add($"Model {id}: attribute list truncated at {MaxAttributesPerModel} for assistant context.");
                        break;
                    }

                    var live = string.Equals(attr.Status, "live", StringComparison.OrdinalIgnoreCase);
                    var row = new AttributeSymbolRowDto
                    {
                        Symbol = attr.Symbol.Trim(),
                        DataType = attr.DataType ?? string.Empty,
                        AttributeType = attr.Type,
                        Status = attr.Status ?? string.Empty,
                        IncludeInSampleTemplate = live,
                        RestrictionSummary = SummarizeRestrictions(attr)
                    };
                    emCtx.Attributes.Add(row);
                    if (live)
                        requiredSymbols.Add(row.Symbol);
                }
            }
        }

        if (campaign.Events != null && campaign.Events.Count > 1)
            warnings.Add("Multiple event models: sample scaffold merges Live symbols across models; validate each payload shape.");

        if (ctx.CampaignShell != null)
        {
            ctx.CampaignShell.IneligibleEventModelIds = ctx.EventModels
                .Where(e => e.ProcessingContract != null && !e.ProcessingContract.IsProcessEventEligible)
                .Select(e => e.ModelId)
                .ToList();
        }

        if (includeSampleTemplate)
        {
            var scaffoldSymbols = new HashSet<string>(requiredSymbols, StringComparer.OrdinalIgnoreCase);
            var fieldRoles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var em in ctx.EventModels)
            {
                var pc = em.ProcessingContract;
                if (pc?.AccountLink?.SymbolPath is { Length: > 0 } accountPath)
                {
                    scaffoldSymbols.Add(accountPath);
                    fieldRoles[accountPath] = "accountLink";
                }
                if (pc?.NaturalKey?.Symbols != null)
                {
                    foreach (var nk in pc.NaturalKey.Symbols)
                    {
                        if (!string.IsNullOrWhiteSpace(nk))
                        {
                            scaffoldSymbols.Add(nk);
                            fieldRoles[nk] = "naturalKey";
                        }
                    }
                }
            }

            foreach (var sym in scaffoldSymbols)
            {
                if (!fieldRoles.ContainsKey(sym))
                    fieldRoles[sym] = "attribute";
            }

            if (scaffoldSymbols.Count > 0)
            {
                var root = scaffoldSymbols
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(s => s, _ => (object?)null);

                ctx.SampleScaffold = new SamplePayloadScaffoldDto
                {
                    RequiredEventSymbols = root.Keys.ToList(),
                    JsonTemplate = JsonSerializer.Serialize(root),
                    AccountLinkSymbolPath = ctx.EventModels
                        .Select(e => e.ProcessingContract?.AccountLink?.SymbolPath)
                        .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
                    NaturalKeySymbols = ctx.EventModels
                        .SelectMany(e => e.ProcessingContract?.NaturalKey?.Symbols ?? [])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    FieldRoles = fieldRoles
                };
            }
        }

        ctx.Warnings = warnings;
        return ctx;
    }

    private async Task<(ModelDto? Model, string? ResolvedType)> TryLoadEventModelAsync(
        string tenantId,
        string modelId,
        CancellationToken cancellationToken)
    {
        foreach (var modelType in _options.Value.EventModelTypeCandidates ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(modelType))
                continue;
            var loaded = await _modelAdapter
                .GetModelAsync(tenantId, modelId, modelType, false, cancellationToken)
                .ConfigureAwait(false);
            if (loaded != null)
                return (loaded, modelType);
        }

        return (null, null);
    }

    private static string? SummarizeRestrictions(ModelAttributeDto attr)
    {
        var r = attr.Restrictions;
        if (r == null || r.Count == 0)
            return null;
        return $"{r.Count} restriction(s)";
    }
}
