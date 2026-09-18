using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic.Exceptions;
using Backend.Core.Llm;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.CampaignAgent.Remediation;
using Microsoft.Extensions.Options;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;
using Journeys.DTO.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Journeys.API.CampaignAgent;

public class CampaignAgentOrchestrator : ICampaignAgentOrchestrator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string FormatCampaignAgentToolNames(IReadOnlyList<AITool> tools) =>
        tools.Count == 0
            ? "(none)"
            : string.Join(", ", tools.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<string> CollectToolNames(IReadOnlyList<AITool> tools) =>
        tools.Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Cast<string>().ToList();

    private static (string Code, string Text) FormatCampaignAgentTurnError(Exception ex)
    {
        if (ContainsAnthropicTranscriptToolSequenceError(ex))
        {
            return (
                "transcript_tool_sequence",
                "The model rejected the tool message sequence for this turn. Retry the message or start a new conversation.");
        }

        if (ex is AnthropicNotFoundException)
        {
            return (
                "model_not_found",
                "Configured Claude model was not found (it may be retired). Update CampaignAgent:ClaudeModel — current default is claude-sonnet-4-6.");
        }

        if (ex is AnthropicBadRequestException)
            return ("llm_bad_request", ex.Message);

        return ("turn_failed", ex.Message);
    }

    private static bool ContainsAnthropicTranscriptToolSequenceError(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current.Message.Contains("tool_use_id", StringComparison.OrdinalIgnoreCase)
                && current.Message.Contains("tool_result", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private readonly IAgentMessageAdapter _messageAdapter;
    private readonly IConfiguration _configuration;
    private readonly ILlmChatClientFactory _llmChatClientFactory;
    private readonly ICampaignAgentPromptComposer _promptComposer;
    private readonly ILlmPromptChatMapper _llmPromptChatMapper;
    private readonly ILogger<CampaignAgentOrchestrator> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICampaignAgentToolAuditSink _toolAuditSink;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ICampaignWorkflowStore _workflowStore;
    private readonly IOptions<Journeys.API.CampaignAgent.DataWarehouse.DataWarehouseProxyOptions> _dataWarehouseOptions;
    private readonly ITenantVerificationContextLoader _tenantVerificationLoader;

    public CampaignAgentOrchestrator(
        IAgentMessageAdapter messageAdapter,
        IConfiguration configuration,
        ILlmChatClientFactory llmChatClientFactory,
        ICampaignAgentPromptComposer promptComposer,
        ILlmPromptChatMapper llmPromptChatMapper,
        ILogger<CampaignAgentOrchestrator> logger,
        IHttpClientFactory httpClientFactory,
        ICampaignAgentToolAuditSink toolAuditSink,
        ILoggerFactory loggerFactory,
        ICampaignWorkflowStore workflowStore,
        IOptions<Journeys.API.CampaignAgent.DataWarehouse.DataWarehouseProxyOptions> dataWarehouseOptions,
        ITenantVerificationContextLoader tenantVerificationLoader)
    {
        _messageAdapter = messageAdapter;
        _configuration = configuration;
        _llmChatClientFactory = llmChatClientFactory;
        _promptComposer = promptComposer;
        _llmPromptChatMapper = llmPromptChatMapper;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _toolAuditSink = toolAuditSink;
        _loggerFactory = loggerFactory;
        _workflowStore = workflowStore;
        _dataWarehouseOptions = dataWarehouseOptions;
        _tenantVerificationLoader = tenantVerificationLoader;
    }

    public async Task RunStreamingTurnAsync(
        string tenantId,
        string userId,
        string? conversationId,
        string userMessage,
        string? linkedCampaignId,
        string? clientMessageId,
        Stream responseBody,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var persistMetrics = _configuration.GetValue("CampaignAgent:PersistTurnMetrics", true);
        CampaignAgentTurnMetricsCollector? metrics = persistMetrics ? new CampaignAgentTurnMetricsCollector() : null;
        var effectiveConversationId = string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId.Trim();
        var dataWarehouseEnabled = _dataWarehouseOptions.Value.Enabled;

        // Small buffer + explicit flushes so each SSE frame leaves Kestrel/proxies promptly (default 4KB buffer can delay deltas).
        await using var writer = new StreamWriter(
            responseBody,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 256,
            leaveOpen: true);

        McpClient? backendMcpClient = null;

        var lastWriteUtc = DateTimeOffset.UtcNow;
        var turnStartedUtc = DateTimeOffset.UtcNow;

        async Task FlushSseWriterAsync()
        {
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await responseBody.FlushAsync(cancellationToken).ConfigureAwait(false);
            lastWriteUtc = DateTimeOffset.UtcNow;
        }

        async Task WriteSseAsync(string eventName, object payload)
        {
            var json = JsonSerializer.Serialize(payload, JsonOpts);
            await writer.WriteLineAsync($"event: {eventName}");
            await writer.WriteLineAsync($"data: {json}");
            await writer.WriteLineAsync();
            await FlushSseWriterAsync();
        }

        async Task WriteKeepAliveAsync()
        {
            await writer.WriteLineAsync(": keep-alive");
            await writer.WriteLineAsync();
            await FlushSseWriterAsync();
        }

        await using var sseKeepAlive = new CampaignAgentSseKeepAlive(WriteKeepAliveAsync, () => lastWriteUtc);

        async Task WriteProgressAsync(string step, string label, string? detail = null)
        {
            var elapsedMs = (long)(DateTimeOffset.UtcNow - turnStartedUtc).TotalMilliseconds;
            await WriteSseAsync("progress", new { requestId, step, label, detail, elapsedMs });
        }

        try
        {
            await WriteSseAsync("started", new { requestId, clientMessageId, conversationId = effectiveConversationId });
            await WriteProgressAsync("turn_started", "Starting turn…");

            var mcpUrl = _configuration["CampaignAgent:McpEndpointUrl"];
            if (string.IsNullOrWhiteSpace(mcpUrl))
            {
                await WriteSseAsync("error", new { requestId, message = "Campaign agent is not configured (missing CampaignAgent:McpEndpointUrl)." });
                return;
            }

            var loadHistorySw = Stopwatch.StartNew();
            await WriteProgressAsync("load_history", "Loading conversation history…");
            var existing = (await _messageAdapter.ListMessagesAsync(tenantId, userId, effectiveConversationId, 500, cancellationToken)).ToList();
            metrics?.MarkLoadHistoryDone(loadHistorySw.ElapsedMilliseconds);
            var nextSeq = existing.Count == 0 ? 1L : existing.Max(m => m.Sequence) + 1;

            var userRow = new AgentMessage(
                tenantId,
                userId,
                effectiveConversationId,
                nextSeq,
                "user",
                userMessage,
                linkedCampaignId,
                null, null, null, null);
            await _messageAdapter.AppendMessageAsync(tenantId, userRow);
            existing.Add(userRow);
            await WriteProgressAsync("save_user_message", "Saving your message…");

            var workflowState = await _workflowStore
                .GetOrCreateAsync(tenantId, userId, effectiveConversationId, cancellationToken)
                .ConfigureAwait(false);
            workflowState = CampaignWorkflowEngine.ApplyUserMessage(workflowState, userMessage, dataWarehouseEnabled);
            if (ShouldLoadTenantVerificationContext(workflowState, userMessage))
            {
                var tvCtx = await _tenantVerificationLoader.LoadAsync(tenantId, cancellationToken).ConfigureAwait(false);
                _tenantVerificationLoader.ApplyToArtifacts(workflowState, tvCtx);
            }
            CampaignJourneyDeliveryGuard.Enabled =
                _configuration.GetValue("CampaignAgent:JourneyDeliveryGuard:Enabled", true);
            await _workflowStore.SaveAsync(workflowState, cancellationToken).ConfigureAwait(false);

            var historyBudgetOptions = ReadHistoryBudgetOptions(workflowState);
            var budgetMessages = CampaignAgentTokenBudget.Apply(existing, historyBudgetOptions);
            var maxPersistedToolResultChars = ReadMaxPersistedToolResultChars();
            var maxPersistedAssistantNarrativeChars = ReadMaxPersistedAssistantNarrativeChars();

            var workflowHint = CampaignWorkflowPromptFormatter.BuildHint(workflowState);

            var promptComposeSw = Stopwatch.StartNew();
            await WriteProgressAsync("compose_prompt", "Preparing campaign context…");
            var promptContext = await _promptComposer.BuildAsync(
                tenantId,
                linkedCampaignId,
                budgetMessages,
                workflowHint,
                workflowState,
                toolsThisTurn: null,
                cancellationToken);
            metrics?.AddPromptCompose(promptComposeSw.ElapsedMilliseconds);
            _logger.LogDebug(
                "Campaign agent LLM prompt built; governanceContentHash={GovernanceHash}, tenantCuratedContentHash={TenantCuratedHash}, tenantContextCacheVersion={TenantCacheVersion}, ephemeralMessageCount={EphemeralCount}",
                promptContext.GovernanceContentHash,
                promptContext.TenantCuratedContentHash ?? "(none)",
                promptContext.TenantContextCacheVersion ?? "(none)",
                promptContext.EphemeralMessages.Count);

            var conversationMessages = CampaignAgentTranscriptRules.SanitizeChatMessages(
                promptContext.EphemeralMessages.Concat(MapToChatMessages(budgetMessages)));

            var journiesHttpClient = _httpClientFactory.CreateClient("CampaignAgentMcp");
            var journeysTransportOptions = new HttpClientTransportOptions
            {
                Endpoint = new Uri(mcpUrl.TrimEnd('/')),
                Name = "Journeys MCP (campaign agent)"
            };
            var journeysTransport = new HttpClientTransport(journeysTransportOptions, journiesHttpClient);
            await WriteProgressAsync("connect_journeys_mcp", "Connecting to Journeys tools…");
            await using var journeysMcpClient = await McpClient.CreateAsync(journeysTransport, cancellationToken: cancellationToken);
            var journeysAllTools = await journeysMcpClient.ListToolsAsync(cancellationToken: cancellationToken);

            var allowProcess = _configuration.GetValue("CampaignAgent:AllowProcessEvent", false);
            var allowMoveTier = _configuration.GetValue("CampaignAgent:AllowMoveTier", false);
            var readToolDigestEnabled = _configuration.GetValue("CampaignAgent:ReadToolDigestEnabled", true);
            var mutationDigestEnabled = _configuration.GetValue("CampaignAgent:MutationDigestEnabled", true);
            var journeysDigestOptions = new JourneysToolDigestOptions
            {
                AssistantContextDigestEnabled = readToolDigestEnabled,
                MutationDigestEnabled = mutationDigestEnabled,
                RulesContractDigestEnabled = _configuration.GetValue("CampaignAgent:RulesContractDigestEnabled", true),
                RulePatternRecipesDigestEnabled = _configuration.GetValue("CampaignAgent:RulePatternRecipesDigestEnabled", true),
                ExampleCampaignDigestEnabled = _configuration.GetValue("CampaignAgent:ExampleCampaignDigestEnabled", true),
                ValidationDigestEnabled = _configuration.GetValue("CampaignAgent:ValidationDigestEnabled", true),
                ListCampaignsDigestEnabled = _configuration.GetValue("CampaignAgent:ListCampaignsDigestEnabled", true),
            };
            var journeysTools = CampaignAgentJourneysMcp.WrapJourneysToolDigests(
                journeysAllTools
                    .Where(t =>
                    {
                        if (!allowProcess && string.Equals(t.Name, "ProcessEvent", StringComparison.OrdinalIgnoreCase))
                            return false;
                        if (!allowMoveTier && string.Equals(t.Name, "MoveTier", StringComparison.OrdinalIgnoreCase))
                            return false;
                        return true;
                    })
                    .Cast<AITool>(),
                journeysDigestOptions,
                _logger,
                () => workflowState)
                .ToList();

            _logger.LogInformation(
                "Campaign agent Journeys MCP tools ({Count}) from {Url}: {ToolNames}",
                journeysTools.Count,
                mcpUrl,
                FormatCampaignAgentToolNames(journeysTools));

            var aiTools = new List<AITool>(journeysTools);
            int? mcpBackendToolCount = null;
            string? mcpBackendMcpError = null;
            string? mcpBackendToolNames = null;

            // Read once here so both the initial backend tool build and the continuation hop can wrap
            // the backend catalog tools with the model-catalog digest decorator using a per-turn retain spec.
            var modelCatalogDigestEnabled = _configuration.GetValue("CampaignAgent:ModelCatalogDigestEnabled", true);
            var modelCatalogTag = _configuration.GetValue<string>("CampaignAgent:ModelCatalogTag");
            if (string.IsNullOrWhiteSpace(modelCatalogTag))
                modelCatalogTag = EventModelEligibilityValidation.EventableTag;
            var toolNameAliasesEnabled = _configuration.GetValue("CampaignAgent:ToolNameAliasesEnabled", true);

            var backendMcpUrl = _configuration["CampaignAgent:BackendMcpEndpointUrl"];
            if (!string.IsNullOrWhiteSpace(backendMcpUrl))
            {
                await WriteProgressAsync("connect_backend_mcp", "Discovering backend tools…");
                McpClient? pendingBackend = null;
                try
                {
                    var backendHttp = _httpClientFactory.CreateClient("CampaignAgentBackendMcp");
                    var backendTransport = new HttpClientTransport(
                        new HttpClientTransportOptions
                        {
                            Endpoint = new Uri(backendMcpUrl.TrimEnd('/')),
                            Name = "Backend MCP (campaign agent)"
                        },
                        backendHttp);
                    pendingBackend = await McpClient.CreateAsync(backendTransport, cancellationToken: cancellationToken);
                    var backendListed = await pendingBackend.ListToolsAsync(cancellationToken: cancellationToken);
                    var backendRaw = backendListed.Cast<AITool>().ToList();
                    if (backendRaw.Count > 0)
                    {
                        _logger.LogInformation(
                            "Campaign agent Backend MCP raw tools ({Count}) from {Url}: {ToolNames}",
                            backendRaw.Count,
                            backendMcpUrl,
                            FormatCampaignAgentToolNames(backendRaw));
                    }

                    var allowBackendSave = _configuration.GetValue("CampaignAgent:AllowBackendSaveModel", true);
                    var allowBackendDelete = _configuration.GetValue("CampaignAgent:AllowBackendDeleteModel", false);
                    var exposeFullBackend = _configuration.GetValue("CampaignAgent:ExposeFullBackendMcpToolSurface", false);
                    var backendFiltered = CampaignAgentBackendMcp.FilterTools(
                        backendRaw,
                        allowBackendSave,
                        allowBackendDelete,
                        exposeFullBackend);
                    if (backendRaw.Count > 0 && backendFiltered.Count == 0)
                    {
                        if (exposeFullBackend)
                        {
                            _logger.LogWarning(
                                "Backend MCP listed tools but merge produced none (unexpected in full-surface mode). Raw: {Names}",
                                FormatCampaignAgentToolNames(backendRaw));
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Backend MCP tools did not pass campaign allowlist+mutating filter. " +
                                "Set CampaignAgent:AllowBackendSaveModel / AllowBackendDeleteModel for SaveModel/DeleteModel, " +
                                "set CampaignAgent:ExposeFullBackendMcpToolSurface to use every Backend tool, " +
                                "or add missing tool name aliases in CampaignAgentBackendMcp. Raw: {Names}",
                                FormatCampaignAgentToolNames(backendRaw));
                        }
                    }

                    mcpBackendToolCount = backendFiltered.Count;
                    mcpBackendToolNames = FormatCampaignAgentToolNames(backendFiltered);
                    // Use the FULL loaded conversation (not the budget-trimmed list) so the primary
                    // campaign is found even if older segments would be trimmed by the token budget.
                    var backendForAgent = CampaignAgentBackendMcp.WrapCatalogDigest(
                        backendFiltered, modelCatalogTag, modelCatalogDigestEnabled, _logger);
                    backendForAgent = CampaignAgentBackendMcp.WrapMutationDigest(
                        backendForAgent, mutationDigestEnabled, _logger);
                    backendForAgent = CampaignAgentBackendMcp.WrapModelReadDigest(
                        backendForAgent, readToolDigestEnabled, _logger, () => workflowState);
                    aiTools.AddRange(backendForAgent);
                    backendMcpClient = pendingBackend;
                    pendingBackend = null;
                    _logger.LogInformation(
                        "Campaign agent Backend MCP tools after {Mode} ({Count}) from {Url}: {ToolNames}",
                        exposeFullBackend ? "pass-through" : "allowlist filter",
                        backendFiltered.Count,
                        backendMcpUrl,
                        FormatCampaignAgentToolNames(backendFiltered));
                }
                catch (Exception ex)
                {
                    var msg = ex.Message ?? string.Empty;
                    mcpBackendMcpError = msg.Length > 500
                        ? ex.GetType().Name + ": " + msg.Substring(0, 500) + "…"
                        : ex.GetType().Name + ": " + msg;
                    _logger.LogWarning(ex, "Campaign agent Backend MCP unavailable at {Url}; continuing with Journeys tools only.", backendMcpUrl);
                }
                finally
                {
                    if (pendingBackend != null)
                        await pendingBackend.DisposeAsync().ConfigureAwait(false);
                }
            }

            // Deterministic pre-step: on entering EventModels, resolve earning intent + rank existing
            // event models and raise the reuse-or-create selection gate before the LLM turn. If it
            // changes state, rebuild the hint/prompt/messages so the model sees the candidates.
            var eventModelPrepChanged = await TryPrepareEventModelSelectionAsync(
                workflowState,
                backendMcpClient,
                aiTools,
                tenantId,
                cancellationToken).ConfigureAwait(false);
            var deferredSelectionApplied =
                CampaignWorkflowStepManager.TryApplyDeferredEventModelSelection(workflowState, userMessage);
            if (eventModelPrepChanged || deferredSelectionApplied)
            {
                await _workflowStore.SaveAsync(workflowState, cancellationToken).ConfigureAwait(false);
                workflowHint = CampaignWorkflowPromptFormatter.BuildHint(
                    workflowState, CollectToolNames(aiTools));
                BcpArtifactRefresh.Refresh(workflowState);
                promptContext = await _promptComposer.BuildAsync(
                    tenantId,
                    linkedCampaignId,
                    budgetMessages,
                    workflowHint,
                    workflowState,
                    CollectToolNames(aiTools),
                    cancellationToken);
                conversationMessages = CampaignAgentTranscriptRules.SanitizeChatMessages(
                    promptContext.EphemeralMessages.Concat(MapToChatMessages(budgetMessages)));
            }

            var journeyPatternPrepChanged = JourneyPatternPrepResolver.TryPrepare(workflowState);
            if (journeyPatternPrepChanged)
            {
                await _workflowStore.SaveAsync(workflowState, cancellationToken).ConfigureAwait(false);
                workflowHint = CampaignWorkflowPromptFormatter.BuildHint(
                    workflowState, CollectToolNames(aiTools));
                BcpArtifactRefresh.Refresh(workflowState);
                promptContext = await _promptComposer.BuildAsync(
                    tenantId,
                    linkedCampaignId,
                    budgetMessages,
                    workflowHint,
                    workflowState,
                    CollectToolNames(aiTools),
                    cancellationToken);
                conversationMessages = CampaignAgentTranscriptRules.SanitizeChatMessages(
                    promptContext.EphemeralMessages.Concat(MapToChatMessages(budgetMessages)));
            }

            // Lets the BFF/UX and operators see the effective tool surface and Backend MCP health on every turn.
            var backendUrlOk = !string.IsNullOrWhiteSpace(backendMcpUrl);
            string? mcpHint = null;
            if (mcpBackendMcpError is null
                && backendUrlOk
                && mcpBackendToolCount is 0 or null)
            {
                var fullSurf = _configuration.GetValue("CampaignAgent:ExposeFullBackendMcpToolSurface", false);
                mcpHint = fullSurf
                    ? "0 Backend MCP tools in the merge (URL not reachable, or server listed 0; see prior API log lines for raw tool names)."
                    : "0 Backend MCP tools in the merge (URL not reachable, or server listed 0, or all names filtered/AllowBackendSaveModel; set ExposeFullBackendMcpToolSurface for full Backend list; see prior API log lines).";
            }

            await WriteSseAsync(
                "mcp",
                new
                {
                    requestId,
                    journeysToolCount = journeysTools.Count,
                    journeysToolNames = FormatCampaignAgentToolNames(journeysTools),
                    backendMcpUrlConfigured = backendUrlOk,
                    mcpBackendToolCount,
                    backendToolNames = mcpBackendMcpError is not null ? null : mcpBackendToolNames,
                    totalToolCount = aiTools.Count,
                    allToolNames = FormatCampaignAgentToolNames(aiTools),
                    backendMcpError = mcpBackendMcpError,
                    mcpHint,
                    allowBackendSaveModel = _configuration.GetValue("CampaignAgent:AllowBackendSaveModel", true),
                    exposeFullBackendMcpToolSurface = _configuration.GetValue("CampaignAgent:ExposeFullBackendMcpToolSurface", false),
                    workflowPhase = workflowState.Phase.ToString(),
                    workflowCampaignKind = workflowState.CampaignKind.ToString()
                });

            await WriteSseAsync("workflow", new
            {
                requestId,
                phase = workflowState.Phase.ToString(),
                campaignKind = workflowState.CampaignKind.ToString(),
                awaitingApproval = workflowState.IsAwaitingApproval
                    ? workflowState.Artifacts.AwaitingApproval.ToString()
                    : null,
                validationStalled = workflowState.Artifacts.ValidationStalled,
                validationStallCycleCount = workflowState.Artifacts.ValidationStallCycleCount,
                artifactVersion = workflowState.Artifacts.ArtifactVersion,
                modelGatePassed = workflowState.ModelGatePassed
            });

            aiTools = CampaignAgentToolAliasExpander.Expand(aiTools, toolNameAliasesEnabled, _logger).ToList();

            // Captured before per-phase filtering so each continuation hop can re-filter the full
            // tool surface for the newly advanced phase (the MCP clients/tool lists are listed once).
            var baseTools = aiTools;
            var beforeToolFilter = aiTools.Count;
            aiTools = CampaignWorkflowToolFilter.Apply(aiTools, workflowState, dataWarehouseEnabled);
            _logger.LogDebug(
                "Campaign agent workflow tool filter: {BeforeCount} -> {AfterCount}, phase={Phase}, kind={Kind}",
                beforeToolFilter,
                aiTools.Count,
                workflowState.Phase,
                workflowState.CampaignKind);

            workflowHint = CampaignWorkflowPromptFormatter.BuildHint(
                workflowState, CollectToolNames(aiTools));
            BcpArtifactRefresh.Refresh(workflowState);
            promptContext = await _promptComposer.BuildAsync(
                tenantId,
                linkedCampaignId,
                budgetMessages,
                workflowHint,
                workflowState,
                CollectToolNames(aiTools),
                cancellationToken);
            conversationMessages = CampaignAgentTranscriptRules.SanitizeChatMessages(
                promptContext.EphemeralMessages.Concat(MapToChatMessages(budgetMessages)));

            var model = _configuration["CampaignAgent:ClaudeModel"]
                        ?? _configuration["CLAUDE_MODEL"]
                        ?? "claude-sonnet-4-6";

            await WriteProgressAsync("prepare_tools", "Preparing tools for this phase…");

            var turnStartDirective = CampaignWorkflowEngine.TryPrepareTurnStartMutatorRetry(
                workflowState, userMessage)
                ?? CampaignWorkflowEngine.TryPrepareTurnStartPatCreation(workflowState)
                ?? CampaignWorkflowEngine.TryPrepareTurnStartJourneyAuthoring(workflowState);
            if (turnStartDirective is not null)
            {
                conversationMessages.Add(new ChatMessage(ChatRole.User, turnStartDirective));
                await _workflowStore.SaveAsync(workflowState, cancellationToken).ConfigureAwait(false);
            }

            // One-hop auto-advance: after a streamed segment whose technical gate advances the phase,
            // run exactly one more segment with the new phase's governance + tools so the agent does
            // the next phase's real work in the same response instead of asking the user to "proceed".
            var continuationHopsUsed = 0;
            var segmentSeqStart = nextSeq + 1;
            AgentMessage? lastPersistedForDoneEvent = null;
            bool shouldContinue;
            do
            {
            workflowState.Artifacts.VerificationToolsInvokedThisSegment = false;
            var phaseAtSegmentStart = workflowState.Phase;
            await WriteProgressAsync("llm_streaming", "Thinking…");
            IChatClient? chatClient = null;
            try
            {
                IChatClient baseChatClient;
                try
                {
                    baseChatClient = CampaignAgentChatClientStackBuilder.Build(
                        _llmChatClientFactory.CreateChatClient(),
                        _logger);
                }
                catch (InvalidOperationException ex)
                {
                    await WriteSseAsync("error", new { requestId, message = ex.Message });
                    return;
                }

                chatClient = baseChatClient;

                var remediationOpts = _configuration
                    .GetSection(BackendToolRemediationOptions.SectionName)
                    .Get<BackendToolRemediationOptions>() ?? new BackendToolRemediationOptions();
                if (remediationOpts.Enabled)
                {
                    chatClient = new BackendToolResultEnrichmentChatClient(
                        chatClient,
                        Options.Create(remediationOpts),
                        _loggerFactory.CreateLogger<BackendToolResultEnrichmentChatClient>());
                }

                if (_configuration.GetValue("CampaignAgent:ToolAudit:Enabled", false))
                {
                    var rootPrefix = _configuration["CampaignAgent:ToolAudit:RootPrefix"] ?? "campaign-agent-tool-audit";
                    var maxPreview = _configuration.GetValue("CampaignAgent:ToolAudit:MaxPreviewChars", 4000);
                    var auditScope = new CampaignAgentToolAuditScope(
                        enabled: true,
                        rootPrefix: rootPrefix,
                        tenantId: tenantId,
                        ownerUserId: userId,
                        conversationId: effectiveConversationId,
                        requestId: requestId,
                        modelId: model,
                        auditDayUtc: DateTime.UtcNow,
                        maxPreviewChars: maxPreview);
                    var auditLogger = _loggerFactory.CreateLogger<CampaignAgentToolAuditChatClient>();
                    chatClient = new CampaignAgentToolAuditChatClient(chatClient, _toolAuditSink, auditScope, auditLogger);
                }

                var maxOut = _configuration.GetValue<int?>("CampaignAgent:MaxOutputTokens");
                if (maxOut is < 1)
                    maxOut = null;
                if (maxOut.HasValue)
                    maxOut = Math.Clamp(maxOut.Value, 256, 128_000);

                // When false, the model may only request one tool invocation per model response (stops parallel duplicate UpsertCampaign batches).
                var allowMultiTool = _configuration.GetValue("CampaignAgent:AllowMultipleToolCallsPerModelResponse", true);

                var chatTemplate = new ChatOptions
                {
                    Tools = aiTools,
                    MaxOutputTokens = maxOut,
                    AllowMultipleToolCalls = allowMultiTool
                };
                OllamaToolRequire.Apply(
                    chatTemplate,
                    CampaignAgentLlmProvider.Resolve(_configuration),
                    workflowState.Phase);
                var mappedPrompt = _llmPromptChatMapper.Map(
                    promptContext.PromptPlan,
                    "CampaignAgent",
                    chatTemplate,
                    conversationMessages);

                var assistantText = new StringBuilder();
                var streamedUpdates = new List<ChatResponseUpdate>();
                var llmSw = Stopwatch.StartNew();
                await foreach (var update in chatClient
                                   .GetStreamingResponseAsync(mappedPrompt.Messages, mappedPrompt.ChatOptions, cancellationToken)
                                   .ConfigureAwait(false))
                {
                    metrics?.ObserveStreamingUpdate(update);
                    streamedUpdates.Add(update.Clone());
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        assistantText.Append(update.Text);
                        await WriteSseAsync("delta", new { requestId, text = update.Text });
                    }
                }
                metrics?.AddLlm(llmSw.ElapsedMilliseconds);

                if (streamedUpdates.Count > 0)
                {
                    var extracted = CampaignWorkflowStreamExtractor.ExtractFromStreamedUpdates(streamedUpdates);
                    if (extracted.Count > 0)
                    {
                        workflowState = CampaignWorkflowEngine.ApplyToolResults(workflowState, extracted, dataWarehouseEnabled);
                        if (!CampaignWorkflowEngine.IsPatBoundaryActive(workflowState)
                            && CampaignWorkflowEngine.ShouldRunEventModelPrep(workflowState))
                        {
                            var midTurnPrepChanged = await TryPrepareEventModelSelectionAsync(
                                workflowState,
                                backendMcpClient,
                                aiTools,
                                tenantId,
                                cancellationToken).ConfigureAwait(false);
                            if (midTurnPrepChanged)
                                await _workflowStore.SaveAsync(workflowState, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            JourneyPatternPrepResolver.TryPrepare(workflowState);
                            await _workflowStore.SaveAsync(workflowState, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                var assistantSeq = segmentSeqStart;
                var wroteTranscriptRowsThisTurn = false;
                try
                {
                    if (streamedUpdates.Count > 0)
                    {
                        var completion = streamedUpdates.ToChatResponse();
                        completion = EnsureNonEmptyCompletionFromStreamedUpdates(streamedUpdates, completion);
                        completion = MergeMissingFunctionResultsFromStreamedUpdates(streamedUpdates, completion);
                        if (completion.Messages is { Count: > 0 } completionMessages)
                        {
                            EnrichPhaseHintsOnCompletionMessages(completionMessages, workflowState.Phase);
                            lastPersistedForDoneEvent = await PersistCompletionMessagesAsync(
                                tenantId,
                                userId,
                                effectiveConversationId,
                                assistantSeq,
                                linkedCampaignId,
                                completionMessages,
                                () => wroteTranscriptRowsThisTurn = true,
                                metrics,
                                maxPersistedToolResultChars,
                                workflowState.Phase,
                                maxPersistedAssistantNarrativeChars);
                        }
                    }

                    if (lastPersistedForDoneEvent is null)
                    {
                        var hadTooling = StreamedUpdatesContainFunctionTooling(streamedUpdates);
                        if (streamedUpdates.Count > 0 && hadTooling)
                        {
                            _logger.LogWarning(
                                "Campaign agent streamed tool/function content but no transcript rows were persisted; attempting repair from streamed updates. RequestId={RequestId}, ConversationId={ConversationId}",
                                requestId,
                                effectiveConversationId);
                            try
                            {
                                var repaired = streamedUpdates.ToChatResponse();
                                repaired = EnsureNonEmptyCompletionFromStreamedUpdates(streamedUpdates, repaired);
                                repaired = MergeMissingFunctionResultsFromStreamedUpdates(streamedUpdates, repaired);
                                if (repaired.Messages is { Count: > 0 } repairedMessages)
                                {
                                    lastPersistedForDoneEvent = await PersistCompletionMessagesAsync(
                                        tenantId,
                                        userId,
                                        effectiveConversationId,
                                        assistantSeq,
                                        linkedCampaignId,
                                        repairedMessages,
                                        () => wroteTranscriptRowsThisTurn = true,
                                        metrics,
                                        maxPersistedToolResultChars,
                                        workflowState.Phase,
                                        maxPersistedAssistantNarrativeChars);
                                }
                            }
                            catch (Exception repairEx)
                            {
                                _logger.LogError(
                                    repairEx,
                                    "Repair persistence from streamed updates failed. RequestId={RequestId}, ConversationId={ConversationId}",
                                    requestId,
                                    effectiveConversationId);
                            }
                        }

                        if (lastPersistedForDoneEvent is null)
                        {
                            if (hadTooling && wroteTranscriptRowsThisTurn)
                            {
                                _logger.LogError(
                                    "Campaign agent could not complete transcript persistence after tool activity; refusing plain-text fallback. RequestId={RequestId}, ConversationId={ConversationId}",
                                    requestId,
                                    effectiveConversationId);
                                await WriteSseAsync("error", new
                                {
                                    requestId,
                                    message = "Failed to persist tool transcript for this turn. Start a new conversation or retry."
                                });
                                return;
                            }

                            if (hadTooling)
                            {
                                _logger.LogWarning(
                                    "Campaign agent using plain assistant text persistence after tool activity (MEAI rows still unavailable). RequestId={RequestId}, ConversationId={ConversationId}",
                                    requestId,
                                    effectiveConversationId);
                            }

                            var fallback = new AgentMessage(
                                tenantId,
                                userId,
                                effectiveConversationId,
                                assistantSeq,
                                "assistant",
                                assistantText.ToString(),
                                linkedCampaignId,
                                null, null, null, null);
                            await _messageAdapter.AppendMessageAsync(tenantId, fallback);
                            lastPersistedForDoneEvent = fallback;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to persist MEAI transcript; attempting recovery. RequestId={RequestId}, ConversationId={ConversationId}",
                        requestId,
                        effectiveConversationId);

                    var hadTooling = StreamedUpdatesContainFunctionTooling(streamedUpdates);
                    if (streamedUpdates.Count > 0 && hadTooling)
                    {
                        try
                        {
                            var repaired = streamedUpdates.ToChatResponse();
                            repaired = EnsureNonEmptyCompletionFromStreamedUpdates(streamedUpdates, repaired);
                            repaired = MergeMissingFunctionResultsFromStreamedUpdates(streamedUpdates, repaired);
                            if (repaired.Messages is { Count: > 0 } recoveredMessages)
                            {
                                lastPersistedForDoneEvent = await PersistCompletionMessagesAsync(
                                    tenantId,
                                    userId,
                                    effectiveConversationId,
                                    assistantSeq,
                                    linkedCampaignId,
                                    recoveredMessages,
                                    () => wroteTranscriptRowsThisTurn = true,
                                    metrics,
                                    maxPersistedToolResultChars,
                                    workflowState.Phase,
                                    maxPersistedAssistantNarrativeChars);
                            }
                        }
                        catch (Exception repairEx)
                        {
                            _logger.LogError(
                                repairEx,
                                "Secondary MEAI transcript persist failed after primary failure. RequestId={RequestId}, ConversationId={ConversationId}",
                                requestId,
                                effectiveConversationId);
                        }
                    }

                    if (lastPersistedForDoneEvent is null)
                    {
                        if (hadTooling)
                        {
                            _logger.LogWarning(
                                "Campaign agent using plain assistant text persistence after primary persist exception and failed recovery. RequestId={RequestId}, ConversationId={ConversationId}",
                                requestId,
                                effectiveConversationId);
                        }

                        var fallback = new AgentMessage(
                            tenantId,
                            userId,
                            effectiveConversationId,
                            assistantSeq,
                            "assistant",
                            assistantText.ToString(),
                            linkedCampaignId,
                            null, null, null, null);
                        await _messageAdapter.AppendMessageAsync(tenantId, fallback);
                        lastPersistedForDoneEvent = fallback;
                    }
                }

                if (PatGateStallCapture.TryCaptureFromAssistantText(workflowState, assistantText.ToString()))
                    await _workflowStore.SaveAsync(workflowState, cancellationToken).ConfigureAwait(false);

            }
            finally
            {
                (chatClient as IDisposable)?.Dispose();
            }

            CampaignJourneyDeliveryGuard.TrySetJourneyEntryCheckpoint(workflowState, userMessage);

            if (CampaignJourneyDeliveryGuard.Enabled
                && workflowState.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.Journey
                && !workflowState.Artifacts.JourneyEntryCheckpointShownThisTurn)
            {
                workflowState.Artifacts.JourneyEntryCheckpointShownThisTurn = true;
                var checkpointLine = CampaignJourneyDeliveryGuard.BuildCheckpointPromptLine(workflowState);
                await WriteProgressAsync("journey_checkpoint", "Journey checkpoint", checkpointLine);
                _logger.LogInformation(
                    "Campaign agent journey checkpoint: conversation {ConversationId}",
                    effectiveConversationId);
            }

            shouldContinue = CampaignWorkflowEngine.ShouldContinueAfterSegment(
                workflowState, continuationHopsUsed);
            if (shouldContinue)
            {
                continuationHopsUsed++;
                if (workflowState.Artifacts.EventModelsGateJustCleared
                    && PointAccountManifestBuilder.Parse(workflowState.Artifacts.PointAccountManifest).Items.Count > 0)
                    workflowState.Artifacts.DeferredMutatorRetry = false;
                segmentSeqStart = lastPersistedForDoneEvent!.Sequence + 1;
                _logger.LogInformation(
                    "Campaign agent auto-advance continuation hop: {FromPhase} -> {ToPhase}, conversation {ConversationId}",
                    phaseAtSegmentStart,
                    workflowState.Phase,
                    effectiveConversationId);

                if (!CampaignWorkflowEngine.IsPatBoundaryActive(workflowState)
                    && CampaignWorkflowEngine.ShouldRunEventModelPrep(workflowState))
                {
                    var hopPrepChanged = await TryPrepareEventModelSelectionAsync(
                        workflowState,
                        backendMcpClient,
                        aiTools,
                        tenantId,
                        cancellationToken).ConfigureAwait(false);
                    if (hopPrepChanged)
                        await _workflowStore.SaveAsync(workflowState, cancellationToken).ConfigureAwait(false);
                }

                // Rebuild the prompt + tool surface for the newly advanced phase. Reloading persisted
                // messages lets the continuation segment see this turn's assistant/tool transcript.
                var reloadedMessages = (await _messageAdapter
                    .ListMessagesAsync(tenantId, userId, effectiveConversationId, 500, cancellationToken)).ToList();
                budgetMessages = CampaignAgentTokenBudget.Apply(reloadedMessages, ReadHistoryBudgetOptions(workflowState));
                workflowHint = CampaignWorkflowPromptFormatter.BuildHint(
                    workflowState, CollectToolNames(aiTools));
                BcpArtifactRefresh.Refresh(workflowState);
                promptContext = await _promptComposer.BuildAsync(
                    tenantId,
                    linkedCampaignId,
                    budgetMessages,
                    workflowHint,
                    workflowState,
                    CollectToolNames(aiTools),
                    cancellationToken);
                conversationMessages = CampaignAgentTranscriptRules.SanitizeChatMessages(
                    promptContext.EphemeralMessages.Concat(MapToChatMessages(budgetMessages)));
                // The persisted transcript ends on this turn's assistant/tool rows; append an
                // ephemeral (non-persisted) user directive so the model generates a fresh segment and
                // continues the now-current phase without waiting for the user to say "proceed".
                conversationMessages.Add(new ChatMessage(
                    ChatRole.User,
                    CampaignWorkflowEngine.BuildContinuationUserDirective(workflowState)));

                await WriteSseAsync(
                    "mcp",
                    new
                    {
                        requestId,
                        journeysToolCount = journeysTools.Count,
                        journeysToolNames = FormatCampaignAgentToolNames(journeysTools),
                        backendMcpUrlConfigured = backendUrlOk,
                        mcpBackendToolCount,
                        backendToolNames = mcpBackendMcpError is not null ? null : mcpBackendToolNames,
                        totalToolCount = baseTools.Count,
                        allToolNames = FormatCampaignAgentToolNames(baseTools),
                        backendMcpError = mcpBackendMcpError,
                        mcpHint,
                        allowBackendSaveModel = _configuration.GetValue("CampaignAgent:AllowBackendSaveModel", true),
                        exposeFullBackendMcpToolSurface = _configuration.GetValue("CampaignAgent:ExposeFullBackendMcpToolSurface", false),
                        workflowPhase = workflowState.Phase.ToString(),
                        workflowCampaignKind = workflowState.CampaignKind.ToString()
                    });

                await WriteSseAsync("workflow", new
                {
                    requestId,
                    phase = workflowState.Phase.ToString(),
                    campaignKind = workflowState.CampaignKind.ToString(),
                    awaitingApproval = workflowState.IsAwaitingApproval
                        ? workflowState.Artifacts.AwaitingApproval.ToString()
                        : null,
                    validationStalled = workflowState.Artifacts.ValidationStalled,
                    validationStallCycleCount = workflowState.Artifacts.ValidationStallCycleCount,
                    artifactVersion = workflowState.Artifacts.ArtifactVersion,
                    modelGatePassed = workflowState.ModelGatePassed
                });

                // Catalog tools in baseTools were already wrapped with the model-catalog digest at the initial
                // tool build (retain spec computed once from the full conversation history). The digest wrapper's
                // double-wrap guard makes re-wrapping here a no-op, and the retain spec is intentionally stable for
                // the whole request. A model first referenced mid-turn is still listed in the catalog digest and
                // fetchable in full via get_model, so it does not need to be force-retained until the next turn.
                aiTools = CampaignWorkflowToolFilter.Apply(baseTools, workflowState, dataWarehouseEnabled);
            }
            }
            while (shouldContinue);

            workflowState.Artifacts.BriefGateJustCleared = false;
            workflowState.Artifacts.EventModelsGateJustCleared = false;
            workflowState.Artifacts.VerificationUserTestIntentThisTurn = false;
            workflowState.Artifacts.VerificationDebugSteeringThisTurn = false;
            workflowState.Artifacts.JourneyEntryCheckpointShownThisTurn = false;
            var workflowSaveSw = Stopwatch.StartNew();
            await _workflowStore.SaveAsync(workflowState, cancellationToken).ConfigureAwait(false);
            metrics?.AddWorkflowSave(workflowSaveSw.ElapsedMilliseconds);

            if (persistMetrics && metrics != null)
            {
                var metricsRow = workflowState.ToWorkflowRow();
                metricsRow.TurnMetricsJson = metrics.BuildJson(requestId);
                await _messageAdapter.UpsertWorkflowOrchestrationRowAsync(tenantId, metricsRow, cancellationToken)
                    .ConfigureAwait(false);
            }

            await WriteProgressAsync("turn_complete", "Finishing up…");
            await WriteSseAsync("done", new { requestId, conversationId = effectiveConversationId, assistantMessageId = lastPersistedForDoneEvent!.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Campaign agent turn failed for conversation {ConversationId}", effectiveConversationId);
            try
            {
                var turnError = FormatCampaignAgentTurnError(ex);
                await WriteSseAsync("error", new { requestId, code = turnError.Code, message = turnError.Text });
            }
            catch
            {
                // ignore secondary failures
            }
        }
        finally
        {
            if (backendMcpClient != null)
                await backendMcpClient.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// On entering the EventModels phase, resolves earning intent from the design brief, fetches and
    /// ranks existing eventable models, stores the candidate set, and (when candidates exist) raises the
    /// EventModelSelection approval gate so the agent presents reuse options before creating a new model.
    /// Returns true when it mutated workflow state (so the caller rebuilds the prompt). Fail-open: any
    /// fetch problem stores a fetch-failed set without gating, letting the agent fall back to prose.
    /// </summary>
    private async Task<bool> TryPrepareEventModelSelectionAsync(
        CampaignWorkflowState state,
        McpClient? backendMcpClient,
        IReadOnlyList<AITool> aiTools,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (backendMcpClient is null)
            return false;
        if (!CampaignWorkflowEngine.ShouldRunEventModelPrep(state)
            && state.Phase != CampaignWorkflowPhase.EventModels)
            return false;
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return false;
        if (state.UserSkippedEventModels || state.CampaignKind == CampaignWorkflowKind.TagFirst)
            return false;
        if (state.ModelGatePassed)
            return false;
        if (state.Artifacts.AwaitingApproval != CampaignWorkflowApprovalKind.None)
            return false;
        if (!string.IsNullOrWhiteSpace(state.Artifacts.SelectedEventModelId))
            return false;
        if (!EventModelCandidatesCatalogFallback.ShouldAttemptMcpCatalogFetch(state))
            return false;

        var brief = !string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefApproved)
            ? state.Artifacts.CampaignDesignBriefApproved
            : state.Artifacts.CampaignDesignBriefProposed;
        state.Artifacts.EarningIntent = EarningIntentResolver.Resolve(brief);

        var models = await EventModelCatalogFetcher
            .FetchAsync(backendMcpClient, aiTools, tenantId, _logger, cancellationToken)
            .ConfigureAwait(false);

        if (models is null)
        {
            EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet { FetchFailed = true });
            _logger.LogInformation(
                "EventModels pre-step: catalog fetch failed for tenant {TenantId}; degrading to prose (no gate).",
                tenantId);
            return true;
        }

        var set = EventModelRanker.Rank(models);
        EventModelCandidatesArtifact.Write(state, set);

        if (EventModelPrepResolver.ShouldAttemptAutoResolve(state))
        {
            var gateOpened = await EventModelPrepResolver.TryAutoResolveStrongMatchAsync(
                state,
                backendMcpClient,
                aiTools,
                tenantId,
                _logger,
                cancellationToken).ConfigureAwait(false);

            if (gateOpened)
            {
                _logger.LogInformation(
                    "EventModels pre-step: strong-match auto-resolve opened gate for {ModelId}.",
                    set.RecommendedDefaultId);
                state.Phase = CampaignWorkflowFocusResolver.CoachDefaultFocus(state);
                return true;
            }
        }

        if (set.Candidates.Count > 0 && !state.Artifacts.UserRequestedNewEventModel)
        {
            state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;
            _logger.LogInformation(
                "EventModels pre-step: {Count} candidate(s), recommendedDefault={Default}, intent={Intent}; raised EventModelSelection gate.",
                set.Candidates.Count,
                set.RecommendedDefaultId ?? "(none)",
                state.Artifacts.EarningIntent);
        }
        else if (set.Candidates.Count > 0 && state.Artifacts.UserRequestedNewEventModel)
        {
            _logger.LogInformation(
                "EventModels pre-step: {Count} candidate(s) found but user requested a new event model; SaveModel remains available (no selection gate).",
                set.Candidates.Count);
        }
        else
        {
            _logger.LogInformation(
                "EventModels pre-step: no eventable candidates found for tenant {TenantId}; no gate (agent may create one).",
                tenantId);
        }

        state.Phase = CampaignWorkflowFocusResolver.CoachDefaultFocus(state);
        return true;
    }

    private static List<ChatMessage> MapToChatMessages(IReadOnlyList<AgentMessage> messages)
    {
        var result = new List<ChatMessage>();
        foreach (var m in messages.OrderBy(x => x.Sequence))
        {
            if (string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                result.Add(new ChatMessage(ChatRole.User, m.Content ?? string.Empty));
            else if (string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                if (CampaignAgentMeaiTranscriptCodec.IsMeaiTranscriptEnvelope(m.Content))
                {
                    try
                    {
                        result.Add(CampaignAgentMeaiTranscriptCodec.DeserializeTranscriptMessage(m.Content!));
                    }
                    catch (Exception)
                    {
                        result.Add(new ChatMessage(ChatRole.Assistant, m.Content ?? string.Empty));
                    }
                }
                else
                    result.Add(new ChatMessage(ChatRole.Assistant, m.Content ?? string.Empty));
            }
            else if (string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(m.ToolCallId))
                    continue;
                if (CampaignAgentTranscriptRules.CallIdAlreadyHasFunctionResult(result, m.ToolCallId))
                    continue;
                try
                {
                    result.Add(CampaignAgentMeaiTranscriptCodec.DeserializeToolRow(m.ToolCallId, m.ToolResultJson));
                }
                catch
                {
                    // Anthropic rejects histories where tool_use is not followed by tool_result; never drop the row.
                    result.Add(new ChatMessage(ChatRole.Tool, new List<AIContent>
                    {
                        new FunctionResultContent(m.ToolCallId,
                            new { error = "Malformed persisted tool result row; could not deserialize for replay." })
                    }));
                }
            }
        }

        EnsureToolResultsFollowAssistantToolCalls(result);
        return result;
    }

    /// <summary>
    /// Anthropic requires each <c>tool_use</c> to be followed by a <c>tool_result</c> before the next user
    /// or assistant message. Repair gaps caused by missing/malformed persisted tool rows or pruning bugs.
    /// </summary>
    private static void EnsureToolResultsFollowAssistantToolCalls(List<ChatMessage> messages)
    {
        for (var i = 0; i < messages.Count; i++)
        {
            if (messages[i].Role != ChatRole.Assistant)
                continue;

            var needed = messages[i].Contents
                .OfType<FunctionCallContent>()
                .Where(f => !f.InformationalOnly && !string.IsNullOrEmpty(f.CallId))
                .Select(f => f.CallId!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (needed.Count == 0)
                continue;

            var found = new HashSet<string>(StringComparer.Ordinal);

            // MEAI transcript envelopes persist tool calls and tool results in the same assistant row
            // (see CampaignAgentMeaiTranscriptCodec). Replay yields one ChatMessage with both FunctionCallContent
            // and FunctionResultContent — do not treat those as missing separate ChatRole.Tool rows.
            foreach (var c in messages[i].Contents)
            {
                if (c is FunctionResultContent fr && !string.IsNullOrEmpty(fr.CallId))
                    found.Add(fr.CallId);
            }

            var j = i + 1;
            while (j < messages.Count && messages[j].Role == ChatRole.Tool)
            {
                foreach (var c in messages[j].Contents)
                {
                    if (c is FunctionResultContent fr && !string.IsNullOrEmpty(fr.CallId))
                        found.Add(fr.CallId);
                }

                j++;
            }

            var missing = needed.Where(id => !found.Contains(id)).ToList();
            if (missing.Count == 0)
                continue;

            var insertAt = i + 1;
            foreach (var callId in missing)
            {
                messages.Insert(insertAt, new ChatMessage(ChatRole.Tool, new List<AIContent>
                {
                    new FunctionResultContent(callId,
                        new { error = "Missing persisted tool result for this tool_use; synthetic row for API replay." })
                }));
                insertAt++;
            }
        }
    }

    private static bool StreamedUpdatesContainFunctionTooling(IEnumerable<ChatResponseUpdate> updates)
    {
        foreach (var u in updates)
        {
            foreach (var c in u.Contents)
            {
                if (c is FunctionCallContent or FunctionResultContent)
                    return true;
            }
        }

        return false;
    }

    private static Dictionary<string, FunctionResultContent> CollectFunctionResultContentsByCallIdFromUpdates(
        IEnumerable<ChatResponseUpdate> updates)
    {
        var dict = new Dictionary<string, FunctionResultContent>(StringComparer.Ordinal);
        foreach (var u in updates)
        {
            foreach (var c in u.Contents)
            {
                if (c is FunctionResultContent fr && !string.IsNullOrEmpty(fr.CallId))
                    dict[fr.CallId] = fr;
            }
        }

        return dict;
    }

    private static HashSet<string> CollectFunctionResultCallIdsPresentAnywhere(ChatResponse completion)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var msg in completion.Messages)
        {
            foreach (var c in msg.Contents)
            {
                if (c is FunctionResultContent fr && !string.IsNullOrEmpty(fr.CallId))
                    set.Add(fr.CallId);
            }
        }

        return set;
    }

    /// <summary>
    /// <see cref="ChatResponseExtensions.ToChatResponse"/> can yield an empty <see cref="ChatResponse.Messages"/> list
    /// even when streamed updates carry tool payloads. Rebuild a minimal assistant message from raw updates so persistence
    /// can still emit MEAI/tool rows.
    /// </summary>
    private static ChatResponse EnsureNonEmptyCompletionFromStreamedUpdates(
        IReadOnlyList<ChatResponseUpdate> streamedUpdates,
        ChatResponse completion)
    {
        if (completion.Messages.Count > 0 || streamedUpdates.Count == 0)
            return completion;
        if (!StreamedUpdatesContainFunctionTooling(streamedUpdates))
            return completion;

        var contents = new List<AIContent>();
        foreach (var u in streamedUpdates)
        {
            foreach (var c in u.Contents)
                contents.Add(c);
        }

        if (contents.Count == 0)
            return completion;

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents));
    }

    /// <summary>
    /// Merges <see cref="FunctionResultContent"/> from the raw stream into assistant <see cref="ChatMessage"/> instances
    /// when <see cref="ChatResponseExtensions.ToChatResponse"/> omitted results that still exist on streamed updates
    /// (keyed by <c>callId</c>). Avoids persisting assistant MEAI envelopes with dangling tool calls and no matching results.
    /// </summary>
    private static ChatResponse MergeMissingFunctionResultsFromStreamedUpdates(
        IReadOnlyList<ChatResponseUpdate> streamedUpdates,
        ChatResponse completion)
    {
        if (completion.Messages.Count == 0)
            return completion;

        var fromUpdates = CollectFunctionResultContentsByCallIdFromUpdates(streamedUpdates);
        if (fromUpdates.Count == 0)
            return completion;

        var satisfied = CollectFunctionResultCallIdsPresentAnywhere(completion);
        var outMessages = new List<ChatMessage>();
        var changed = false;

        foreach (var msg in completion.Messages)
        {
            if (msg.Role != ChatRole.Assistant)
            {
                outMessages.Add(msg);
                continue;
            }

            var missingCallIds = msg.Contents
                .OfType<FunctionCallContent>()
                .Where(f => !f.InformationalOnly && !string.IsNullOrEmpty(f.CallId))
                .Select(f => f.CallId!)
                .Distinct(StringComparer.Ordinal)
                .Where(id => !satisfied.Contains(id))
                .ToList();
            if (missingCallIds.Count == 0)
            {
                outMessages.Add(msg);
                continue;
            }

            var newContents = new List<AIContent>(msg.Contents);
            var injected = 0;
            foreach (var callId in missingCallIds)
            {
                if (!fromUpdates.TryGetValue(callId, out var fr))
                    continue;
                newContents.Add(fr);
                satisfied.Add(callId);
                injected++;
            }

            if (injected == 0)
            {
                outMessages.Add(msg);
                continue;
            }

            changed = true;
            outMessages.Add(new ChatMessage(ChatRole.Assistant, newContents)
            {
                MessageId = msg.MessageId,
                AuthorName = msg.AuthorName,
                CreatedAt = msg.CreatedAt,
                RawRepresentation = msg.RawRepresentation,
                AdditionalProperties = msg.AdditionalProperties
            });
        }

        if (!changed)
            return completion;

        var rebuiltResponse = new ChatResponse
        {
            ConversationId = completion.ConversationId,
            ModelId = completion.ModelId,
            CreatedAt = completion.CreatedAt,
            FinishReason = completion.FinishReason,
            ResponseId = completion.ResponseId,
            Usage = completion.Usage,
            RawRepresentation = completion.RawRepresentation,
            AdditionalProperties = completion.AdditionalProperties
        };

        foreach (var m in outMessages)
            rebuiltResponse.Messages.Add(m);

        return rebuiltResponse;
    }

    private async Task<AgentMessage?> PersistCompletionMessagesAsync(
        string tenantId,
        string userId,
        string conversationId,
        long startSequence,
        string? linkedCampaignId,
        IEnumerable<ChatMessage> completionMessages,
        Action onTranscriptRowWritten,
        CampaignAgentTurnMetricsCollector? metrics = null,
        int maxPersistedToolResultChars = ToolResultHistoryStub.DefaultMaxPersistedChars,
        CampaignWorkflowPhase? workflowPhase = null,
        int maxPersistedAssistantNarrativeChars = CampaignAgentTranscriptPersistence.DefaultMaxPersistedAssistantNarrativeChars)
    {
        var persistSw = Stopwatch.StartNew();
        var satisfiedCallIds = new HashSet<string>(StringComparer.Ordinal);
        long seq = startSequence;
        AgentMessage? last = null;
        foreach (var msg in completionMessages)
        {
            foreach (var row in CampaignAgentTranscriptPersistence.BuildPersistenceRowsFromCompletionMessage(
                         tenantId,
                         userId,
                         conversationId,
                         ref seq,
                         linkedCampaignId,
                         satisfiedCallIds,
                         msg,
                         metrics,
                         maxPersistedToolResultChars,
                         workflowPhase,
                         maxPersistedAssistantNarrativeChars))
            {
                await _messageAdapter.AppendMessageAsync(tenantId, row);
                onTranscriptRowWritten();
                last = row;
            }
        }

        metrics?.AddPersist(persistSw.ElapsedMilliseconds);
        return last;
    }

    private static void EnrichPhaseHintsOnCompletionMessages(
        IList<ChatMessage> messages,
        CampaignWorkflowPhase phase)
    {
        var callIdToName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var msg in messages)
        {
            if (msg.Role != ChatRole.Assistant)
                continue;

            foreach (var c in msg.Contents)
            {
                if (c is FunctionCallContent fc && !string.IsNullOrEmpty(fc.CallId))
                    callIdToName[fc.CallId] = fc.Name ?? string.Empty;
            }
        }

        foreach (var msg in messages)
        {
            if (msg.Role != ChatRole.Tool)
                continue;

            for (var i = 0; i < msg.Contents.Count; i++)
            {
                if (msg.Contents[i] is not FunctionResultContent fr || string.IsNullOrEmpty(fr.CallId))
                    continue;

                callIdToName.TryGetValue(fr.CallId, out var toolName);
                var raw = fr.Result switch
                {
                    null => string.Empty,
                    string s => s,
                    _ => JsonSerializer.Serialize(fr.Result, JsonOpts)
                };

                var enriched = CampaignWorkflowToolPhaseHints.EnrichToolResultJson(phase, toolName, raw);
                if (!string.Equals(enriched, raw, StringComparison.Ordinal))
                    msg.Contents[i] = new FunctionResultContent(fr.CallId, enriched);
            }
        }
    }

    private static bool ShouldLoadTenantVerificationContext(CampaignWorkflowState state, string? userMessage) =>
        VerificationDeliveryGuard.ShouldSurfaceVerificationCoach(state)
        || WorkflowUserPhraseCatalog.ContainsAny(userMessage ?? "", WorkflowUserPhraseCatalog.JourneyVerificationIntentPhrases)
        || (CreationSnapshotArtifact.IsCreationComplete(state)
            && state.CampaignKind == CampaignWorkflowKind.EventDriven
            && string.IsNullOrWhiteSpace(state.Artifacts.TenantTestAccountAllowlistJson));

    private HistoryBudgetOptions ReadHistoryBudgetOptions(CampaignWorkflowState? workflowState = null) =>
        new()
        {
            PinFirstUserSegment = _configuration.GetValue("CampaignAgent:PinSalientUserSegment", true),
            PinMostRecentCompletedSegment = _configuration.GetValue("CampaignAgent:PinMostRecentCompletedSegment", true),
            ShrinkToolResults = _configuration.GetValue("CampaignAgent:HistoryShrinkToolResults", true),
            ShrinkThresholdChars = _configuration.GetValue("CampaignAgent:HistoryShrinkToolResultsThresholdChars", 2048),
            MaxChars = _configuration.GetValue("CampaignAgent:HistoryMaxChars", CampaignAgentTokenBudget.DefaultMaxChars),
            MaxUserTurns = _configuration.GetValue("CampaignAgent:HistoryMaxUserTurns", CampaignAgentTokenBudget.DefaultMaxUserTurns),
            JourneyContractSummaryPinActive = workflowState?.Artifacts.JourneyContractSummaryPinActive == true
        };

    private int ReadMaxPersistedToolResultChars() =>
        _configuration.GetValue("CampaignAgent:MaxPersistedToolResultChars", ToolResultHistoryStub.DefaultMaxPersistedChars);

    private int ReadMaxPersistedAssistantNarrativeChars() =>
        _configuration.GetValue(
            "CampaignAgent:MaxPersistedAssistantNarrativeChars",
            CampaignAgentTranscriptPersistence.DefaultMaxPersistedAssistantNarrativeChars);
}
