using System.Text.Json;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Orchestrates campaign-agent data clearing from stored messages (session) or tenant-wide listings.
/// SaveModel deletes use Backend MCP when a matching delete tool is advertised; otherwise the operation records warnings.
/// Model definition deletes use Backend MCP DeleteModel when enabled and only for models with isPerTenancy=false.
/// </summary>
public sealed class CampaignAgentDataClearService : ICampaignAgentDataClearService
{
    private const int MaxSessionMessages = 1000;
    private const int TenantPageSize = 200;
    private const int ModelListPageSize = 500;
    private const int MaxModelListPages = 500;

    private readonly record struct ModelDefinitionDeleteTarget(string ModelId, string ModelType);

    private readonly IAgentMessageAdapter _messageAdapter;
    private readonly ICampaignService _campaignService;
    private readonly IPointAccountTypeCache _pointAccountTypeCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CampaignAgentDataClearService> _logger;

    public CampaignAgentDataClearService(
        IAgentMessageAdapter messageAdapter,
        ICampaignService campaignService,
        IPointAccountTypeCache pointAccountTypeCache,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CampaignAgentDataClearService> logger)
    {
        _messageAdapter = messageAdapter;
        _campaignService = campaignService;
        _pointAccountTypeCache = pointAccountTypeCache;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ClearDataResult> ClearSessionAsync(
        string tenantId,
        string ownerUserId,
        string conversationId,
        ClearCategoryFlags categories,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var result = new ClearDataResult();
        if (!categories.Campaigns && !categories.PointAccountTypes && !categories.SaveModelEntities && !categories.ModelDefinitions)
            return result;

        if (categories.ModelDefinitions)
        {
            result.Warnings.Add(
                "Model definitions: clearing is tenant-wide only; use POST …/clear/tenant with Categories.ModelDefinitions (not session clear).");
            if (!categories.Campaigns && !categories.PointAccountTypes && !categories.SaveModelEntities)
                return result;
        }

        var messages = await _messageAdapter
            .ListMessagesAsync(tenantId, ownerUserId, conversationId, MaxSessionMessages, cancellationToken)
            .ConfigureAwait(false);
        var manifest = AgentConversationManifestParser.BuildManifest(messages);

        await using var backendMcp = await TryCreateBackendMcpClientAsync(cancellationToken).ConfigureAwait(false);

        if (categories.SaveModelEntities)
        {
            if (backendMcp is null)
            {
                result.Warnings.Add("SaveModel (session): Backend MCP is not configured or unreachable; skipped.");
            }
            else
            {
                var tools = (await backendMcp.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Cast<AITool>().ToList();
                var deleteTool = CampaignAgentBackendMcp.TryResolveDeleteEntityToolName(tools);
                if (string.IsNullOrEmpty(deleteTool))
                {
                    result.Warnings.Add(
                        "SaveModel (session): no delete tool found on Backend MCP (expected names like DeleteEntity/delete_entity); skipped.");
                }
                else
                {
                    foreach (var r in manifest.SaveModelEntities)
                    {
                        if (string.IsNullOrWhiteSpace(r.ModelName) || string.IsNullOrWhiteSpace(r.EntityId))
                        {
                            result.Warnings.Add(
                                $"SaveModel (session): skipped entry missing model name or id (model={r.ModelName}, id={r.EntityId}).");
                            continue;
                        }

                        var ok = await TryDeleteSaveModelEntityAsync(
                                backendMcp,
                                deleteTool,
                                tenantId,
                                r.ModelName!,
                                r.EntityId!,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (ok)
                        {
                            result.SaveModelEntitiesDeleted++;
                        }
                        else
                        {
                            result.SaveModelEntitiesFailed++;
                            result.Errors.Add(
                                new ClearDataError(
                                    $"SaveModel:{r.ModelName}:{r.EntityId}",
                                    "Backend MCP delete tool call failed or returned isError=true."));
                        }
                    }
                }
            }
        }

        if (categories.Campaigns)
        {
            foreach (var c in manifest.Campaigns)
            {
                try
                {
                    await _campaignService
                        .DeleteCampaignAsync(tenantId, c.CampaignId, c.Status)
                        .ConfigureAwait(false);
                    result.CampaignsDeleted++;
                }
                catch (Exception ex)
                {
                    result.CampaignsFailed++;
                    result.Errors.Add(new ClearDataError($"Campaign:{c.CampaignId}", ex.Message));
                    _logger.LogWarning(ex, "Session clear: failed to delete campaign {CampaignId} for tenant {TenantId}", c.CampaignId, tenantId);
                }
            }
        }

        if (categories.PointAccountTypes)
        {
            foreach (var patId in manifest.PointAccountTypeIds)
            {
                try
                {
                    await _campaignService.DeletePointAccountTypeAsync(tenantId, patId).ConfigureAwait(false);
                    result.PointAccountTypesDeleted++;
                }
                catch (Exception ex)
                {
                    result.PointAccountTypesFailed++;
                    result.Errors.Add(new ClearDataError($"PointAccountType:{patId}", ex.Message));
                    _logger.LogWarning(ex, "Session clear: failed to delete PAT {PatId} for tenant {TenantId}", patId, tenantId);
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ClearDataResult> ClearTenantAsync(
        string tenantId,
        ClearCategoryFlags categories,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var result = new ClearDataResult();
        if (!categories.Campaigns && !categories.PointAccountTypes && !categories.SaveModelEntities && !categories.ModelDefinitions)
            return result;

        await using var backendMcp = await TryCreateBackendMcpClientAsync(cancellationToken).ConfigureAwait(false);

        if (categories.SaveModelEntities)
        {
            if (backendMcp is null)
            {
                result.Warnings.Add("SaveModel (tenant): Backend MCP is not configured or unreachable; skipped.");
            }
            else
            {
                await ClearTenantSaveModelBestEffortAsync(backendMcp, tenantId, result, cancellationToken).ConfigureAwait(false);
            }
        }

        if (categories.ModelDefinitions)
        {
            if (backendMcp is null)
            {
                result.Warnings.Add("Model definitions (tenant): Backend MCP is not configured or unreachable; skipped.");
            }
            else
            {
                await ClearTenantModelDefinitionsBestEffortAsync(backendMcp, tenantId, result, cancellationToken).ConfigureAwait(false);
            }
        }

        if (categories.Campaigns)
        {
            foreach (var status in new[]
                     {
                         CampaignStatusStrings.Live, CampaignStatusStrings.Draft, CampaignStatusStrings.Archive,
                         CampaignStatusStrings.Pause,
                     })
            {
                string? token = null;
                do
                {
                    var page = await _campaignService
                        .GetCampaignsByStatusAsync(tenantId, status, TenantPageSize, token)
                        .ConfigureAwait(false);
                    var entities = page.Entities ?? new List<CampaignDto>();
                    foreach (var dto in entities)
                    {
                        if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Status))
                            continue;
                        try
                        {
                            await _campaignService.DeleteCampaignAsync(tenantId, dto.Id!, dto.Status!).ConfigureAwait(false);
                            result.CampaignsDeleted++;
                        }
                        catch (Exception ex)
                        {
                            result.CampaignsFailed++;
                            result.Errors.Add(new ClearDataError($"Campaign:{dto.Id}", ex.Message));
                            _logger.LogWarning(ex, "Tenant clear: failed to delete campaign {CampaignId}", dto.Id);
                        }
                    }

                    token = string.IsNullOrEmpty(page.ContinuationToken) ? null : page.ContinuationToken;
                } while (!string.IsNullOrEmpty(token));
            }
        }

        if (categories.PointAccountTypes)
        {
            string? token = null;
            do
            {
                var page = await _campaignService
                    .GetAllPointAccountTypesAsync(tenantId, TenantPageSize, token ?? string.Empty)
                    .ConfigureAwait(false);
                var entities = page.Entities ?? new List<PointAccountTypeDto>();
                foreach (var dto in entities)
                {
                    if (string.IsNullOrWhiteSpace(dto.Id))
                        continue;
                    try
                    {
                        await _campaignService.DeletePointAccountTypeAsync(tenantId, dto.Id!).ConfigureAwait(false);
                        result.PointAccountTypesDeleted++;
                    }
                    catch (Exception ex)
                    {
                        result.PointAccountTypesFailed++;
                        result.Errors.Add(new ClearDataError($"PointAccountType:{dto.Id}", ex.Message));
                        _logger.LogWarning(ex, "Tenant clear: failed to delete PAT {PatId}", dto.Id);
                    }
                }

                token = string.IsNullOrEmpty(page.ContinuationToken) ? null : page.ContinuationToken;
            } while (!string.IsNullOrEmpty(token));

            try
            {
                await _pointAccountTypeCache.InvalidateTenantPointAccountTypesAsync(tenantId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tenant clear: PAT cache invalidation failed for tenant {TenantId}", tenantId);
            }
        }

        return result;
    }

    private async Task ClearTenantSaveModelBestEffortAsync(
        McpClient mcp,
        string tenantId,
        ClearDataResult result,
        CancellationToken cancellationToken)
    {
        var tools = (await mcp.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Cast<AITool>().ToList();
        var deleteTool = CampaignAgentBackendMcp.TryResolveDeleteEntityToolName(tools);
        if (string.IsNullOrEmpty(deleteTool))
        {
            result.Warnings.Add(
                "SaveModel (tenant): no delete tool on Backend MCP; skipped. Configure an entity delete tool (e.g. DeleteEntity/delete_entity) on the Backend server.");
            return;
        }

        var listTool = CampaignAgentBackendMcp.TryResolveListModelsToolName(tools);
        if (string.IsNullOrEmpty(listTool))
        {
            result.Warnings.Add("SaveModel (tenant): no list_models-style tool found; cannot enumerate entities; skipped.");
            return;
        }

        CallToolResult listResult;
        try
        {
            listResult = await mcp
                .CallToolAsync(
                    listTool,
                    new Dictionary<string, object?> { ["tenantId"] = tenantId },
                    progress: null,
                    options: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"SaveModel (tenant): list tool '{listTool}' failed: {ex.Message}");
            _logger.LogWarning(ex, "Tenant SaveModel clear: list tool {ListTool} failed", listTool);
            return;
        }

        if (listResult.IsError == true)
        {
            result.Warnings.Add($"SaveModel (tenant): list tool '{listTool}' returned isError=true.");
            return;
        }

        if (!TryGetJsonFromCallToolResult(listResult, out var root))
        {
            result.Warnings.Add($"SaveModel (tenant): could not parse JSON from list tool '{listTool}' response.");
            return;
        }

        var targets = new List<SaveModelEntityRef>();
        var seen = new HashSet<SaveModelEntityRef>();
        CollectSaveModelTargetsFromJson(root, targets, null);
        foreach (var t in targets)
        {
            if (!seen.Add(t))
                continue;
            if (string.IsNullOrWhiteSpace(t.ModelName) || string.IsNullOrWhiteSpace(t.EntityId))
                continue;

            var ok = await TryDeleteSaveModelEntityAsync(mcp, deleteTool, tenantId, t.ModelName!, t.EntityId!, cancellationToken)
                .ConfigureAwait(false);
            if (ok)
            {
                result.SaveModelEntitiesDeleted++;
            }
            else
            {
                result.SaveModelEntitiesFailed++;
                result.Errors.Add(
                    new ClearDataError(
                        $"SaveModel:{t.ModelName}:{t.EntityId}",
                        "Backend MCP delete tool call failed or returned isError=true."));
            }
        }

        if (targets.Count == 0)
            result.Warnings.Add(
                "SaveModel (tenant): list response contained no model/id pairs the API could recognize; nothing deleted.");
    }

    private async Task ClearTenantModelDefinitionsBestEffortAsync(
        McpClient mcp,
        string tenantId,
        ClearDataResult result,
        CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue("CampaignAgent:AllowBackendDeleteModel", false))
        {
            result.Warnings.Add(
                "Model definitions (tenant): CampaignAgent:AllowBackendDeleteModel is false; skipped (enable to allow DeleteModel MCP calls).");
            return;
        }

        var tools = (await mcp.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Cast<AITool>().ToList();
        var deleteTool = CampaignAgentBackendMcp.TryResolveDeleteModelToolName(tools);
        if (string.IsNullOrEmpty(deleteTool))
        {
            result.Warnings.Add(
                "Model definitions (tenant): DeleteModel/delete_model not found on Backend MCP; skipped.");
            return;
        }

        var listTool = CampaignAgentBackendMcp.TryResolveListModelsToolName(tools);
        if (string.IsNullOrEmpty(listTool))
        {
            result.Warnings.Add("Model definitions (tenant): no list_models-style tool found; skipped.");
            return;
        }

        var targets = new List<ModelDefinitionDeleteTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedNotPerTenant = 0;

        var isPagedListTool = listTool.Equals("ListModels", StringComparison.OrdinalIgnoreCase)
                              || listTool.Equals("list_models", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isPagedListTool)
            {
                string? continuationToken = null;
                for (var page = 0; page < MaxModelListPages; page++)
                {
                    var listArgs = new Dictionary<string, object?>
                    {
                        ["tenantId"] = tenantId,
                        ["pageSize"] = ModelListPageSize,
                    };
                    if (!string.IsNullOrEmpty(continuationToken))
                        listArgs["continuationToken"] = continuationToken;

                    var listResult = await mcp
                        .CallToolAsync(listTool, listArgs, progress: null, options: null, cancellationToken)
                        .ConfigureAwait(false);

                    if (listResult.IsError == true)
                    {
                        result.Warnings.Add($"Model definitions (tenant): list tool '{listTool}' returned isError=true.");
                        return;
                    }

                    if (!TryGetJsonFromCallToolResult(listResult, out var root))
                    {
                        result.Warnings.Add($"Model definitions (tenant): could not parse JSON from list tool '{listTool}'.");
                        return;
                    }

                    AppendModelDefinitionsEligibleForDelete(root, targets, seen, ref skippedNotPerTenant);
                    continuationToken = GetContinuationTokenOrEmpty(root);
                    if (string.IsNullOrEmpty(continuationToken))
                        break;
                }
            }
            else
            {
                var listArgs = new Dictionary<string, object?> { ["tenantId"] = tenantId, ["pageSize"] = ModelListPageSize };
                var listResult = await mcp
                    .CallToolAsync(listTool, listArgs, progress: null, options: null, cancellationToken)
                    .ConfigureAwait(false);

                if (listResult.IsError == true)
                {
                    result.Warnings.Add($"Model definitions (tenant): list tool '{listTool}' returned isError=true.");
                    return;
                }

                if (!TryGetJsonFromCallToolResult(listResult, out var root))
                {
                    result.Warnings.Add($"Model definitions (tenant): could not parse JSON from list tool '{listTool}'.");
                    return;
                }

                AppendModelDefinitionsEligibleForDelete(root, targets, seen, ref skippedNotPerTenant);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Model definitions (tenant): list failed: {ex.Message}");
            _logger.LogWarning(ex, "Tenant model definition clear: list tool {ListTool} failed", listTool);
            return;
        }

        result.ModelDefinitionsSkippedNotPerTenant = skippedNotPerTenant;

        if (targets.Count == 0)
        {
            result.Warnings.Add(
                "Model definitions (tenant): no eligible models (requires id, modelType, isPerTenancy=false); nothing deleted.");
            return;
        }

        foreach (var t in targets)
        {
            var ok = await TryDeleteModelDefinitionAsync(mcp, deleteTool, tenantId, t.ModelId, t.ModelType, cancellationToken)
                .ConfigureAwait(false);
            if (ok)
                result.ModelDefinitionsDeleted++;
            else
            {
                result.ModelDefinitionsFailed++;
                result.Errors.Add(
                    new ClearDataError(
                        $"ModelDefinition:{t.ModelId}",
                        "Backend MCP DeleteModel call failed or returned isError=true."));
            }
        }
    }

    private static void AppendModelDefinitionsEligibleForDelete(
        JsonElement root,
        List<ModelDefinitionDeleteTarget> acc,
        HashSet<string> seen,
        ref int skippedNotPerTenant)
    {
        if (TryGetArrayProperty(root, "Items", out var items))
        {
            foreach (var el in items.EnumerateArray())
                TryAddOneModelDefinitionDelete(el, acc, seen, ref skippedNotPerTenant);
            return;
        }

        if (TryGetArrayProperty(root, "models", out var models))
        {
            foreach (var el in models.EnumerateArray())
                TryAddOneModelDefinitionDelete(el, acc, seen, ref skippedNotPerTenant);
            return;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
                TryAddOneModelDefinitionDelete(el, acc, seen, ref skippedNotPerTenant);
        }
    }

    private static void TryAddOneModelDefinitionDelete(
        JsonElement el,
        List<ModelDefinitionDeleteTarget> acc,
        HashSet<string> seen,
        ref int skippedNotPerTenant)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return;

        if (!TryGetStringProperty(el, "id", out var id) || string.IsNullOrWhiteSpace(id))
            return;

        if (!TryGetStringProperty(el, "modelType", out var modelType) || string.IsNullOrWhiteSpace(modelType))
            return;

        if (!TryGetBoolProperty(el, "isPerTenancy", out var isPerTenancy))
        {
            skippedNotPerTenant++;
            return;
        }

        if (isPerTenancy)
        {
            skippedNotPerTenant++;
            return;
        }

        if (!seen.Add(id))
            return;

        acc.Add(new ModelDefinitionDeleteTarget(id, modelType));
    }

    private static bool TryGetArrayProperty(JsonElement obj, string name, out JsonElement array)
    {
        array = default;
        if (obj.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var p in obj.EnumerateObject())
        {
            if (!string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (p.Value.ValueKind != JsonValueKind.Array)
                return false;
            array = p.Value;
            return true;
        }

        return false;
    }

    private static bool TryGetBoolProperty(JsonElement obj, string name, out bool value)
    {
        value = false;
        if (obj.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var p in obj.EnumerateObject())
        {
            if (!string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (p.Value.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }

            if (p.Value.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }

            return false;
        }

        return false;
    }

    private static string? GetContinuationTokenOrEmpty(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var p in root.EnumerateObject())
        {
            if (!string.Equals(p.Name, "continuationToken", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(p.Name, "ContinuationToken", StringComparison.OrdinalIgnoreCase))
                continue;
            if (p.Value.ValueKind != JsonValueKind.String)
                return null;
            return p.Value.GetString() ?? string.Empty;
        }

        return null;
    }

    private static void CollectSaveModelTargetsFromJson(JsonElement root, List<SaveModelEntityRef> acc, string? modelHint)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var el in root.EnumerateArray())
                    CollectSaveModelTargetsFromJson(el, acc, modelHint);
                return;
            case JsonValueKind.Object:
                var modelName = modelHint;
                if (TryGetStringProperty(root, "modelName", out var mn1))
                    modelName = mn1 ?? modelName;
                else if (TryGetStringProperty(root, "model", out var mn2))
                    modelName = mn2 ?? modelName;
                else if (TryGetStringProperty(root, "type", out var mn3))
                    modelName = mn3 ?? modelName;

                if (TryGetStringProperty(root, "id", out var id) && !string.IsNullOrWhiteSpace(id))
                {
                    if (!string.IsNullOrWhiteSpace(modelName))
                        acc.Add(new SaveModelEntityRef(modelName, id));
                }

                foreach (var p in root.EnumerateObject())
                {
                    if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        CollectSaveModelTargetsFromJson(p.Value, acc, modelName ?? modelHint);
                }

                return;
        }
    }

    private static bool TryGetStringProperty(JsonElement obj, string name, out string? value)
    {
        value = null;
        if (obj.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var prop in obj.EnumerateObject())
        {
            if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind != JsonValueKind.String)
                return false;
            value = prop.Value.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static bool TryGetJsonFromCallToolResult(CallToolResult result, out JsonElement root)
    {
        root = default;
        if (result.StructuredContent is JsonElement je)
        {
            root = je;
            return true;
        }

        if (result.Content is { Count: > 0 } contents)
        {
            foreach (var c in contents)
            {
                if (c is TextContentBlock tcb && !string.IsNullOrWhiteSpace(tcb.Text))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(tcb.Text);
                        root = doc.RootElement.Clone();
                        return true;
                    }
                    catch (JsonException)
                    {
                        /* try next */
                    }
                }
            }
        }

        return false;
    }

    private async Task<bool> TryDeleteModelDefinitionAsync(
        McpClient mcp,
        string deleteToolName,
        string tenantId,
        string modelId,
        string modelType,
        CancellationToken cancellationToken)
    {
        var args = new Dictionary<string, object?>
        {
            ["tenantId"] = tenantId,
            ["modelId"] = modelId,
            ["modelType"] = modelType,
        };

        try
        {
            var tr = await mcp
                .CallToolAsync(deleteToolName, args, progress: null, options: null, cancellationToken)
                .ConfigureAwait(false);
            if (tr.IsError == true)
                return false;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DeleteModel tool {Tool} failed for {ModelId}/{ModelType}", deleteToolName, modelId, modelType);
            return false;
        }
    }

    private async Task<bool> TryDeleteSaveModelEntityAsync(
        McpClient mcp,
        string deleteToolName,
        string tenantId,
        string modelName,
        string entityId,
        CancellationToken cancellationToken)
    {
        var args = new Dictionary<string, object?>
        {
            ["tenantId"] = tenantId,
            ["modelName"] = modelName,
            ["id"] = entityId,
            ["entityId"] = entityId,
            ["entityJson"] = JsonSerializer.Serialize(new Dictionary<string, object?> { ["id"] = entityId }),
        };

        try
        {
            var tr = await mcp
                .CallToolAsync(deleteToolName, args, progress: null, options: null, cancellationToken)
                .ConfigureAwait(false);
            if (tr.IsError == true)
                return false;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SaveModel delete tool {Tool} failed for {Model}/{Entity}", deleteToolName, modelName, entityId);
            return false;
        }
    }

    private async Task<McpClient?> TryCreateBackendMcpClientAsync(CancellationToken cancellationToken)
    {
        var url = _configuration["CampaignAgent:BackendMcpEndpointUrl"];
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var http = _httpClientFactory.CreateClient("CampaignAgentBackendMcp");
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(url.TrimEnd('/')),
                    Name = "Backend MCP (campaign agent clear)",
                },
                http);
            return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not connect to Backend MCP at {Url} for data clear", url);
            return null;
        }
    }
}
