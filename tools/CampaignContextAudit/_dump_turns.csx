// One-off transcript dump — delete after use
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"
#r "nuget: Microsoft.Extensions.Http, 9.0.0"

using CampaignContextAudit.Configuration;
using CampaignContextAudit.Transcript;
using Elevate.ELP.Core.Interfaces.DataStorage;
using Elevate.ELP.DAL.Adapters;
using Elevate.ELP.Infra.Backend;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var tenant = "primo";
var convId = "a12c5ed754af4680a21a414157e1043f";
var minSeq = 85;
var maxSeq = long.MaxValue;
var focusSeqs = new HashSet<long>();

var config = AuditBackendConfiguration.Build();
AuditBackendConfiguration.Validate(config);
var services = new ServiceCollection();
services.AddSingleton(config);
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddHttpClient();
services.AddBackend(config);
services.AddScoped<IAgentMessageAdapter, AgentMessageAdapter>();
await using var provider = services.BuildServiceProvider();
var adapter = provider.GetRequiredService<IAgentMessageAdapter>();
var load = await CosmosTranscriptLoader.LoadAsync(adapter, tenant, convId);

foreach (var row in load.ChatRows.OrderBy(r => r.Sequence).Where(r => r.Sequence >= minSeq))
{
    var role = row.Role ?? "?";
    var tool = string.IsNullOrWhiteSpace(row.ToolName) ? "" : $" [{row.ToolName}]";
    var content = row.Content ?? row.ToolResultJson ?? "";
    if (content.Length > 1200) content = content[..1200] + "...";
    content = content.Replace('\n', ' ').Replace('\r', ' ');
    Console.WriteLine($"seq {row.Sequence,4} {role}{tool}: {content}");
}
