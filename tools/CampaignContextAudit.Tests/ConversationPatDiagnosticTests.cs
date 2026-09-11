using CampaignContextAudit.Analysis;
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
using Xunit;
using Xunit.Abstractions;

namespace CampaignContextAudit.Tests;

public class ConversationPatDiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public ConversationPatDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Dump_a8a789_pat_phase()
    {
        var load = await LoadAsync("primo", "a8a789010e3840128b04af34aa417f43");
        var rows = load.ChatRows.OrderBy(r => r.Sequence).ToList();
        var timeline = ToolCallTimeline.Build(rows);

        _output.WriteLine("=== User turns ===");
        foreach (var u in rows.Where(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase)))
            _output.WriteLine($"seq {u.Sequence}: {Truncate(u.Content, 200)}");

        _output.WriteLine("\n=== PAT / model / campaign mutators ===");
        foreach (var e in timeline.Where(e =>
                     e.ToolName.Contains("point_account", StringComparison.OrdinalIgnoreCase)
                     || e.ToolName.Contains("get_model", StringComparison.OrdinalIgnoreCase)
                     || e.ToolName.Contains("save_model", StringComparison.OrdinalIgnoreCase)
                     || e.ToolName.Contains("upsert_campaign", StringComparison.OrdinalIgnoreCase)
                     || e.ToolName.Contains("validate_campaign", StringComparison.OrdinalIgnoreCase)))
        {
            var toolRow = rows.FirstOrDefault(r =>
                string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase)
                && r.ToolCallId == e.CallId);
            var snippet = Truncate(toolRow?.ToolResultJson ?? toolRow?.Content, 120);
            _output.WriteLine($"seq {e.Sequence,4} {e.ToolName,-28} rc={e.ResultChars,5}  {snippet}");
        }

        _output.WriteLine("\n=== get_model / save_model call ids ===");
        foreach (var a in rows.Where(r => r.Sequence is >= 8 and <= 24 && string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var fc in MeaiEnvelope.ExtractFunctionCalls(a.Content))
            {
                if (!fc.Name.Contains("model", StringComparison.OrdinalIgnoreCase))
                    continue;
                _output.WriteLine($"  seq {a.Sequence} {fc.Name}: {Truncate(fc.ArgumentsRaw, 200)}");
            }
        }

        _output.WriteLine("\n=== Stall / deferral assistant lines ===");
        foreach (var a in rows.Where(r => string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase)))
        {
            var c = a.Content ?? "";
            if (!ContainsPatStallSignal(c))
                continue;
            _output.WriteLine($"seq {a.Sequence}: {Truncate(c, 500)}");
        }

        var wfDoc = load.WorkflowRows.LastOrDefault();
        if (wfDoc is not null)
        {
            var wfMsg = AgentMessageDocMapper.ToAgentMessage(wfDoc, load.OwnerUserId);
            var state = CampaignWorkflowState.FromWorkflowRow(wfMsg);
            _output.WriteLine("\n=== Final workflow ===");
            _output.WriteLine($"phase={state?.Phase} modelGate={wfMsg.WorkflowModelGatePassed} creationComplete={CreationSnapshotArtifact.IsCreationComplete(state!)}");
            _output.WriteLine($"patManifest items={PointAccountManifestBuilder.Parse(state!.Artifacts.PointAccountManifest).Items.Count}");
            _output.WriteLine($"deferredMutatorRetry={state.Artifacts.DeferredMutatorRetry} patHttpDeferral={state.Artifacts.PatHttpDeferralShown}");
            if (!string.IsNullOrWhiteSpace(state.Artifacts.LastToolRemediationSummary))
                _output.WriteLine($"lastRemediation={Truncate(state.Artifacts.LastToolRemediationSummary, 300)}");
        }
    }

    private static bool ContainsPatStallSignal(string c) =>
        c.Contains("HTTP", StringComparison.OrdinalIgnoreCase)
        || c.Contains("paste", StringComparison.OrdinalIgnoreCase)
        || c.Contains("GUID", StringComparison.OrdinalIgnoreCase)
        || c.Contains("not surfaced", StringComparison.OrdinalIgnoreCase)
        || c.Contains("upsert_point_account", StringComparison.OrdinalIgnoreCase)
        || c.Contains("point account type", StringComparison.OrdinalIgnoreCase)
        || c.Contains("Coach:", StringComparison.OrdinalIgnoreCase)
        || c.Contains("proceed", StringComparison.OrdinalIgnoreCase) && c.Contains("PAT", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "...";

    [Fact]
    public async Task Replay_a8a789_flags_gate_closed_pat_stall()
    {
        var load = await LoadAsync("primo", "a8a789010e3840128b04af34aa417f43");
        var timeline = ToolCallTimeline.Build(load.ChatRows);
        var findings = OutcomeDetectors.DetectGateClosedPatStall(load.ChatRows);

        var finding = Assert.Single(findings);
        Assert.Equal("GATE_CLOSED_PAT_STALL", finding.Code);
        Assert.Contains("17", finding.CitedSequences);
        Assert.Contains("23", finding.CitedSequences);
    }

    [Fact]
    public async Task Dump_914b0134_duration_and_chattiness()
    {
        var load = await LoadAsync("primo", "914b0134be574299b2fed8d5f406c598");
        var rows = load.ChatRows.OrderBy(r => r.Sequence).ToList();
        var timeline = ToolCallTimeline.Build(rows);

        _output.WriteLine($"=== Stats ===");
        _output.WriteLine($"Rows={rows.Count} user={rows.Count(r => r.Role == "user")} assistant={rows.Count(r => r.Role == "assistant")} tool={rows.Count(r => r.Role == "tool")}");
        _output.WriteLine($"Seq {rows.First().Sequence}-{rows.Last().Sequence}");

        var withTs = rows.Where(r => r.CosmosTimestamp.HasValue).OrderBy(r => r.CosmosTimestamp).ToList();
        if (withTs.Count >= 2)
        {
            var span = DateTimeOffset.FromUnixTimeSeconds(withTs.Last().CosmosTimestamp!.Value)
                       - DateTimeOffset.FromUnixTimeSeconds(withTs.First().CosmosTimestamp!.Value);
            _output.WriteLine($"Cosmos _ts wall span: {span.TotalMinutes:F1} min");
        }

        var wf = load.WorkflowRows.LastOrDefault();
        if (wf?.TurnMetricsJson is not null)
            _output.WriteLine($"turnMetricsJson: {Truncate(wf.TurnMetricsJson, 800)}");

        _output.WriteLine("\n=== User turns ===");
        var userSeqs = rows.Where(r => r.Role == "user").Select(r => r.Sequence).ToList();
        for (var i = 0; i < userSeqs.Count; i++)
        {
            var start = userSeqs[i];
            var end = i + 1 < userSeqs.Count ? userSeqs[i + 1] : long.MaxValue;
            var seg = rows.Where(r => r.Sequence > start && r.Sequence < end).ToList();
            var assistChars = seg.Where(r => r.Role == "assistant").Sum(r => (r.Content ?? "").Length);
            var user = rows.First(r => r.Sequence == start);
            _output.WriteLine($"Turn {i + 1} seq {start}: user {(user.Content ?? "").Length} chars | segment {seg.Count} rows, {seg.Count(r => r.Role == "tool")} tools, {assistChars:N0} assistant chars");
            _output.WriteLine($"  user: {Truncate(user.Content, 200)}");
        }

        _output.WriteLine("\n=== Top assistant prose ===");
        foreach (var a in rows.Where(r => r.Role == "assistant").OrderByDescending(r => (r.Content ?? "").Length).Take(8))
            _output.WriteLine($"seq {a.Sequence}: {(a.Content ?? "").Length} chars — {Truncate(a.Content, 200)}");

        _output.WriteLine("\n=== Tool timeline ===");
        foreach (var e in timeline)
            _output.WriteLine($"seq {e.Sequence,4} {e.ToolName,-28} rc={e.ResultChars,5}");

        var snapshot = WorkflowSnapshotParser.Parse(wf);
        var findings = OutcomeDetectors.DetectAll(snapshot, rows, timeline, wf?.Sequence).ToList();
        findings.AddRange(OutcomeDetectors.DetectGateClosedPatStall(rows));
        _output.WriteLine("\n=== Key findings ===");
        foreach (var f in findings.Where(f => f.Severity is "blocking" or "degrading"))
            _output.WriteLine($"{f.Code} | {f.Severity} | {string.Join(",", f.CitedSequences)} | {f.Summary}");
    }

    [Fact]
    public async Task Dump_3b92be6e_duration_and_chattiness()
    {
        var load = await LoadAsync("primo", "3b92be6e594845fe9c6c0131c8698e4c");
        var rows = load.ChatRows.OrderBy(r => r.Sequence).ToList();
        var timeline = ToolCallTimeline.Build(rows);

        _output.WriteLine($"=== Stats ===");
        _output.WriteLine($"Rows={rows.Count} user={rows.Count(r => r.Role == "user")} assistant={rows.Count(r => r.Role == "assistant")} tool={rows.Count(r => r.Role == "tool")}");
        _output.WriteLine($"Seq {rows.First().Sequence}-{rows.Last().Sequence}");
        _output.WriteLine($"validate_campaign calls={timeline.Count(e => e.ToolName == "validate_campaign")}");
        _output.WriteLine($"get_model calls={timeline.Count(e => e.ToolName == "get_model")}");
        _output.WriteLine($"get_rules_engine_contract_summary calls={timeline.Count(e => e.ToolName == "get_rules_engine_contract_summary")}");

        var withTs = rows.Where(r => r.CosmosTimestamp.HasValue).OrderBy(r => r.CosmosTimestamp).ToList();
        if (withTs.Count >= 2)
        {
            var span = DateTimeOffset.FromUnixTimeSeconds(withTs.Last().CosmosTimestamp!.Value)
                       - DateTimeOffset.FromUnixTimeSeconds(withTs.First().CosmosTimestamp!.Value);
            _output.WriteLine($"Cosmos _ts wall span: {span.TotalMinutes:F1} min");
        }

        _output.WriteLine("\n=== User turns ===");
        var userSeqs = rows.Where(r => r.Role == "user").Select(r => r.Sequence).ToList();
        for (var i = 0; i < userSeqs.Count; i++)
        {
            var start = userSeqs[i];
            var end = i + 1 < userSeqs.Count ? userSeqs[i + 1] : long.MaxValue;
            var seg = rows.Where(r => r.Sequence > start && r.Sequence < end).ToList();
            var assistChars = seg.Where(r => r.Role == "assistant").Sum(r => (r.Content ?? "").Length);
            var validates = seg.Count(r => r.Role == "tool" && (r.ToolName ?? "").Contains("validate_campaign", StringComparison.OrdinalIgnoreCase));
            var user = rows.First(r => r.Sequence == start);
            _output.WriteLine($"Turn {i + 1} seq {start}: {validates} validates | segment {seg.Count} rows, {seg.Count(r => r.Role == "tool")} tools, {assistChars:N0} assistant chars");
            _output.WriteLine($"  user: {Truncate(user.Content, 200)}");
        }

        _output.WriteLine("\n=== Top assistant prose ===");
        foreach (var a in rows.Where(r => r.Role == "assistant").OrderByDescending(r => (r.Content ?? "").Length).Take(8))
            _output.WriteLine($"seq {a.Sequence}: {(a.Content ?? "").Length} chars — {Truncate(a.Content, 200)}");

        _output.WriteLine("\n=== Tool timeline (mutators + discovery) ===");
        foreach (var e in timeline.Where(e =>
                     e.ToolName.Contains("validate", StringComparison.OrdinalIgnoreCase)
                     || e.ToolName.Contains("upsert", StringComparison.OrdinalIgnoreCase)
                     || e.ToolName.Contains("get_model", StringComparison.OrdinalIgnoreCase)
                     || e.ToolName.Contains("contract", StringComparison.OrdinalIgnoreCase)
                     || e.ToolName.Contains("example", StringComparison.OrdinalIgnoreCase)
                     || e.ToolName.Contains("process_event", StringComparison.OrdinalIgnoreCase)))
            _output.WriteLine($"seq {e.Sequence,4} {e.ToolName,-35} rc={e.ResultChars,6}");

        var wf = load.WorkflowRows.LastOrDefault();
        if (wf is not null)
        {
            var wfMsg = AgentMessageDocMapper.ToAgentMessage(wf, load.OwnerUserId);
            var state = CampaignWorkflowState.FromWorkflowRow(wfMsg);
            _output.WriteLine("\n=== Final workflow ===");
            _output.WriteLine($"phase={state?.Phase} creationComplete={CreationSnapshotArtifact.IsCreationComplete(state!)}");
            _output.WriteLine($"journeyRuleSetCount={CreationSnapshotArtifact.Read(state!)?.JourneyRuleSetCount}");
            _output.WriteLine($"validationStalled={state!.Artifacts.ValidationStalled} stallCycles={state.Artifacts.ValidationStallCycleCount}");
            _output.WriteLine($"journeyContractPin={state.Artifacts.JourneyContractSummaryPinActive} fetched={state.Artifacts.JourneyContractSummaryFetchedThisEpisode}");
            _output.WriteLine($"journeyPatternPrep={state.Artifacts.JourneyPatternPrepComplete} patternId={state.Artifacts.JourneyPatternId}");
            if (!string.IsNullOrWhiteSpace(state.Artifacts.LastToolRemediationSummary))
                _output.WriteLine($"lastRemediation={Truncate(state.Artifacts.LastToolRemediationSummary, 400)}");
        }

        var snapshot = WorkflowSnapshotParser.Parse(wf);
        var findings = OutcomeDetectors.DetectAll(snapshot, rows, timeline, wf?.Sequence).ToList();
        findings.AddRange(OutcomeDetectors.DetectGateClosedPatStall(rows));
        _output.WriteLine("\n=== All findings ===");
        foreach (var f in findings.OrderByDescending(f => f.Severity))
            _output.WriteLine($"{f.Code} | {f.Severity} | {string.Join(",", f.CitedSequences)} | {f.Summary}");
    }

    [Fact]
    public async Task Dump_4b6d3c_duration_and_chattiness()
    {
        var load = await LoadAsync("primo", "4b6d3c959c674737beaaddeb61109d7c");
        var rows = load.ChatRows.OrderBy(r => r.Sequence).ToList();
        var timeline = ToolCallTimeline.Build(rows);

        _output.WriteLine($"=== Stats ===");
        _output.WriteLine($"Rows={rows.Count} user={rows.Count(r => r.Role == "user")} assistant={rows.Count(r => r.Role == "assistant")} tool={rows.Count(r => r.Role == "tool")}");
        _output.WriteLine($"validate_campaign calls={timeline.Count(e => e.ToolName == "validate_campaign")}");

        _output.WriteLine("\n=== User turns ===");
        var userSeqs = rows.Where(r => r.Role == "user").Select(r => r.Sequence).ToList();
        for (var i = 0; i < userSeqs.Count; i++)
        {
            var start = userSeqs[i];
            var end = i + 1 < userSeqs.Count ? userSeqs[i + 1] : long.MaxValue;
            var seg = rows.Where(r => r.Sequence > start && r.Sequence < end).ToList();
            var assistChars = seg.Where(r => r.Role == "assistant").Sum(r => (r.Content ?? "").Length);
            var validates = seg.Count(r => r.Role == "tool" && (r.ToolName ?? "").Contains("validate_campaign", StringComparison.OrdinalIgnoreCase));
            var user = rows.First(r => r.Sequence == start);
            _output.WriteLine($"Turn {i + 1} seq {start}: {validates} validates | segment {seg.Count} rows, {seg.Count(r => r.Role == "tool")} tools, {assistChars:N0} assistant chars");
            _output.WriteLine($"  user: {Truncate(user.Content, 200)}");
        }

        _output.WriteLine("\n=== validate_campaign timeline ===");
        foreach (var e in timeline.Where(e => e.ToolName == "validate_campaign"))
            _output.WriteLine($"seq {e.Sequence,4} rc={e.ResultChars,5}");

        var wf = load.WorkflowRows.LastOrDefault();
        var snapshot = WorkflowSnapshotParser.Parse(wf);
        var findings = OutcomeDetectors.DetectAll(snapshot, rows, timeline, wf?.Sequence).ToList();
        _output.WriteLine("\n=== Key findings ===");
        foreach (var f in findings.Where(f => f.Severity is "blocking" or "degrading" or "intent-breaking"))
            _output.WriteLine($"{f.Code} | {f.Severity} | {string.Join(",", f.CitedSequences)} | {f.Summary}");
    }

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
