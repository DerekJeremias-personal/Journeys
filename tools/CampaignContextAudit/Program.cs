using CampaignContextAudit.Analysis;
using CampaignContextAudit.Budget;
using CampaignContextAudit.Cli;
using CampaignContextAudit.Configuration;
using CampaignContextAudit.Governance;
using CampaignContextAudit.Mapping;
using CampaignContextAudit.Models;
using CampaignContextAudit.Reporting;
using CampaignContextAudit.Transcript;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.DAL.Adapters;
using Journeys.Infra.Backend;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CampaignContextAudit;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var opts = CliOptions.Parse(args);
        if (opts is null) { PrintUsage(); return 2; }

        if (!Directory.Exists(opts.GovernanceDir))
        {
            Console.Error.WriteLine($"Governance dir not found: {opts.GovernanceDir}");
            return 3;
        }

        Directory.CreateDirectory(opts.OutputDir);
        if (opts.JsonOutDir is not null)
            Directory.CreateDirectory(opts.JsonOutDir);

        if (opts.InputMode == CliInputMode.StaticGovernance)
            return RunStaticGovernance(opts);

        var tenantSlice = opts.TenantSliceFile is not null && File.Exists(opts.TenantSliceFile)
            ? File.ReadAllText(opts.TenantSliceFile)
            : null;

        IReadOnlyList<LoadedTranscript> transcripts;
        if (opts.InputMode == CliInputMode.File)
        {
            if (!Directory.Exists(opts.TranscriptsDir))
            {
                Console.Error.WriteLine($"Transcripts dir not found: {opts.TranscriptsDir}");
                return 3;
            }

            transcripts = TranscriptLoader.LoadDir(opts.TranscriptsDir!);
            if (transcripts.Count == 0)
            {
                Console.Error.WriteLine("No *.json transcripts found.");
                return 4;
            }
        }
        else
        {
            try
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

                var loaded = new List<LoadedTranscript>();
                foreach (var rawId in opts.ConversationIds)
                {
                    try
                    {
                        loaded.Add(await CosmosTranscriptLoader.LoadAsync(adapter, opts.TenantId!, rawId));
                    }
                    catch (InvalidOperationException ex) when (
                        ex.Message.Contains("No AgentMessage rows found", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine(ex.Message);
                        return 7;
                    }
                }

                transcripts = loaded;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 6;
            }
        }

        var sizer = new SegmentSizer(opts.GovernanceDir, opts.DataWarehouseEnabled);

        AuditReportJson? baseline = null;
        if (opts.BaselineJson is not null)
        {
            if (!File.Exists(opts.BaselineJson))
            {
                Console.Error.WriteLine($"Baseline JSON not found: {opts.BaselineJson}");
                return 5;
            }
            baseline = FindingsJsonWriter.Read(opts.BaselineJson);
        }

        foreach (var t in transcripts)
        {
            ProcessTranscript(t, opts, sizer, tenantSlice, baseline);
        }

        return 0;
    }

    private static int RunStaticGovernance(CliOptions opts)
    {
        var sourceRoot = opts.SourceRoot
            ?? Path.GetFullPath(Path.Combine(opts.GovernanceDir, "..", "..", ".."));

        var report = StaticGovernanceAnalyzer.Analyze(new StaticGovernanceOptions(
            opts.GovernanceDir,
            sourceRoot,
            opts.DataWarehouseEnabled,
            opts.CorrelateFindingsDir));

        var md = GovernanceReportWriter.Build(report);
        var outPath = Path.Combine(opts.OutputDir, $"{DateTime.UtcNow:yyyy-MM-dd}-governance-static-audit.md");
        File.WriteAllText(outPath, md);

        if (opts.JsonOutDir is not null)
        {
            var jsonPath = Path.Combine(opts.JsonOutDir, $"{DateTime.UtcNow:yyyy-MM-dd}-governance-static-findings.json");
            GovernanceFindingsJsonWriter.Write(jsonPath, report, opts.GovernanceDir, sourceRoot);
            Console.WriteLine($"Wrote {jsonPath}.");
        }

        Console.WriteLine($"Wrote {outPath} ({report.Findings.Count} findings).");
        return 0;
    }

    private static void ProcessTranscript(
        LoadedTranscript t,
        CliOptions opts,
        SegmentSizer sizer,
        string? tenantSlice,
        AuditReportJson? baseline)
    {
        var workflowRow = t.WorkflowRows.OrderByDescending(w => w.Sequence).FirstOrDefault();
        var snapshot = WorkflowSnapshotParser.Parse(workflowRow);
        var workflowState = MapWorkflowState(workflowRow, t.OwnerUserId);
        var timeline = ToolCallTimeline.Build(t.ChatRows);
        var phaseMap = PhaseReconstructor.PhaseByUserTurn(t.ChatRows, timeline, opts.PhaseOverride);
        var turns = TurnBudgetBuilder.Build(
            t.ChatRows, phaseMap, p => sizer.StableSizesForPhase(p), workflowState, tenantSlice);

        var findings = new List<Finding>();
        findings.AddRange(CompetencyDetectors.RedundantDiscovery(timeline));
        findings.AddRange(CompetencyDetectors.RediscoveryAfterCreation(timeline));
        findings.AddRange(CompetencyDetectors.TopBloat(timeline, top: 5));
        findings.AddRange(CompetencyDetectors.WorkflowStateDrift(t.WorkflowRows, timeline));
        findings.AddRange(OutcomeDetectors.DetectAll(snapshot, t.ChatRows, timeline, workflowRow?.Sequence));
        findings.AddRange(OutcomeDetectors.DetectVerificationBlockedNoAllowlist(workflowRow?.Content, t.ChatRows));
        findings.AddRange(OutcomeDetectors.DetectEventModelsGateBlocksMutators(
            snapshot, t.ChatRows, timeline, workflowRow?.WorkflowModelGatePassed, workflowRow?.Sequence));
        findings.AddRange(OutcomeDetectors.DetectPatMutatorDeferredGateOpen(
            snapshot, t.ChatRows, timeline, workflowRow?.WorkflowModelGatePassed, workflowRow?.Sequence));
        findings.AddRange(OutcomeDetectors.DetectGateClosedPatStall(t.ChatRows));
        findings.AddRange(OutcomeDetectors.DetectPatSkippedInlineManifest(
            snapshot, t.ChatRows, workflowRow?.WorkflowModelGatePassed, workflowRow?.Sequence));
        findings.AddRange(OutcomeDetectors.DetectFalseValidationPassZeroRuleSets(t.ChatRows));
        findings.AddRange(OutcomeDetectors.DetectJourneyShapeNodesNotChildren(t.ChatRows));
        findings.AddRange(OutcomeDetectors.DetectEventPayloadSchemaLoop(snapshot, t.ChatRows, timeline));

        var allRows = t.ChatRows.Concat(t.WorkflowRows).ToList();
        var effectiveWallMs = EffectiveWallMsResolver.Resolve(allRows, null);
        var (performance, perfFindings) = PerformanceAggregator.Build(
            allRows, timeline, opts.SlowToolMs, longTurnMs: opts.StallMs, effectiveWallMsOverride: effectiveWallMs);
        findings.AddRange(perfFindings);

        effectiveWallMs = EffectiveWallMsResolver.Resolve(allRows, performance);
        findings.AddRange(OutcomeDetectors.DetectStallAndAbort(
            snapshot, allRows, effectiveWallMs, opts.StallMs, opts.AbortMs));
        findings = OutcomeDetectors.ApplyValidationLoopSeverity(findings, snapshot).ToList();

        var milestones = CreationMilestoneEvaluator.Evaluate(
            snapshot, t.ChatRows, timeline, workflowRow?.Content);
        var delivery = DeliveryGrader.Grade(
            findings, snapshot, milestones, effectiveWallMs, t.ChatRows, opts.AbortMs);

        var name = ResolveReportName(t.SourcePath);
        var tenantSliceNote = tenantSlice is null
            ? null
            : "Tenant curated slice text was supplied via `--tenant-slice-file` and included in SESSION estimates.";
        var rubric = RubricGrader.Grade(findings, snapshot, turns, delivery.Grade);
        var linkedCampaignId = TryReadLinkedCampaignId(workflowRow?.Content);
        var report = ReportWriter.Build(
            name, turns, findings, snapshot, performance, tenantSliceNote, rubric, delivery, linkedCampaignId);
        var outPath = Path.Combine(opts.OutputDir, $"{DateTime.UtcNow:yyyy-MM-dd}-{name}-context-audit.md");
        File.WriteAllText(outPath, report);

        AuditReportJson? currentJson = null;
        if (opts.JsonOutDir is not null)
        {
            var jsonReport = FindingsJsonWriter.BuildReport(t, snapshot, findings, turns, performance, rubric, delivery);
            currentJson = jsonReport;
            var jsonPath = Path.Combine(opts.JsonOutDir, $"{DateTime.UtcNow:yyyy-MM-dd}-{name}-findings.json");
            FindingsJsonWriter.Write(jsonPath, jsonReport);
            Console.WriteLine($"Wrote {jsonPath}.");
        }

        if (baseline is not null && currentJson is not null)
        {
            var diff = TranscriptDiffWriter.Build(baseline, currentJson, name);
            var diffPath = Path.Combine(opts.OutputDir, $"{name}-diff-report.md");
            File.WriteAllText(diffPath, diff);
            Console.WriteLine($"Wrote {diffPath}.");
        }

        Console.WriteLine($"Wrote {outPath} ({turns.Count} turns, {findings.Count} findings).");
    }

    private static string ResolveReportName(string sourcePath)
    {
        if (sourcePath.StartsWith("cosmos:", StringComparison.OrdinalIgnoreCase))
        {
            var slash = sourcePath.LastIndexOf('/');
            return slash >= 0 ? sourcePath[(slash + 1)..] : sourcePath["cosmos:".Length..];
        }

        return Path.GetFileNameWithoutExtension(sourcePath);
    }

    private static CampaignWorkflowState? MapWorkflowState(AgentMessageDoc? workflowRow, string? ownerUserId)
    {
        if (workflowRow is null) return null;
        return CampaignWorkflowState.FromWorkflowRow(AgentMessageDocMapper.ToAgentMessage(workflowRow, ownerUserId));
    }

    private static void PrintUsage() => Console.Error.WriteLine(
        "Usage (trace): CampaignContextAudit (--transcripts <dir> | --tenant <id> --conversation-id <id> [...]) " +
        "--governance <dir> --out <dir> " +
        "[--json-out <dir>] [--baseline <findings.json>] [--tenant-slice-file <path>] " +
        "[--slow-tool-ms <ms>] [--stall-ms <ms>] [--abort-ms <ms>] [--phase <Phase>] [--no-warehouse]\n" +
        "Usage (static): CampaignContextAudit --static-governance --governance <dir> --out <dir> " +
        "[--source-root <Journeys>] [--json-out <dir>] [--correlate-findings <dir>] [--no-warehouse]");

    private static string? TryReadLinkedCampaignId(string? workflowContent)
    {
        if (string.IsNullOrWhiteSpace(workflowContent)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(workflowContent);
            var root = doc.RootElement;
            if (root.TryGetProperty("creationSnapshot", out var cs)
                && cs.ValueKind == System.Text.Json.JsonValueKind.String
                && !string.IsNullOrWhiteSpace(cs.GetString()))
            {
                using var inner = System.Text.Json.JsonDocument.Parse(cs.GetString()!);
                if (inner.RootElement.TryGetProperty("campaignId", out var cid))
                    return cid.GetString();
            }

            if (root.TryGetProperty("campaignShellRef", out var shell)
                && shell.ValueKind == System.Text.Json.JsonValueKind.String
                && !string.IsNullOrWhiteSpace(shell.GetString()))
            {
                using var inner = System.Text.Json.JsonDocument.Parse(shell.GetString()!);
                if (inner.RootElement.TryGetProperty("campaignId", out var cid))
                    return cid.GetString();
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // ignore malformed workflow content
        }

        return null;
    }
}
