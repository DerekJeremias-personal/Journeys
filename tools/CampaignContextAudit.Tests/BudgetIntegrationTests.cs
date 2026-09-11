using CampaignContextAudit.Budget;
using CampaignContextAudit.Configuration;
using CampaignContextAudit.Transcript;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.DAL.Adapters;
using Journeys.Infra.Backend;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace CampaignContextAudit.Tests;

public class BudgetIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public BudgetIntegrationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Replay_4a1b472_turn_57_history_under_80k()
    {
        var load = await LoadAsync("primo", "4a1b472b537448dabff275ca914efe8c");
        var ordered = load.ChatRows.OrderBy(m => m.Sequence).ToList();
        var historyInput = ordered.Where(m => m.Sequence <= 57).ToList();
        var before = HistoryBudgeter.CountEffective(historyInput);
        var budget = HistoryBudgeter.Apply(historyInput);

        _output.WriteLine($"rows={historyInput.Count} before={before} after={budget.FinalChars} dropped={budget.DroppedSegments.Count}");
        LogBreakdown(budget.Kept, _output);

        Assert.Empty(budget.DroppedSegments);
        Assert.True(budget.FinalChars <= HistoryBudgeter.MaxChars,
            $"Turn 57 history {budget.FinalChars:N0} chars exceeds {HistoryBudgeter.MaxChars:N0} cap (before={before:N0}).");
    }

    private static void LogBreakdown(IReadOnlyList<Models.AgentMessageDoc> rows, ITestOutputHelper output)
    {
        var tool = rows.Where(m => m.Role == "tool").Sum(m => m.ToolResultJson?.Length ?? 0);
        var user = rows.Where(m => m.Role == "user").Sum(m => m.Content?.Length ?? 0);
        var asstPlain = rows.Where(m => m.Role == "assistant" && !IsMeai(m.Content)).Sum(m => m.Content?.Length ?? 0);
        var asstMeai = rows.Where(m => m.Role == "assistant" && IsMeai(m.Content)).Sum(m => m.Content?.Length ?? 0);
        output.WriteLine($" kept: tool={tool} user={user} asstPlain={asstPlain} asstMeaiRaw={asstMeai} eff={HistoryBudgeter.CountEffective(rows)}");
    }

    private static bool IsMeai(string? content) =>
        content?.TrimStart().StartsWith("{\"v\":1,\"meai\":true", StringComparison.Ordinal) == true;

    private static async Task<LoadedTranscript> LoadAsync(string tenant, string convId)
    {
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
        return await CosmosTranscriptLoader.LoadAsync(adapter, tenant, convId);
    }
}
