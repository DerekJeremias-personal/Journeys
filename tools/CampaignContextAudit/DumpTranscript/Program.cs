using CampaignContextAudit.Configuration;
using CampaignContextAudit.Mapping;
using CampaignContextAudit.Transcript;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DAL.Adapters;
using Journeys.Infra.Backend;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var tenant = args.ElementAtOrDefault(0) ?? "primo";
var convId = args.ElementAtOrDefault(1) ?? "23859a471ec84695bc2122b2a601a2fb";
var mode = args.ElementAtOrDefault(2);

if (string.Equals(mode, "--workflow", StringComparison.OrdinalIgnoreCase))
{
    var config = AuditBackendConfiguration.Build();
    AuditBackendConfiguration.Validate(config);
    var services = new ServiceCollection();
    services.AddSingleton(config);
    services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
    services.AddHttpClient();
    services.AddBackend(config);
    services.AddScoped<IAgentMessageAdapter, AgentMessageAdapter>();
    await using var provider = services.BuildServiceProvider();
    var adapter = provider.GetRequiredService<IAgentMessageAdapter>();
    var load = await CosmosTranscriptLoader.LoadAsync(adapter, tenant, convId);
    var wf = load.WorkflowRows.LastOrDefault();
    if (wf is null) { Console.WriteLine("No workflow row"); return; }
    Console.WriteLine($"workflowPhase={wf.WorkflowPhase}");
    Console.WriteLine($"workflowModelGatePassed={wf.WorkflowModelGatePassed}");
    Console.WriteLine($"workflowCampaignKind={wf.WorkflowCampaignKind}");
    Console.WriteLine($"turnMetricsJson={(wf.TurnMetricsJson is null ? "(null)" : wf.TurnMetricsJson[..Math.Min(500, wf.TurnMetricsJson.Length)])}");
    var state = CampaignWorkflowState.FromWorkflowRow(AgentMessageDocMapper.ToAgentMessage(wf, load.OwnerUserId));
    if (state is not null)
    {
        var readiness = EventModelsReadiness.Evaluate(state);
        Console.WriteLine($"eventModelsReady={readiness.IsReady} blockers=[{string.Join(", ", readiness.Blockers)}]");
        Console.WriteLine($"eventModelsGateBlocksMutators={!readiness.IsReady}");
    }
    return;
}

{
var focus = (mode ?? "14,30")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(long.Parse)
    .ToHashSet();

var config = AuditBackendConfiguration.Build();
AuditBackendConfiguration.Validate(config);
var services = new ServiceCollection();
services.AddSingleton(config);
services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
services.AddHttpClient();
services.AddBackend(config);
services.AddScoped<IAgentMessageAdapter, AgentMessageAdapter>();
await using var provider = services.BuildServiceProvider();
var adapter = provider.GetRequiredService<IAgentMessageAdapter>();
var load = await CosmosTranscriptLoader.LoadAsync(adapter, tenant, convId);

foreach (var row in load.ChatRows.OrderBy(r => r.Sequence))
{
    if (row.Sequence < 8 || row.Sequence > 38) continue;
    var tool = string.IsNullOrWhiteSpace(row.ToolName) ? "" : $" [{row.ToolName}]";
    var content = row.ToolResultJson ?? row.Content ?? "";
    if (focus.Contains(row.Sequence))
    {
        Console.WriteLine($"=== seq {row.Sequence} {row.Role}{tool} (full) ===");
        Console.WriteLine(content);
        Console.WriteLine();
        continue;
    }
    if (content.Length > 600) content = content[..600] + "...";
    content = content.Replace('\n', ' ').Replace('\r', ' ');
    Console.WriteLine($"seq {row.Sequence,4} {row.Role}{tool}: {content}");
}
}
