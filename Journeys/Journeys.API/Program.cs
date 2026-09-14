using Journeys.API.A2a;
using Journeys.API.CampaignAgent;
using Journeys.API.Configuration;
using Journeys.API.Examples;
using Journeys.API.Mcp;
using Journeys.Core.Interfaces.Services;
using Backend.Core.Llm;
using Backend.Llm.Anthropic;
using Journeys.API.Middleware;
using Journeys.Core.Configuration;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Journeys.Infra.Auth;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Microsoft.AspNetCore.Mvc.Authorization;
using Serilog.Events;
using Microsoft.Extensions.Logging;
using Journeys.Core.Services;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});

builder.Host.UseSerilog();
builder.Services.AddHttpClient<AzureServerIdService>();

var journeysLog = builder.Configuration.GetValue<string>("JourneysLog");
var journeysErrorLog = builder.Configuration.GetValue<string>("JourneysErrorLog");
var workspaceId = builder.Configuration.GetValue<string>("Serilog:WriteTo:0:Args:workspaceId");
var authenticationId = builder.Configuration.GetValue<string>("Serilog:WriteTo:0:Args:authenticationId");

if (string.IsNullOrEmpty(workspaceId))
{
    var serilogConfig = builder.Configuration.GetSection("Serilog:WriteTo").GetChildren();
    var azureAnalyticsConfig = serilogConfig.FirstOrDefault(x => x["Name"] == "AzureAnalytics");
    if (azureAnalyticsConfig != null)
    {
        workspaceId = azureAnalyticsConfig.GetSection("Args")["workspaceId"];
        authenticationId = azureAnalyticsConfig.GetSection("Args")["authenticationId"];
    }
}

Console.WriteLine($"[Serilog Config] WorkspaceId: {(string.IsNullOrEmpty(workspaceId) ? "NOT SET" : workspaceId.Substring(0, Math.Min(8, workspaceId.Length)) + "...")}");
Console.WriteLine($"[Serilog Config] AuthenticationId: {(string.IsNullOrEmpty(authenticationId) ? "NOT SET" : "SET (hidden)")}");
Console.WriteLine($"[Serilog Config] JourneysLog: {journeysLog ?? "NOT SET"}");
Console.WriteLine($"[Serilog Config] JourneysErrorLog: {journeysErrorLog ?? "NOT SET"}");

Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine($"[Serilog Internal] {msg}"));

var azureServerIdService = builder.Services.BuildServiceProvider().GetRequiredService<AzureServerIdService>();
var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.With(new Enrichers(new HttpContextAccessor(), azureServerIdService));

var azureAnalyticsEnabled = !string.IsNullOrWhiteSpace(workspaceId)
    && !string.IsNullOrWhiteSpace(authenticationId)
    && !string.IsNullOrWhiteSpace(journeysLog)
    && !string.IsNullOrWhiteSpace(journeysErrorLog);

if (azureAnalyticsEnabled)
{
    loggerConfiguration
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(log => log.Level == LogEventLevel.Error || log.Level == LogEventLevel.Fatal)
            .WriteTo.AzureAnalytics(workspaceId, authenticationId, journeysErrorLog))
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(log => log.Level != LogEventLevel.Error && log.Level != LogEventLevel.Fatal)
            .WriteTo.AzureAnalytics(workspaceId, authenticationId, journeysLog));
}
else
{
    Console.WriteLine("[Serilog Config] Azure Analytics sink skipped (workspaceId or authenticationId not set). Console logging only.");
}

Log.Logger = loggerConfiguration
    .WriteTo.Logger(lc => lc.WriteTo.Console())
    .CreateLogger();

Log.Information("Journeys API Starting - Serilog configured with WorkspaceId: {WorkspaceId}, JourneysLog: {JourneysLog}, JourneysErrorLog: {JourneysErrorLog}",
    workspaceId?.Substring(0, Math.Min(8, workspaceId?.Length ?? 0)) + "...",
    journeysLog,
    journeysErrorLog);

builder.Services.AddControllers(opts =>
{
    opts.Filters.Add(new AuthorizeFilter("RequireLoyaltyAccount"));
})
.AddJsonOptions(jsonOpts =>
    jsonOpts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Journeys API",
        Version = "v1",
        Description = "Journeys API"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<AzureServerIdService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

builder.Services
    .AddDAL(builder.Configuration)
    .AddInfra(builder.Configuration)
    .AddCampaignServices(builder.Configuration)
    .AddAccountServices()
    .AddEventServices()
    .AddExportServices()
    .AddBatchServices(builder.Configuration)
    .AddNotificationsServices(builder.Configuration)
    .AddDashboardRolesServices()
    .AddFileIngestionsServices()
    .AddDatabricksConfigServices(builder.Configuration)
    .AddReportServices();

builder.Services.AddSingleton<JourneysExamplePack>();

builder.Services.AddHttpClient("CampaignAgentMcp")
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var handler = new HttpClientHandler();
        if (cfg.GetValue("CampaignAgent:BypassMcpServerCertificateValidation", false))
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        return handler;
    });
builder.Services.Configure<Journeys.API.CampaignAgent.DataWarehouse.DataWarehouseProxyOptions>(
    builder.Configuration.GetSection(Journeys.API.CampaignAgent.DataWarehouse.DataWarehouseProxyOptions.SectionName));
builder.Services.AddHttpClient<Journeys.API.CampaignAgent.DataWarehouse.IDataWarehouseProxyClient,
    Journeys.API.CampaignAgent.DataWarehouse.DataWarehouseProxyClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<
        Journeys.API.CampaignAgent.DataWarehouse.DataWarehouseProxyOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opts.ProxyBaseUrl))
        client.BaseAddress = new Uri(opts.ProxyBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(opts.TimeoutSeconds, 5, 120));
});

builder.Services.AddHttpClient("CampaignAgentBackendMcp")
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var handler = new HttpClientHandler();
        if (cfg.GetValue("CampaignAgent:BypassBackendMcpServerCertificateValidation", false))
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        return handler;
    });
builder.Services.AddScoped<ICampaignAgentTenantContextProvider, CampaignAgentTenantContextProvider>();
builder.Services.AddScoped<ITenantVerificationContextLoader, TenantVerificationContextLoader>();
builder.Services.AddScoped<ICampaignAgentPromptComposer, CampaignAgentPromptComposer>();
builder.Services.AddScoped<ICampaignWorkflowStore, CampaignWorkflowStore>();
builder.Services.AddSingleton<Channel<CampaignAgentToolAuditBatchItem>>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var capacity = Math.Max(16, cfg.GetValue("CampaignAgent:ToolAudit:ChannelCapacity", 2048));
    return Channel.CreateBounded<CampaignAgentToolAuditBatchItem>(new BoundedChannelOptions(capacity)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });
});
builder.Services.AddSingleton<ChannelReader<CampaignAgentToolAuditBatchItem>>(sp =>
    sp.GetRequiredService<Channel<CampaignAgentToolAuditBatchItem>>().Reader);
builder.Services.AddSingleton<ICampaignAgentToolAuditSink>(sp =>
    new CampaignAgentToolAuditSink(sp.GetRequiredService<Channel<CampaignAgentToolAuditBatchItem>>().Writer));
builder.Services.AddHostedService<CampaignAgentToolAuditBackgroundService>();

builder.Services.AddSingleton<CampaignAgentMcpEndpointsHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<CampaignAgentMcpEndpointsHealthCheck>(
        "campaign_agent_mcp",
        tags: new[] { "ready", "campaign_agent" });
builder.Services.AddHostedService<CampaignAgentMcpStartupValidationHostedService>();

builder.Services.AddSingleton(sp =>
    new AnthropicLlmChatClientFactory(
        sp.GetRequiredService<IConfiguration>(),
        "CampaignAgent",
        sp.GetService<ILogger<AnthropicLlmChatClientFactory>>()));
builder.Services.AddSingleton<ILlmChatClientFactory>(sp =>
    sp.GetRequiredService<AnthropicLlmChatClientFactory>());
builder.Services.AddSingleton<ILlmPromptChatMapper, AnthropicLlmPromptChatMapper>();
builder.Services.AddScoped<ICampaignAgentOrchestrator, CampaignAgentOrchestrator>();
builder.Services.AddScoped<ICampaignAgentDataClearService, CampaignAgentDataClearService>();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<JourneysMcpTools>()
    .WithTools<DataWarehouseMcpTools>()
    .WithTools<CampaignObjectiveMcpTools>()
    .WithTools<JourneysMcpExampleTools>()
    .WithResources<JourneysMcpResources>()
    .WithResources<JourneysMcpExampleResources>();

builder.Services.AddSingleton<A2aTaskMemoryStore>();
builder.Services.AddScoped<JourneysA2aToolDispatcher>(sp =>
    new JourneysA2aToolDispatcher(
        new JourneysMcpTools(
            sp.GetRequiredService<ICampaignService>(),
            sp.GetRequiredService<ICampaignAssistantContextService>(),
            sp.GetRequiredService<ILoyaltyAccountService>(),
            sp.GetRequiredService<IEventService>(),
            sp.GetRequiredService<IFileIngestionService>(),
            sp.GetRequiredService<IRulesService>())));
builder.Services.AddScoped<JourneysA2aService>();

builder.Services.ConfigureAuth(configuration: builder.Configuration, environment: builder.Environment, logger: logger);

var app = builder.Build();

var enableSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("EnableSwagger", false)
    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENABLE_SWAGGER"));

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Journeys API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");

app.Use(async (context, next) =>
{
    if (context.Request.Method == HttpMethods.Options)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        return;
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    var inContainer = bool.TryParse(app.Configuration.GetValue<string>("DOTNET_RUNNING_IN_CONTAINER"), out var result) && result;
    if (!inContainer)
    {
        app.UseHttpsRedirection();
    }
}

app.UseHttpsRedirection();
app.UseMiddleware<RequestLogging>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();
app.MapMcp("/mcp");
app.MapJourneysA2a();

app.Run();
