using System.Linq;
using Anthropic;
using Journeys.CampaignAgent.Remediation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Journeys.Agent;

/// <summary>
/// Journeys console: Claude + Journeys.API MCP (campaigns, events, …) and optionally BackEnd.Web MCP (dynamic models).
/// </summary>
internal static class Program
{
    public static async Task<int> Main()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddUserSecrets<AgentSecretsAnchor>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        using var logFactory = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(LogLevel.Information);
            b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
        });
        var log = logFactory.CreateLogger("Journeys.Agent");

        var mcpServerUrl = ResolveJourneysMcpUrl(config);
        if (string.IsNullOrEmpty(mcpServerUrl))
        {
            log.LogError("Set JourneysMcpServerUrl, Journeys__McpServerUrl, or environment Journeys_MCP_SERVER_URL (e.g. https://localhost:7001/mcp for Journeys.API).");
            return 1;
        }

        var backendMcpUrl = ResolveBackendMcpUrl(config);
        var useBackendMcp = !string.IsNullOrWhiteSpace(backendMcpUrl);

        var apiKey = config["ANTHROPIC_API_KEY"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var model = config["ClaudeModel"] ?? Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "claude-sonnet-4-6";

        var allowProcess = config.GetValue("AllowProcessEvent", false);
        var allowMove = config.GetValue("AllowMoveTier", false);

        var allowSaveModel = config.GetValue("AllowSaveModel", false);
        var allowDeleteModel = config.GetValue("AllowDeleteModel", false);
        var allowSetEntity = config.GetValue("AllowSetEntity", false);
        var allowMoveEntity = config.GetValue("AllowMoveEntity", false);
        var allowSaveRootTaxonomy = ConfigBoolDefaultTrue(config, "AllowSaveRootTaxonomy");
        var allowSaveTaxonomy = ConfigBoolDefaultTrue(config, "AllowSaveTaxonomy");
        var allowBulkUpsertTaxonomies = ConfigBoolDefaultTrue(config, "AllowBulkUpsertTaxonomies");

        if (useBackendMcp)
        {
            log.LogInformation(
                "Backend MCP gates (env/user-secrets override JSON): AllowSaveModel={Save}, AllowDeleteModel={Delete}, AllowSetEntity={Se}, AllowMoveEntity={Move}. When AllowSaveModel is false, SaveModel/save_model are removed before Claude sees tools.",
                allowSaveModel,
                allowDeleteModel,
                allowSetEntity,
                allowMoveEntity);
        }

        var bypassJourneysCert = config.GetValue("BypassJourneysMcpServerCertificateValidation", false);
        var bypassBackendCert = config.GetValue("BypassBackendMcpServerCertificateValidation", false);
        var maxConnectAttempts = Math.Max(1, config.GetValue("McpConnectMaxAttempts", 30));
        var retryDelaySeconds = Math.Max(1, config.GetValue("McpConnectRetryDelaySeconds", 5));
        var initialDelaySeconds = Math.Max(0, config.GetValue("McpConnectInitialDelaySeconds", 0));
        if (initialDelaySeconds > 0)
        {
            log.LogInformation(
                "Waiting {Seconds}s before first MCP connect (McpConnectInitialDelaySeconds)…",
                initialDelaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds)).ConfigureAwait(false);
        }

        using var httpJourneys = CreateMcpHttpClient(bypassJourneysCert);
        var transportJourneys = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(mcpServerUrl.TrimEnd('/')),
                Name = "Journeys MCP Server (Journeys.Agent)"
            },
            httpJourneys);

        var journeysClient = await ConnectMcpAsync(
                log,
                mcpServerUrl,
                transportJourneys,
                maxConnectAttempts,
                retryDelaySeconds,
                "Journeys MCP",
                "Ensure Journeys.API is running; typical URL: https://localhost:7001/mcp.")
            .ConfigureAwait(false);
        if (journeysClient is null)
            return 1;

        McpClient? backendClient = null;
        HttpClient? httpBackend = null;
        if (useBackendMcp)
        {
            httpBackend = CreateMcpHttpClient(bypassBackendCert);
            var transportBackend = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(backendMcpUrl!.TrimEnd('/')),
                    Name = "Backend MCP Server (BackEnd.Web via Journeys.Agent)"
                },
                httpBackend);

            backendClient = await ConnectMcpAsync(
                    log,
                    backendMcpUrl,
                    transportBackend,
                    maxConnectAttempts,
                    retryDelaySeconds,
                    "Backend MCP",
                    "Ensure BackEnd.Web is running; typical URL: https://localhost:7155/mcp.")
                .ConfigureAwait(false);
            if (backendClient is null)
            {
                await journeysClient.DisposeAsync().ConfigureAwait(false);
                httpBackend.Dispose();
                return 1;
            }
        }

        await using (journeysClient)
        {
            if (backendClient is not null)
                await using (backendClient)
                {
                    return await RunAgentSessionAsync(
                        config,
                        log,
                        logFactory,
                        apiKey,
                        model,
                        journeysClient,
                        backendClient,
                        allowProcess,
                        allowMove,
                        allowSaveModel,
                        allowDeleteModel,
                        allowSetEntity,
                        allowMoveEntity,
                        allowSaveRootTaxonomy,
                        allowSaveTaxonomy,
                        allowBulkUpsertTaxonomies).ConfigureAwait(false);
                }

            return await RunAgentSessionAsync(
                config,
                log,
                logFactory,
                apiKey,
                model,
                journeysClient,
                backendClient: null,
                allowProcess,
                allowMove,
                allowSaveModel,
                allowDeleteModel,
                allowSetEntity,
                allowMoveEntity,
                allowSaveRootTaxonomy,
                allowSaveTaxonomy,
                allowBulkUpsertTaxonomies).ConfigureAwait(false);
        }
    }

    private static async Task<int> RunAgentSessionAsync(
        IConfiguration config,
        ILogger log,
        ILoggerFactory loggerFactory,
        string? apiKey,
        string model,
        McpClient journeysClient,
        McpClient? backendClient,
        bool allowProcess,
        bool allowMove,
        bool allowSaveModel,
        bool allowDeleteModel,
        bool allowSetEntity,
        bool allowMoveEntity,
        bool allowSaveRootTaxonomy,
        bool allowSaveTaxonomy,
        bool allowBulkUpsertTaxonomies)
    {
        IList<McpClientTool> journeysToolsRaw;
        try
        {
            journeysToolsRaw = await journeysClient.ListToolsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "ListTools failed for Journeys MCP.");
            return 1;
        }

        IList<McpClientTool>? backendToolsRaw = null;
        if (backendClient is not null)
        {
            try
            {
                backendToolsRaw = await backendClient.ListToolsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "ListTools failed for Backend MCP.");
                return 1;
            }
        }

        var journeysFiltered = FilterJourneysTools(config, journeysToolsRaw, allowProcess, allowMove, log);
        List<McpClientTool> merged;
        if (backendClient is not null && backendToolsRaw is not null)
        {
            var backendFiltered = FilterBackendTools(
                config,
                backendToolsRaw,
                allowSaveModel,
                allowDeleteModel,
                allowSetEntity,
                allowMoveEntity,
                allowSaveRootTaxonomy,
                allowSaveTaxonomy,
                allowBulkUpsertTaxonomies,
                log);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            merged = new List<McpClientTool>();
            foreach (var t in journeysFiltered.Concat(backendFiltered))
            {
                if (!seen.Add(t.Name))
                {
                    log.LogError("Duplicate MCP tool name across hosts: {Name}. Fix MCP servers or disable one host.", t.Name);
                    return 1;
                }

                merged.Add(t);
            }

            log.LogInformation(
                "Merged MCP tools: {Total} (Journeys: {JourneysCount}, Backend: {BkCount}). Names: {Names}",
                merged.Count,
                journeysFiltered.Count,
                backendFiltered.Count,
                string.Join(", ", merged.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)));
        }
        else
        {
            merged = journeysFiltered.ToList();
            log.LogInformation(
                "Journeys MCP only: {Count} tool(s) (AllowProcessEvent={P}, AllowMoveTier={M}): {Names}",
                merged.Count,
                allowProcess,
                allowMove,
                string.Join(", ", merged.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)));
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            log.LogWarning("ANTHROPIC_API_KEY not set. Tool list above is the set that would be passed to the model. Set the key, then re-run to chat.");
            return 0;
        }

        var systemPrompt = await LoadSystemPromptAsync().ConfigureAwait(false);
        var anthropicClient = new AnthropicClient { ApiKey = apiKey };
        IChatClient chatClient = anthropicClient
            .AsIChatClient(model)
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        if (config.GetValue("BackendToolRemediation:Enabled", true))
        {
            var remediationOpts = new BackendToolRemediationOptions
            {
                Enabled = true,
                MaxEnrichmentsPerTurn = config.GetValue("BackendToolRemediation:MaxEnrichmentsPerTurn", 5),
                ToolAllowlist = config.GetSection("BackendToolRemediation:ToolAllowlist").Get<string[]>()
                    ?? ["SaveModel", "save_model"]
            };
            chatClient = new BackendToolResultEnrichmentChatClient(
                chatClient,
                Options.Create(remediationOpts),
                loggerFactory.CreateLogger<BackendToolResultEnrichmentChatClient>());
        }

        var aiTools = merged.Cast<AITool>().ToList();
        if (aiTools.Count == 0)
        {
            log.LogError("No tools after filters. Check JourneysToolAllowlist / BackendToolAllowlist and MCP servers.");
            return 1;
        }

        var chatOptions = new ChatOptions
        {
            Instructions = systemPrompt,
            Tools = aiTools
        };

        var history = new List<ChatMessage>();
        log.LogInformation(
            "Journeys Agent ready (model: {Model}). Backend MCP: {Bk}. 'reset' clears history, 'exit' quits.",
            model,
            backendClient is not null ? "on" : "off");

        while (true)
        {
            Console.Write("You: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;
            if (input.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                history.Clear();
                log.LogInformation("Conversation history cleared.");
                continue;
            }

            history.Add(new ChatMessage(ChatRole.User, input));
            try
            {
                var response = await chatClient.GetResponseAsync(history, chatOptions).ConfigureAwait(false);
                AppendChatResponseToHistory(history, response);
                var text = response?.Text?.Trim();
                if (string.IsNullOrEmpty(text))
                    Console.WriteLine("Assistant: [No text in response.]");
                else
                    Console.WriteLine($"Assistant: {text}");
            }
            catch (Exception ex)
            {
                if (history.Count > 0 && history[^1].Role == ChatRole.User)
                    history.RemoveAt(history.Count - 1);
                log.LogError("Turn error: {Message}", ex.Message);
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static async Task<McpClient?> ConnectMcpAsync(
        ILogger log,
        string mcpUrl,
        HttpClientTransport transport,
        int maxConnectAttempts,
        int retryDelaySeconds,
        string label,
        string hintWhenDown)
    {
        McpClient? mcpClient = null;
        for (var attempt = 1; attempt <= maxConnectAttempts; attempt++)
        {
            try
            {
                mcpClient = await McpClient.CreateAsync(transport).ConfigureAwait(false);
                if (attempt > 1)
                    log.LogInformation("{Label}: connected at {Url} (attempt {Attempt}).", label, mcpUrl, attempt);
                return mcpClient;
            }
            catch (Exception ex)
            {
                if (attempt == 1)
                {
                    log.LogInformation(
                        "{Label} not ready ({Message}). {Hint} Retrying up to {Max} attempt(s), {Delay}s apart…",
                        label,
                        ex.Message,
                        hintWhenDown,
                        maxConnectAttempts,
                        retryDelaySeconds);
                }
                else
                {
                    log.LogWarning(
                        "{Label} connect failed (attempt {Attempt}/{Max}): {Message}. Retrying in {Delay}s…",
                        label,
                        attempt,
                        maxConnectAttempts,
                        ex.Message,
                        retryDelaySeconds);
                }

                if (attempt == maxConnectAttempts)
                {
                    log.LogError(ex, "{Label}: could not connect to {Url} after {Max} attempts.", label, mcpUrl, maxConnectAttempts);
                    return null;
                }

                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds)).ConfigureAwait(false);
            }
        }

        return null;
    }

    private static void AppendChatResponseToHistory(List<ChatMessage> history, ChatResponse? response)
    {
        if (response is null)
            return;
        if (response.Messages is { Count: > 0 } list)
        {
            foreach (var m in list)
                history.Add(m);
            return;
        }
        if (!string.IsNullOrEmpty(response.Text))
            history.Add(new ChatMessage(ChatRole.Assistant, response.Text));
    }

    private static string? ResolveJourneysMcpUrl(IConfiguration config) =>
        config["Journeys:McpServerUrl"] ?? config["JourneysMcpServerUrl"] ?? config["Journeys__McpServerUrl"]
        ?? Environment.GetEnvironmentVariable("JOURNEYS_MCP_SERVER_URL");

    private static string? ResolveBackendMcpUrl(IConfiguration config) =>
        config["Backend:McpServerUrl"] ?? config["BackendMcpServerUrl"] ?? config["Backend__McpServerUrl"]
        ?? Environment.GetEnvironmentVariable("BACKEND_MCP_SERVER_URL");

    private static HttpClient CreateMcpHttpClient(bool bypassCertificateValidation)
    {
        if (!bypassCertificateValidation)
            return new HttpClient();
        var h = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        return new HttpClient(h);
    }

    private static bool ConfigBoolDefaultTrue(IConfiguration config, string key)
    {
        var raw = config[key];
        return string.IsNullOrWhiteSpace(raw) || (bool.TryParse(raw, out var v) && v);
    }

    /// <summary>JourneysToolAllowlist applies only to Journeys-sourced tools (Backend tools are not removed by this list).</summary>
    private static List<McpClientTool> FilterJourneysTools(
        IConfiguration config,
        IEnumerable<McpClientTool> allTools,
        bool allowProcessEvent,
        bool allowMoveTier,
        ILogger log)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!allowProcessEvent) excluded.Add("ProcessEvent");
        if (!allowMoveTier) excluded.Add("MoveTier");

        var list = allTools
            .Where(t => !excluded.Contains(t.Name))
            .ToList();

        var allowlist = config.GetSection("JourneysToolAllowlist").Get<string[]>();
        if (allowlist is { Length: > 0 })
        {
            var allow = new HashSet<string>(allowlist, StringComparer.OrdinalIgnoreCase);
            var before = list.Count;
            list = list.Where(t => allow.Contains(t.Name)).ToList();
            log.LogInformation("JourneysToolAllowlist: kept {Count} of {Before} Journeys tool(s).", list.Count, before);
        }

        return list;
    }

    /// <summary>Same surface as Backend.Agent: model-catalog tools only, plus mutating gates.</summary>
    private static List<McpClientTool> FilterBackendTools(
        IConfiguration config,
        IEnumerable<McpClientTool> allTools,
        bool allowSaveModel,
        bool allowDeleteModel,
        bool allowSetEntity,
        bool allowMoveEntity,
        bool allowSaveRootTaxonomy,
        bool allowSaveTaxonomy,
        bool allowBulkUpsertTaxonomies,
        ILogger log)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!allowSaveModel)
        {
            excluded.Add("SaveModel");
            excluded.Add("save_model");
        }

        if (!allowDeleteModel)
        {
            excluded.Add("DeleteModel");
            excluded.Add("delete_model");
        }

        if (!allowSetEntity) excluded.Add("SetEntity");
        if (!allowMoveEntity) excluded.Add("MoveEntity");
        if (!allowSaveRootTaxonomy) excluded.Add("SaveRootTaxonomy");
        if (!allowSaveTaxonomy) excluded.Add("SaveTaxonomy");
        if (!allowBulkUpsertTaxonomies) excluded.Add("BulkUpsertTaxonomies");

        // Keep in sync with Journeys.API/CampaignAgent/CampaignAgentBackendMcp.AllowedToolNames (Backend MCP tool names vary by casing).
        var modelToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ListModels", "list_models",
            "GetAllModels", "get_all_models",
            "GetModel", "get_model",
            "GetManyModels", "get_many_models",
            "GetModelAttributesForRules", "get_model_attributes_for_rules",
            "BuildTaxonomicRule", "build_taxonomic_rule",
            "ListExampleModels", "list_example_models",
            "GetExampleModel", "get_example_model",
            "SaveModel", "save_model",
            "DeleteModel", "delete_model"
        };

        var list = allTools
            .Where(t => modelToolNames.Contains(t.Name) && !excluded.Contains(t.Name))
            .ToList();

        var allowlist = config.GetSection("BackendToolAllowlist").Get<string[]>();
        if (allowlist is { Length: > 0 })
        {
            var allow = new HashSet<string>(allowlist, StringComparer.OrdinalIgnoreCase);
            var before = list.Count;
            list = list.Where(t => allow.Contains(t.Name)).ToList();
            log.LogInformation("BackendToolAllowlist: kept {Count} of {Before} Backend tool(s).", list.Count, before);
        }

        return list;
    }

    private sealed class AgentSecretsAnchor
    {
    }

    private static async Task<string> LoadSystemPromptAsync()
    {
        const string defaultPrompt =
            "You are a Journeys assistant. Use the attached tools; require tenantId and do not invent identifiers.";
        var path = Path.Combine(AppContext.BaseDirectory, "AgentSystemPrompt.txt");
        if (File.Exists(path))
            return await File.ReadAllTextAsync(path).ConfigureAwait(false);
        return defaultPrompt;
    }
}
