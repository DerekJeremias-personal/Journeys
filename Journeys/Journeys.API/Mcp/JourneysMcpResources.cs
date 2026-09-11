using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.DTO.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Journeys.API.Mcp;

/// <summary>
/// MCP resources exposing Journeys data (campaigns, accounts, etc.) for agent context.
/// </summary>
[McpServerResourceType]
public class JourneysMcpResources
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly JsonSerializerOptions ContractSummaryJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ICampaignService _campaignService;
    private readonly ILoyaltyAccountService _accountService;

    public JourneysMcpResources(ICampaignService campaignService, ILoyaltyAccountService accountService)
    {
        _campaignService = campaignService;
        _accountService = accountService;
    }

    [McpServerResource(
        UriTemplate = "journeys://rules-engine/campaign-contract/v1",
        Name = "Rules engine campaign contract (Tier A)",
        MimeType = "application/json")]
    [Description("Static JSON contract: rule/outcome kinds, Tier A required fields and violation codes (same as GetRulesEngineContractSummary).")]
    public Task<ResourceContents> GetRulesEngineCampaignContract(
        CancellationToken cancellationToken = default)
    {
        var dto = RulesEngineMcpContractSummary.Build();
        var json = JsonSerializer.Serialize(dto, ContractSummaryJsonOptions);
        return Task.FromResult<ResourceContents>(new TextResourceContents
        {
            Uri = "journeys://rules-engine/campaign-contract/v1",
            MimeType = "application/json",
            Text = json
        });
    }

    [McpServerResource(
        UriTemplate = "journeys://rules-engine/pattern-recipes/v1",
        Name = "Rules engine pattern recipes (A–F)",
        MimeType = "application/json")]
    [Description("Static JSON pattern recipes for journey rule authoring (same as GetRulePatternRecipes): when to use HistoricalRule vs PointBalanceProvider, minimal skeletons, anti-patterns, ruleSemantics.")]
    public Task<ResourceContents> GetRulesEnginePatternRecipes(
        CancellationToken cancellationToken = default)
    {
        var dto = RulesEnginePatternRecipes.Build();
        var json = JsonSerializer.Serialize(dto, ContractSummaryJsonOptions);
        return Task.FromResult<ResourceContents>(new TextResourceContents
        {
            Uri = "journeys://rules-engine/pattern-recipes/v1",
            MimeType = "application/json",
            Text = json
        });
    }

    [McpServerResource(
        UriTemplate = "journeys://campaign/{tenantId}/{campaignId}",
        Name = "Journeys Campaign",
        MimeType = "application/json")]
    [Description("Returns a campaign by tenant and campaign ID (Live status).")]
    public async Task<ResourceContents> GetCampaign(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The campaign identifier.")] string campaignId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("tenantId and campaignId are required.");

        var campaign = await _campaignService.FetchCampaignAsync(tenantId, campaignId, CampaignStatusStrings.Live);
        if (campaign == null)
            throw new InvalidOperationException($"Campaign not found: tenantId={tenantId}, campaignId={campaignId}");

        var json = JsonSerializer.Serialize(campaign, JsonOptions);
        return new TextResourceContents
        {
            Uri = $"journeys://campaign/{tenantId}/{campaignId}",
            MimeType = "application/json",
            Text = json
        };
    }

    [McpServerResource(
        UriTemplate = "journeys://account/{tenantId}/{accountId}",
        Name = "Journeys Loyalty Account",
        MimeType = "application/json")]
    [Description("Returns a loyalty account by tenant and account ID.")]
    public async Task<ResourceContents> GetAccount(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The loyalty account identifier.")] string accountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("tenantId and accountId are required.");

        var account = await _accountService.GetLoyaltyAccountAsync(tenantId, accountId, resettleIfNeeded: false);
        if (account == null)
            throw new InvalidOperationException($"Account not found: tenantId={tenantId}, accountId={accountId}");

        var json = JsonSerializer.Serialize(account, JsonOptions);
        return new TextResourceContents
        {
            Uri = $"journeys://account/{tenantId}/{accountId}",
            MimeType = "application/json",
            Text = json
        };
    }

    [McpServerResource(
        UriTemplate = "journeys://campaign/{tenantId}/{campaignId}/segments",
        Name = "Journeys Campaign Segments",
        MimeType = "application/json")]
    [Description("Returns the segments of a campaign by tenant and campaign ID (Live status).")]
    public async Task<ResourceContents> GetCampaignSegments(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The campaign identifier.")] string campaignId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("tenantId and campaignId are required.");

        var campaign = await _campaignService.FetchCampaignAsync(tenantId, campaignId, CampaignStatusStrings.Live);
        if (campaign == null)
            throw new InvalidOperationException($"Campaign not found: tenantId={tenantId}, campaignId={campaignId}");

        var segments = campaign.Segments ?? new List<SegmentDto>();
        var json = JsonSerializer.Serialize(segments, JsonOptions);
        return new TextResourceContents
        {
            Uri = $"journeys://campaign/{tenantId}/{campaignId}/segments",
            MimeType = "application/json",
            Text = json
        };
    }

    [McpServerResource(
        UriTemplate = "journeys://journey/{tenantId}/{campaignId}",
        Name = "Journeys Journey",
        MimeType = "application/json")]
    [Description("Returns the journey definition (nodes, rules, navigation) for a campaign by tenant and campaign ID (Live status).")]
    public async Task<ResourceContents> GetJourney(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The campaign identifier.")] string campaignId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("tenantId and campaignId are required.");

        var campaign = await _campaignService.FetchCampaignAsync(tenantId, campaignId, CampaignStatusStrings.Live);
        if (campaign == null)
            throw new InvalidOperationException($"Campaign not found: tenantId={tenantId}, campaignId={campaignId}");
        if (campaign.Journey == null)
            throw new InvalidOperationException($"Campaign has no journey: tenantId={tenantId}, campaignId={campaignId}");

        var json = JsonSerializer.Serialize(campaign.Journey, JsonOptions);
        return new TextResourceContents
        {
            Uri = $"journeys://journey/{tenantId}/{campaignId}",
            MimeType = "application/json",
            Text = json
        };
    }

    [McpServerResource(
        UriTemplate = "journeys://point-account-type/{tenantId}/{pointAccountTypeId}",
        Name = "Journeys Point Account Type",
        MimeType = "application/json")]
    [Description("Returns a point account type by tenant and ID.")]
    public async Task<ResourceContents> GetPointAccountType(
        [Description("The tenant identifier.")] string tenantId,
        [Description("The point account type identifier.")] string pointAccountTypeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(pointAccountTypeId))
            throw new ArgumentException("tenantId and pointAccountTypeId are required.");

        var pat = await _campaignService.FetchPointAccountType(tenantId, pointAccountTypeId);
        if (pat == null)
            throw new InvalidOperationException($"Point account type not found: tenantId={tenantId}, pointAccountTypeId={pointAccountTypeId}");

        var json = JsonSerializer.Serialize(pat, JsonOptions);
        return new TextResourceContents
        {
            Uri = $"journeys://point-account-type/{tenantId}/{pointAccountTypeId}",
            MimeType = "application/json",
            Text = json
        };
    }
}
