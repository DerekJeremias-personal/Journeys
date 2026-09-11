using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.Services;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Journeys.Infra.Backend;
using ModelContextProtocol.Server;

namespace Journeys.API.Mcp;

/// <summary>
/// MCP tools exposing Journeys campaign, journey, account, event, ingestion, point types, and tier operations for agent use.
/// </summary>
[McpServerToolType]
public class JourneysMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions ContractSummaryJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string SerializeApiErrors(APIErrorsException ex) =>
        JsonSerializer.Serialize(new { errors = ex.Errors }, JsonOptions);

    private static string SerializeToolException(string errorKey, Exception ex) =>
        JsonSerializer.Serialize(new
        {
            errors = new Dictionary<string, string>
            {
                [errorKey] = $"{ex.GetType().Name}: {ex.Message}"
            }
        }, JsonOptions);

    private static string SerializeBackendValidationErrors(BackendValidationException ex) =>
        JsonSerializer.Serialize(new
        {
            errors = ex.ValidationErrors,
            message = ex.Message,
            errorCode = ex.ErrorCode
        }, JsonOptions);

    /// <summary>
    /// Parses and pre-validates campaign JSON. Returns structured <c>{ errors }</c> JSON on failure
    /// instead of throwing, so MCP does not replace the message with an opaque invocation error.
    /// </summary>
    private static string? TryParseCampaignJson(string campaignJson, out CampaignDto? campaign)
    {
        campaign = null;

        try
        {
            using var shapeDoc = JsonDocument.Parse(campaignJson);
            CampaignJourneyAuthoringShapeValidator.ValidateCampaignJson(shapeDoc.RootElement);
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(shapeDoc.RootElement);
            CampaignJourneyNavigationValidator.ValidateCampaignJson(shapeDoc.RootElement);
        }
        catch (JsonException ex)
        {
            return SerializeToolException("campaignJson", ex);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }

        try
        {
            campaign = JsonSerializer.Deserialize<CampaignDto>(campaignJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return SerializeToolException("campaignJson", ex);
        }

        if (campaign == null)
        {
            return SerializeToolException(
                "campaignJson",
                new ArgumentException("campaignJson did not deserialize to a campaign."));
        }

        return null;
    }

    private readonly ICampaignService _campaignService;
    private readonly ICampaignAssistantContextService _campaignAssistantContextService;
    private readonly ILoyaltyAccountService _accountService;
    private readonly IEventService _eventService;
    private readonly IFileIngestionService _ingestionService;
    private readonly IRulesService _rulesService;

    public JourneysMcpTools(
        ICampaignService campaignService,
        ICampaignAssistantContextService campaignAssistantContextService,
        ILoyaltyAccountService accountService,
        IEventService eventService,
        IFileIngestionService ingestionService,
        IRulesService rulesService)
    {
        _campaignService = campaignService;
        _campaignAssistantContextService = campaignAssistantContextService;
        _accountService = accountService;
        _eventService = eventService;
        _ingestionService = ingestionService;
        _rulesService = rulesService;
    }

    [McpServerTool, Description("Gets a campaign by tenant ID, campaign ID, and optional status (default: Live). During in-session authoring, use status=Draft.")]
    public async Task<string> GetCampaign(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The campaign identifier.")] string campaignId,
        [Description("Campaign status: Live, Draft, or Archive. Defaults to Live.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("tenantId and campaignId are required.");

        status ??= CampaignStatusStrings.Live;
        try
        {
            var campaign = await _campaignService.FetchCampaignAsync(tenantId, campaignId, status);
            if (campaign == null)
                return $"Campaign not found: tenantId={tenantId}, campaignId={campaignId}, status={status}";

            return JsonSerializer.Serialize(campaign, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description(
        "Returns heavy assistant context for a saved campaign: allowed ModelAttributeDto type discriminators, per–event-model attribute rows (symbols, data types, Live flags), optional JSON sample scaffold under event.*. " +
        "Use after UpsertCampaign or GetCampaign when authoring or validating sample payloads. Same data as HTTP GET .../assistant-context.")]
    public async Task<string> GetCampaignAssistantContext(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The campaign identifier (same as GetCampaign).")] string campaignId,
        [Description("Campaign status: Live, Draft, or Archive. Defaults to Live.")] string? status = null,
        [Description("When true (default), include SampleScaffold.JsonTemplate and RequiredEventSymbols from Live attributes.")] bool includeSampleTemplate = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("tenantId and campaignId are required.");

        status ??= CampaignStatusStrings.Live;
        var ctx = await _campaignAssistantContextService
            .GetContextAsync(tenantId, campaignId, status, includeSampleTemplate, cancellationToken)
            .ConfigureAwait(false);
        if (ctx == null)
            return $"Campaign not found: tenantId={tenantId}, campaignId={campaignId}, status={status}";

        return JsonSerializer.Serialize(ctx, JsonOptions);
    }

    [McpServerTool, Description(
        "Returns JSON for the Tier A rules-engine campaign contract: rule/outcome/provider Kind lists, evaluator $type names, enumCatalog (valid enum strings), critical required JSON fields, and Tier A violation codes. " +
        "No tenant state. Use before UpsertCampaign or when fixing journey.validation / journey.materialize / [violation=...] errors. " +
        "Equivalent to MCP resource journeys://rules-engine/campaign-contract/v1.")]
    public Task<string> GetRulesEngineContractSummary(CancellationToken cancellationToken = default)
    {
        var dto = RulesEngineMcpContractSummary.Build();
        return Task.FromResult(JsonSerializer.Serialize(dto, ContractSummaryJsonOptions));
    }

    [McpServerTool(ReadOnly = true, Name = "get_rule_pattern_recipes"), Description(
        "Returns pattern recipes (A–F) for journey rule authoring: when to use HistoricalRule vs PointBalanceProvider, minimal JSON skeletons, anti-patterns, and rule-kind semantics. " +
        "No tenant state. Call once per journey authoring episode before building HistoricalRule, tier navigation, or composite trees. " +
        "Equivalent to MCP resource journeys://rules-engine/pattern-recipes/v1.")]
    public Task<string> GetRulePatternRecipes(CancellationToken cancellationToken = default)
    {
        var dto = RulesEnginePatternRecipes.Build();
        return Task.FromResult(JsonSerializer.Serialize(dto, ContractSummaryJsonOptions));
    }

    [McpServerTool, Description("Gets a campaign by tenant and external campaign ID. Use status Live or Draft.")]
    public async Task<string> GetCampaignByExtId(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The external campaign identifier.")] string extCampaignId,
        [Description("Campaign status: Live or Draft. Defaults to Live.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(extCampaignId))
            throw new ArgumentException("tenantId and extCampaignId are required.");

        status ??= CampaignStatusStrings.Live;
        try
        {
            var campaign = status == CampaignStatusStrings.Draft
                ? await _campaignService.GetDraftCampaignByExtIdAsync(tenantId, extCampaignId)
                : await _campaignService.GetLiveCampaignByExtIdAsync(tenantId, extCampaignId);
            if (campaign == null)
                return $"Campaign not found: tenantId={tenantId}, extCampaignId={extCampaignId}, status={status}";

            return JsonSerializer.Serialize(campaign, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description("Lists all versions (Live, Draft, Archive) of a campaign by external campaign ID.")]
    public async Task<string> ListCampaignVersions(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The external campaign identifier.")] string extCampaignId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(extCampaignId))
            throw new ArgumentException("tenantId and extCampaignId are required.");

        try
        {
            var versions = await _campaignService.GetCampaignVersionsByExtIdAsync(tenantId, extCampaignId);
            return JsonSerializer.Serialize(versions ?? new List<CampaignDto>(), JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description("Gets only the segments for a campaign (convenience when full campaign is not needed).")]
    public async Task<string> GetSegmentsFromCampaign(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The campaign identifier.")] string campaignId,
        [Description("Campaign status: Live, Draft, or Archive. Defaults to Live.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("tenantId and campaignId are required.");

        status ??= CampaignStatusStrings.Live;
        try
        {
            var campaign = await _campaignService.FetchCampaignAsync(tenantId, campaignId, status);
            if (campaign == null)
                return $"Campaign not found: tenantId={tenantId}, campaignId={campaignId}, status={status}";

            return JsonSerializer.Serialize(campaign.Segments ?? new List<SegmentDto>(), JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description("Lists campaigns for a tenant by status (Live, Draft, Archive). Returns a paged list with optional continuation token.")]
    public async Task<string> ListCampaigns(
        [Description("The tenant identifier.")] string tenantId,
        [Description("Campaign status: Live, Draft, or Archive. Defaults to Live.")] string? status = null,
        [Description("Page size (1-100). Default 20.")] int pageSize = 20,
        [Description("Optional continuation token from a previous list call.")] string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("tenantId is required.");

        status ??= CampaignStatusStrings.Live;
        pageSize = Math.Clamp(pageSize, 1, 100);
        try
        {
            var result = await _campaignService.GetCampaignsByStatusAsync(tenantId, status, pageSize, continuationToken);
            if (result?.Entities == null)
                return JsonSerializer.Serialize(new { Count = 0, Entities = Array.Empty<object>(), ContinuationToken = (string?)null }, JsonOptions);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description("Gets the journey definition for a campaign (structure, nodes, navigation). Returns the Journey property of the campaign.")]
    public async Task<string> GetJourneyFromCampaign(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The campaign identifier.")] string campaignId,
        [Description("Campaign status: Live, Draft, or Archive. Defaults to Live.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("tenantId and campaignId are required.");

        status ??= CampaignStatusStrings.Live;
        try
        {
            var campaign = await _campaignService.FetchCampaignAsync(tenantId, campaignId, status);
            if (campaign == null)
                return $"Campaign not found: tenantId={tenantId}, campaignId={campaignId}, status={status}";
            if (campaign.Journey == null)
                return $"Campaign has no journey: tenantId={tenantId}, campaignId={campaignId}";

            return JsonSerializer.Serialize(campaign.Journey, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description(
        "Gets a loyalty account by tenant ID and account ID. accountId may be the internal loyalty account id (GUID) " +
        "or, when no GUID match exists, an external account id (extaccountId) used in ProcessEvent account-link fields.")]
    public async Task<string> GetAccount(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The loyalty account id (GUID) or external account id (extaccountId).")] string accountId,
        [Description("If true, resettle account state if needed. Default false.")] bool resettleIfNeeded = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("tenantId and accountId are required.");

        try
        {
            var account = await _accountService.GetLoyaltyAccountAsync(tenantId, accountId, resettleIfNeeded);
            if (account == null)
                account = await _accountService.GetLoyaltyAccountByExtIdAsync(tenantId, accountId, resettleIfNeeded: resettleIfNeeded);
            if (account == null)
                return $"Account not found: tenantId={tenantId}, accountId={accountId}";

            return JsonSerializer.Serialize(account, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
        catch (Exception ex)
        {
            return SerializeToolException("getAccount", ex);
        }
    }

    [McpServerTool, Description("Gets campaign statistics for a campaign (e.g. status).")]
    public async Task<string> GetCampaignStats(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The campaign identifier.")] string campaignId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("tenantId and campaignId are required.");

        try
        {
            var stats = await _campaignService.GetCampaignStatsAsync(tenantId, campaignId);
            return JsonSerializer.Serialize(stats ?? new CampaignStatisticsDto(), JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description("Gets a point account type by tenant and ID. Returns expiresToPointAccountTypeId, pointsLifespanDays, pointsLifespanEndDate, rounding fields for cascade and manifest wiring.")]
    public async Task<string> GetPointAccountType(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The point account type identifier.")] string pointAccountTypeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(pointAccountTypeId))
            throw new ArgumentException("tenantId and pointAccountTypeId are required.");

        try
        {
            var pat = await _campaignService.FetchPointAccountType(tenantId, pointAccountTypeId);
            if (pat == null)
                return $"Point account type not found: tenantId={tenantId}, pointAccountTypeId={pointAccountTypeId}";

            return JsonSerializer.Serialize(pat, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description("Lists point account types for a tenant (paged). Returns expiresToPointAccountTypeId, pointsLifespanDays, pointsLifespanEndDate, rounding fields for cascade and manifest wiring.")]
    public async Task<string> ListPointAccountTypes(
        [Description("The tenant identifier.")] string tenantId,
        [Description("Page size (1-100). Default 20.")] int pageSize = 20,
        [Description("Optional continuation token from a previous list call.")] string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("tenantId is required.");

        pageSize = Math.Clamp(pageSize, 1, 100);
        try
        {
            var result = await _campaignService.GetAllPointAccountTypesAsync(tenantId, pageSize, continuationToken ?? string.Empty);
            if (result?.Entities == null)
                return JsonSerializer.Serialize(new { Count = 0, Entities = Array.Empty<PointAccountTypeDto>(), ContinuationToken = (string?)null }, JsonOptions);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description("Preview the effect of moving an account to a tier (qualification/spendable balances, demotion, validation). Read-only; does not change state.")]
    public async Task<string> PreviewTierMove(
        [Description("The tenant identifier.")] string tenantId,
        [Description("MoveTierRequest as JSON: LoyaltyAccountId or LoyaltyAccountXReference, TargetCampaignId, TargetJourneyId, Comment (optional), AdminUserId (optional).")] string requestJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(requestJson))
            throw new ArgumentException("tenantId and requestJson are required.");

        MoveTierRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<MoveTierRequest>(requestJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid request JSON: {ex.Message}", ex);
        }

        if (request == null)
            throw new ArgumentException("requestJson did not deserialize to a MoveTierRequest.");

        try
        {
            var response = await _rulesService.PreviewTierMoveAsync(tenantId, request, cancellationToken);
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description("Moves an account to a tier. Mutating; use only when explicitly allowed (e.g. admin operations). Requires AdminUserId in request.")]
    public async Task<string> MoveTier(
        [Description("The tenant identifier.")] string tenantId,
        [Description("MoveTierRequest as JSON: LoyaltyAccountId or LoyaltyAccountXReference, TargetCampaignId, TargetJourneyId, Comment (optional), AdminUserId.")] string requestJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(requestJson))
            throw new ArgumentException("tenantId and requestJson are required.");

        MoveTierRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<MoveTierRequest>(requestJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid request JSON: {ex.Message}", ex);
        }

        if (request == null)
            throw new ArgumentException("requestJson did not deserialize to a MoveTierRequest.");

        try
        {
            var response = await _rulesService.MoveTierAsync(tenantId, request, cancellationToken);
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool(ReadOnly = true, Name = "validate_campaign"), Description(
        "Validates a campaign structure without persisting. Pass the campaign as JSON (CampaignDto). " +
        "Returns hard errors (same as save would reject), advisory warnings, and nextSteps coaching. " +
        "Prefer validate_campaign before upsert_campaign when journey JSON changed in the same turn.")]
    public async Task<string> ValidateCampaign(
        [Description("The tenant identifier.")] string tenantId,
        [Description("Campaign JSON (CampaignDto) to validate.")] string campaignJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return SerializeToolException("tenantId", new ArgumentException("tenantId is required."));
        if (string.IsNullOrWhiteSpace(campaignJson))
            return SerializeToolException("campaignJson", new ArgumentException("campaignJson is required."));

        var parseError = TryParseCampaignJson(campaignJson, out var campaign);
        if (parseError != null)
            return parseError;

        try
        {
            var result = await _campaignService.ValidateCampaignAsync(tenantId, campaign!, cancellationToken);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
        catch (Exception ex)
        {
            return SerializeToolException("validateCampaign", ex);
        }
    }

    [McpServerTool, Description(
        "Creates or updates a campaign. Pass the campaign as JSON (CampaignDto). Requires tenantId and valid campaign payload. " +
        "Journey rule trees, outcomes, and navigation must use correct polymorphic metadata: Kind on RuleBase/OutcomeBase roots and nested rules; $type on IValueProvider, IEvaluatable, INavigationCriteria, and IHistoricalValueProvider objects (see campaign agent governance). " +
        "Unless the user explicitly asked for Live, use status Draft for new campaigns; promote to Live only after explicit user approval. During in-session authoring, use status=Draft. Each id in Campaign.Events must be a complete event payload model with wrapper linked in modelMetaData (see governance). " +
        "startDate and endDate in campaignJson must be ISO-8601 UTC; default startDate to start of current UTC day and omit endDate unless the user wants an end—confirm UTC and local equivalents with the user per governance. " +
        "Call at most once per user request in a single assistant turn after you have gathered context; include the full intended campaign in campaignJson. " +
        "Do not invoke again after a successful save in the same turn.")]
    public async Task<string> UpsertCampaign(
        [Description("The tenant identifier.")] string tenantId,
        [Description("Campaign JSON (CampaignDto): include status (prefer Draft for new work); startDate/endDate as UTC ISO-8601; Campaign.Events ids must reference event+wrapper models per governance; journey rules use Kind vs $type per merged campaign governance.")] string campaignJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return SerializeToolException("tenantId", new ArgumentException("tenantId is required."));
        if (string.IsNullOrWhiteSpace(campaignJson))
            return SerializeToolException("campaignJson", new ArgumentException("campaignJson is required."));

        var parseError = TryParseCampaignJson(campaignJson, out var campaign);
        if (parseError != null)
            return parseError;

        try
        {
            var saved = await _campaignService.UpsertCampaignAsync(tenantId, campaign!);
            return JsonSerializer.Serialize(saved, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
        catch (Exception ex)
        {
            return SerializeToolException("upsertCampaign", ex);
        }
    }

    [McpServerTool, Description(
        "Deletes a campaign by tenant, campaign ID, and status (Live, Draft, or Archive). " +
        "Mutating and irreversible for that campaign version; use only when the user explicitly asked to delete it. " +
        "Use the same status as the row you are removing (as with GetCampaign).")]
    public async Task<string> DeleteCampaign(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The campaign identifier.")] string campaignId,
        [Description("Campaign status: Live, Draft, or Archive. Defaults to Live.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("tenantId and campaignId are required.");

        status ??= CampaignStatusStrings.Live;

        try
        {
            await _campaignService.DeleteCampaignAsync(tenantId, campaignId, status);
            return JsonSerializer.Serialize(new { deleted = true, tenantId, campaignId, status }, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description(
        "Creates or updates a point account type. Required: name, status, ledgerType (Escrow | Spendable | Expired | NonSpendable | Archive). " +
        "Tier-qualification counters: NonSpendable + isSpendable false — not TierQualification. " +
        "Prefer rolling expiration: pointsLifespanDays with null pointsLifespanEndDate. " +
        "When expiring/cascading, set expiresToPointAccountTypeId to destination PAT id (create destination PAT first). " +
        "isSpendable true only with Spendable ledger. Equivalent to HTTP POST api/Campaign/{tenantId}/pointaccounttype/upsert.")]
    public async Task<string> UpsertPointAccountType(
        [Description("The tenant identifier.")] string tenantId,
        [Description("Point account type JSON (PointAccountTypeDto).")] string pointAccountTypeJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("tenantId is required.");
        if (string.IsNullOrWhiteSpace(pointAccountTypeJson))
            throw new ArgumentException("pointAccountTypeJson is required.");

        PointAccountTypeDto? pat;
        try
        {
            pat = JsonSerializer.Deserialize<PointAccountTypeDto>(pointAccountTypeJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid point account type JSON: {ex.Message}", ex);
        }

        if (pat == null)
            throw new ArgumentException("pointAccountTypeJson did not deserialize to a PointAccountTypeDto.");

        try
        {
            var saved = await _campaignService.UpsertPointAccountTypeAsync(tenantId, pat);
            return JsonSerializer.Serialize(saved, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
    }

    [McpServerTool, Description(
        "Processes an event through the loyalty engine (event type + payload as JSON). For Draft campaign verification, pass campaignId and use a tenant-allowlisted test account; evaluates that Draft exclusively. Mutates real loyalty state. " +
        "Omit id/Id everywhere in the JSON (root and each nested object and each list item, e.g. every Item in items); use business-key symbols from GetModel for the event and child models; align root keys with NaturalKeySymbols.")]
    public async Task<string> ProcessEvent(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The event model/schema name (e.g. order, registration).")] string modelName,
        [Description("Inner event payload JSON; no id/Id on root or inside arrays/objects (line items use line-model symbols only, e.g. sku).")] string eventJson,
        [Description("If true, reprocess an existing event. Default false.")] bool reprocessEvent = false,
        [Description("Optional Draft campaign id for verification on tenant-allowlisted test accounts.")] string? campaignId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(eventJson))
            throw new ArgumentException("tenantId, modelName, and eventJson are required.");

        JsonElement data;
        try
        {
            using var doc = JsonDocument.Parse(eventJson);
            data = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid event JSON: {ex.Message}", ex);
        }

        try
        {
            var result = await _eventService.ProcessEventAsync(tenantId, modelName, data, cancellationToken, reprocessEvent, campaignId);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (APIErrorsException ex)
        {
            return SerializeApiErrors(ex);
        }
        catch (BackendValidationException ex)
        {
            return SerializeBackendValidationErrors(ex);
        }
        catch (Exception ex)
        {
            return SerializeToolException("processEvent", ex);
        }
    }

    [McpServerTool, Description("Lists ingestion folder names for a tenant.")]
    public async Task<string> GetIngestionFolders(
        [Description("The tenant identifier.")] string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("tenantId is required.");

        var folders = await _ingestionService.GetFolders(tenantId);
        return JsonSerializer.Serialize(folders ?? new List<string>(), JsonOptions);
    }

    [McpServerTool, Description("Gets ingestion chunk summary for a file (by tenant, folder, and file name).")]
    public async Task<string> GetIngestionSummary(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The folder name.")] string folderName,
        [Description("The file name.")] string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("tenantId, folderName, and fileName are required.");

        var summary = await _ingestionService.GetChunksSummaryByFileName(tenantId, folderName, fileName);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }
}
