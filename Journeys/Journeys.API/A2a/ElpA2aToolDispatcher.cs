using System.Text.Json;
using Journeys.API.Mcp;

namespace Journeys.API.A2a;

internal sealed class JourneysA2aToolDispatcher
{
    private readonly JourneysMcpTools _tools;

    public JourneysA2aToolDispatcher(JourneysMcpTools tools)
    {
        _tools = tools;
    }

    public Task<string> DispatchAsync(string tool, JsonElement arguments, string? tenantFromSendMessage, CancellationToken cancellationToken)
    {
        var tenantId = A2aArgs.ResolveTenant(tenantFromSendMessage, arguments);
        cancellationToken.ThrowIfCancellationRequested();

        return tool switch
        {
            "GetCampaign" => _tools.GetCampaign(tenantId, A2aArgs.GetString(arguments, "campaignId") ?? throw new ArgumentException("campaignId is required."), A2aArgs.GetString(arguments, "status"), cancellationToken),
            "GetCampaignByExtId" => _tools.GetCampaignByExtId(tenantId, A2aArgs.GetString(arguments, "extCampaignId") ?? throw new ArgumentException("extCampaignId is required."), A2aArgs.GetString(arguments, "status"), cancellationToken),
            "ListCampaignVersions" => _tools.ListCampaignVersions(tenantId, A2aArgs.GetString(arguments, "extCampaignId") ?? throw new ArgumentException("extCampaignId is required."), cancellationToken),
            "GetSegmentsFromCampaign" => _tools.GetSegmentsFromCampaign(tenantId, A2aArgs.GetString(arguments, "campaignId") ?? throw new ArgumentException("campaignId is required."), A2aArgs.GetString(arguments, "status"), cancellationToken),
            "ListCampaigns" => _tools.ListCampaigns(tenantId, A2aArgs.GetString(arguments, "status"), A2aArgs.GetInt(arguments, "pageSize", 20), A2aArgs.GetString(arguments, "continuationToken"), cancellationToken),
            "GetJourneyFromCampaign" => _tools.GetJourneyFromCampaign(tenantId, A2aArgs.GetString(arguments, "campaignId") ?? throw new ArgumentException("campaignId is required."), A2aArgs.GetString(arguments, "status"), cancellationToken),
            "GetAccount" => _tools.GetAccount(tenantId, A2aArgs.GetString(arguments, "accountId") ?? throw new ArgumentException("accountId is required."), A2aArgs.GetBool(arguments, "resettleIfNeeded"), cancellationToken),
            "GetCampaignStats" => _tools.GetCampaignStats(tenantId, A2aArgs.GetString(arguments, "campaignId") ?? throw new ArgumentException("campaignId is required."), cancellationToken),
            "GetCampaignAssistantContext" => _tools.GetCampaignAssistantContext(
                tenantId,
                A2aArgs.GetString(arguments, "campaignId") ?? throw new ArgumentException("campaignId is required."),
                A2aArgs.GetString(arguments, "status"),
                A2aArgs.GetBool(arguments, "includeSampleTemplate", true),
                cancellationToken),
            "GetRulesEngineContractSummary" => _tools.GetRulesEngineContractSummary(cancellationToken),
            "GetRulePatternRecipes" => _tools.GetRulePatternRecipes(cancellationToken),
            "GetPointAccountType" => _tools.GetPointAccountType(tenantId, A2aArgs.GetString(arguments, "pointAccountTypeId") ?? throw new ArgumentException("pointAccountTypeId is required."), cancellationToken),
            "ListPointAccountTypes" => _tools.ListPointAccountTypes(tenantId, A2aArgs.GetInt(arguments, "pageSize", 20), A2aArgs.GetString(arguments, "continuationToken"), cancellationToken),
            "PreviewTierMove" => _tools.PreviewTierMove(tenantId, A2aArgs.GetJsonString(arguments, "requestJson") ?? throw new ArgumentException("requestJson is required."), cancellationToken),
            "MoveTier" => _tools.MoveTier(tenantId, A2aArgs.GetJsonString(arguments, "requestJson") ?? throw new ArgumentException("requestJson is required."), cancellationToken),
            "UpsertCampaign" => _tools.UpsertCampaign(tenantId, A2aArgs.GetJsonString(arguments, "campaignJson") ?? throw new ArgumentException("campaignJson is required."), cancellationToken),
            "DeleteCampaign" => _tools.DeleteCampaign(tenantId, A2aArgs.GetString(arguments, "campaignId") ?? throw new ArgumentException("campaignId is required."), A2aArgs.GetString(arguments, "status"), cancellationToken),
            "ProcessEvent" => _tools.ProcessEvent(tenantId, A2aArgs.GetString(arguments, "modelName") ?? throw new ArgumentException("modelName is required."), A2aArgs.GetJsonString(arguments, "eventJson") ?? throw new ArgumentException("eventJson is required."), A2aArgs.GetBool(arguments, "reprocessEvent"), A2aArgs.GetString(arguments, "campaignId"), cancellationToken),
            "GetIngestionFolders" => _tools.GetIngestionFolders(tenantId, cancellationToken),
            "GetIngestionSummary" => _tools.GetIngestionSummary(tenantId, A2aArgs.GetString(arguments, "folderName") ?? throw new ArgumentException("folderName is required."), A2aArgs.GetString(arguments, "fileName") ?? throw new ArgumentException("fileName is required."), cancellationToken),
            _ => Task.FromResult(JsonSerializer.Serialize(new { error = $"Unknown tool '{tool}'. See Journeys-A2A-Contract.md for MCP tool names." }, A2aJsonOptions.Instance))
        };
    }
}
