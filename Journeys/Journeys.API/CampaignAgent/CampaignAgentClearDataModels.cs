namespace Journeys.API.CampaignAgent;

/// <summary>Which persisted categories to clear (session manifest or tenant-wide listing).</summary>
public sealed class ClearCategoryFlags
{
    public bool Campaigns { get; set; }

    public bool PointAccountTypes { get; set; }

    public bool SaveModelEntities { get; set; }

    /// <summary>
    /// Tenant-wide: delete Backend model definitions via MCP <c>DeleteModel</c>, only when
    /// <c>isPerTenancy</c> is false on the model payload and <c>CampaignAgent:AllowBackendDeleteModel</c> is enabled.
    /// </summary>
    public bool ModelDefinitions { get; set; }
}

public sealed class ClearDataError
{
    public ClearDataError(string scope, string message)
    {
        Scope = scope;
        Message = message;
    }

    public string Scope { get; }

    public string Message { get; }
}

/// <summary>Outcome of a session or tenant clear operation (partial success allowed).</summary>
/// <summary>POST body for <c>…/campaign-agent/clear/session</c>.</summary>
public sealed class ClearSessionRequest
{
    public string ConversationId { get; set; } = "";

    public ClearCategoryFlags Categories { get; set; } = new();
}

/// <summary>POST body for <c>…/campaign-agent/clear/tenant</c>.</summary>
public sealed class ClearTenantRequest
{
    public ClearCategoryFlags Categories { get; set; } = new();

    /// <summary>Must exactly match the route tenant id (case-insensitive).</summary>
    public string ConfirmTenantId { get; set; } = "";
}

public sealed class ClearDataResult
{
    public int CampaignsDeleted { get; set; }

    public int CampaignsFailed { get; set; }

    public int PointAccountTypesDeleted { get; set; }

    public int PointAccountTypesFailed { get; set; }

    public int SaveModelEntitiesDeleted { get; set; }

    public int SaveModelEntitiesFailed { get; set; }

    public int ModelDefinitionsDeleted { get; set; }

    public int ModelDefinitionsFailed { get; set; }

    public int ModelDefinitionsSkippedNotPerTenant { get; set; }

    public List<string> Warnings { get; } = new();

    public List<ClearDataError> Errors { get; } = new();
}
