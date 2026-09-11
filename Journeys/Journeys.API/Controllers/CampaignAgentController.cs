using System.Security.Claims;
using System.Text.Json;
using Journeys.API.CampaignAgent;
using Journeys.Core.Interfaces.DataStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers;

/// <summary>
/// Campaign-agent chat for Next.js and other UI clients (SSE streaming).
/// Tenant id is supplied in the URL. User id comes from the JWT when present, else from
/// <c>X-Journeys-Audit</c> <c>adminUserId</c> when a valid <c>Journeys-API-KEY</c> is supplied (EXP admin-web proxy).
/// When <c>CampaignAgent:AllowAnonymousUnauthenticatedAccess</c> is true, anonymous callers use
/// <c>CampaignAgent:AnonymousDevUserId</c> (Development only — do not enable in production).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/{tenantId}/campaign-agent")]
public class CampaignAgentController : ControllerBase
{
    private readonly IAgentMessageAdapter _messageAdapter;
    private readonly ICampaignAgentOrchestrator _orchestrator;
    private readonly ICampaignAgentDataClearService _dataClearService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CampaignAgentController> _logger;

    public CampaignAgentController(
        IAgentMessageAdapter messageAdapter,
        ICampaignAgentOrchestrator orchestrator,
        ICampaignAgentDataClearService dataClearService,
        IConfiguration configuration,
        ILogger<CampaignAgentController> logger)
    {
        _messageAdapter = messageAdapter;
        _orchestrator = orchestrator;
        _dataClearService = dataClearService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Lists recent chat threads for the current user, derived from stored messages (approximate ordering).
    /// </summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ListThreadsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ListThreadsResponse>> ListThreads(
        [FromRoute] string tenantId,
        [FromQuery] int maxMessagesScan = 500,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        tenantId = tenantId.Trim();
        var userId = ResolveEffectiveUserId(User, Request);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Missing user claim in JWT (or enable CampaignAgent:AllowAnonymousUnauthenticatedAccess for dev)." });

        if (!RouteTenantAllowedForPrincipal(tenantId, User))
            return Forbid();

        maxMessagesScan = Math.Clamp(maxMessagesScan, 50, 1000);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var recent = await _messageAdapter.ListRecentMessagesForUserAsync(tenantId, userId, maxMessagesScan, cancellationToken);
        var byConv = new Dictionary<string, (DateTimeOffset Last, string? Linked)>(StringComparer.Ordinal);
        foreach (var m in recent.OrderByDescending(x => x.LastUpdated ?? x.CreateDate ?? DateTimeOffset.MinValue))
        {
            var ts = m.LastUpdated ?? m.CreateDate ?? DateTimeOffset.MinValue;
            if (!byConv.TryGetValue(m.ConversationId, out var cur) || ts > cur.Last)
                byConv[m.ConversationId] = (ts, m.LinkedCampaignId ?? cur.Linked);
        }

        var items = byConv
            .OrderByDescending(kv => kv.Value.Last)
            .Take(pageSize)
            .Select(kv => new ThreadSummary(kv.Key, kv.Value.Last, kv.Value.Linked))
            .ToList();

        return Ok(new ListThreadsResponse(items));
    }

    /// <summary>
    /// Stream one assistant turn (SSE: started with conversationId, delta, done | error). Omit conversationId to start a new thread.
    /// </summary>
    [HttpPost("messages/stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task StreamMessage(
        [FromRoute] string tenantId,
        [FromBody] StreamMessageRequest body,
        CancellationToken cancellationToken)
    {
        tenantId = tenantId.Trim();
        var userId = ResolveEffectiveUserId(User, Request);
        if (string.IsNullOrEmpty(userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.ContentType = "application/json";
            await Response.WriteAsync("{\"error\":\"Missing user claim in JWT (or enable CampaignAgent:AllowAnonymousUnauthenticatedAccess for dev).\"}", cancellationToken);
            return;
        }

        if (!RouteTenantAllowedForPrincipal(tenantId, User))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            Response.ContentType = "application/json";
            await Response.WriteAsync("{\"error\":\"JWT tenant does not match route tenant.\"}", cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(body.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            Response.ContentType = "application/json";
            await Response.WriteAsync("{\"error\":\"Message is required.\"}", cancellationToken);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.Append("X-Accel-Buffering", "no");
        // Match other streaming actions (e.g. EventsController bulk): avoid holding the body in a server buffer until the request ends.
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        // Commit headers and start the response immediately so the client sees an open stream while MCP/LLM work runs.
        await Response.StartAsync(cancellationToken);

        await _orchestrator.RunStreamingTurnAsync(
            tenantId,
            userId,
            body.ConversationId,
            body.Message.Trim(),
            body.LinkedCampaignId,
            body.ClientMessageId,
            Response.Body,
            cancellationToken);
    }

    /// <summary>
    /// Deletes data attributed to this conversation (from persisted agent/tool history) for the selected categories.
    /// </summary>
    [HttpPost("clear/session")]
    [ProducesResponseType(typeof(ClearDataResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClearDataResult>> ClearSessionData(
        [FromRoute] string tenantId,
        [FromBody] ClearSessionRequest body,
        CancellationToken cancellationToken = default)
    {
        tenantId = tenantId.Trim();
        var userId = ResolveEffectiveUserId(User, Request);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Missing user claim in JWT (or enable CampaignAgent:AllowAnonymousUnauthenticatedAccess for dev)." });

        if (!RouteTenantAllowedForPrincipal(tenantId, User))
            return Forbid();

        if (string.IsNullOrWhiteSpace(body.ConversationId))
            return BadRequest(new { error = "ConversationId is required." });

        var result = await _dataClearService
            .ClearSessionAsync(tenantId, userId, body.ConversationId.Trim(), body.Categories, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Dangerous: deletes campaigns, point account types, SaveModel-backed entities, and/or Backend model definitions
    /// (only definitions with isPerTenancy=false, and only when CampaignAgent:AllowBackendDeleteModel is true) for the tenant when categories are set.
    /// </summary>
    [HttpPost("clear/tenant")]
    [ProducesResponseType(typeof(ClearDataResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClearDataResult>> ClearTenantData(
        [FromRoute] string tenantId,
        [FromBody] ClearTenantRequest body,
        CancellationToken cancellationToken = default)
    {
        tenantId = tenantId.Trim();
        var userId = ResolveEffectiveUserId(User, Request);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Missing user claim in JWT (or enable CampaignAgent:AllowAnonymousUnauthenticatedAccess for dev)." });

        if (!RouteTenantAllowedForPrincipal(tenantId, User))
            return Forbid();

        if (string.IsNullOrWhiteSpace(body.ConfirmTenantId)
            || !string.Equals(body.ConfirmTenantId.Trim(), tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "ConfirmTenantId must exactly match the route tenant id." });
        }

        _logger.LogWarning(
            "Campaign-agent tenant-wide clear requested for tenant {TenantId} by user {UserId}: campaigns={Campaigns}, pointAccountTypes={Pats}, saveModel={Save}, modelDefinitions={ModelDefs}",
            tenantId,
            userId,
            body.Categories.Campaigns,
            body.Categories.PointAccountTypes,
            body.Categories.SaveModelEntities,
            body.Categories.ModelDefinitions);

        var result = await _dataClearService.ClearTenantAsync(tenantId, body.Categories, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// When JWT carries a tenant claim and <c>CampaignAgent:RequireJwtTenantMatchesRoute</c> is true (default), it must equal <paramref name="routeTenantId"/> (case-insensitive).
    /// </summary>
    private bool RouteTenantAllowedForPrincipal(string routeTenantId, ClaimsPrincipal user)
    {
        if (!_configuration.GetValue("CampaignAgent:RequireJwtTenantMatchesRoute", true))
            return true;

        var claimTenant = ResolveTenantIdFromClaimsOnly(user);
        if (string.IsNullOrWhiteSpace(claimTenant))
            return true;

        return string.Equals(claimTenant.Trim(), routeTenantId, StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveTenantIdFromClaimsOnly(ClaimsPrincipal user)
    {
        var claimTypes = _configuration.GetSection("CampaignAgent:TenantIdClaimTypes").Get<string[]>();
        if (claimTypes != null)
        {
            foreach (var ct in claimTypes)
            {
                var v = user.FindFirst(ct)?.Value;
                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
        }

        foreach (var ct in new[] { "tenant_id", "tid", "http://schemas.microsoft.com/identity/claims/tenantid" })
        {
            var v = user.FindFirst(ct)?.Value;
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }

    private static string? ResolveUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? user.FindFirstValue("oid")
        ?? user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

    /// <summary>
    /// JWT user id when authenticated; else <c>X-Journeys-Audit</c> adminUserId when <c>Journeys-API-KEY</c> is valid;
    /// otherwise optional fixed dev user when explicitly allowed by configuration.
    /// </summary>
    private string? ResolveEffectiveUserId(ClaimsPrincipal user, HttpRequest request)
    {
        var fromClaims = ResolveUserId(user);
        if (!string.IsNullOrEmpty(fromClaims))
            return fromClaims;

        var fromAudit = ResolveUserIdFromJourneysAuditHeader(request);
        if (!string.IsNullOrEmpty(fromAudit))
            return fromAudit;

        if (!_configuration.GetValue("CampaignAgent:AllowAnonymousUnauthenticatedAccess", false))
            return null;

        var configured = _configuration["CampaignAgent:AnonymousDevUserId"]?.Trim();
        return string.IsNullOrEmpty(configured) ? "anonymous-campaign-agent" : configured;
    }

    /// <summary>
    /// Trusted proxy path: valid <c>Journeys-API-KEY</c> plus <c>X-Journeys-Audit</c> JSON with non-empty <c>adminUserId</c>.
    /// </summary>
    private string? ResolveUserIdFromJourneysAuditHeader(HttpRequest request)
    {
        var apiKey = request.Headers["Journeys-API-KEY"].ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var validKeys = _configuration.GetSection("ApiKeys").Get<string[]>() ?? Array.Empty<string>();
        if (!validKeys.Contains(apiKey))
            return null;

        if (!request.Headers.TryGetValue("X-Journeys-Audit", out var auditValues))
            return null;

        var auditJson = auditValues.ToString();
        if (string.IsNullOrWhiteSpace(auditJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(auditJson);
            if (!doc.RootElement.TryGetProperty("adminUserId", out var adminUserIdEl))
                return null;

            var adminUserId = adminUserIdEl.GetString()?.Trim();
            return string.IsNullOrEmpty(adminUserId) ? null : adminUserId;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Campaign-agent X-Journeys-Audit header is not valid JSON");
            return null;
        }
    }
}

public sealed class ThreadSummary
{
    public ThreadSummary(string conversationId, DateTimeOffset lastActivity, string? linkedCampaignId)
    {
        ConversationId = conversationId;
        LastActivity = lastActivity;
        LinkedCampaignId = linkedCampaignId;
    }

    public string ConversationId { get; }
    public DateTimeOffset LastActivity { get; }
    public string? LinkedCampaignId { get; }
}

public sealed class ListThreadsResponse
{
    public ListThreadsResponse(List<ThreadSummary> items)
    {
        Items = items;
    }

    public List<ThreadSummary> Items { get; }
}

public sealed class StreamMessageRequest
{
    public string Message { get; set; } = "";

    /// <summary>When null or omitted, the server starts a new thread and returns its id on the SSE <c>started</c> event.</summary>
    public string? ConversationId { get; set; }

    public string? LinkedCampaignId { get; set; }
    public string? ClientMessageId { get; set; }
}
