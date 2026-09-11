namespace CampaignContextAudit.Cli;

public enum CliInputMode
{
    File,
    Cosmos,
    StaticGovernance
}

public sealed record CliOptions(
    CliInputMode InputMode,
    string? TranscriptsDir,
    string? TenantId,
    IReadOnlyList<string> ConversationIds,
    string GovernanceDir,
    string OutputDir,
    string? JsonOutDir,
    string? BaselineJson,
    string? TenantSliceFile,
    int SlowToolMs,
    int StallMs,
    int AbortMs,
    bool DataWarehouseEnabled,
    string? PhaseOverride,
    string? SourceRoot,
    string? CorrelateFindingsDir)
{
    public static CliOptions? Parse(string[] args)
    {
        string? tr = null, gov = null, outv = null, jsonOut = null, baseline = null, tenantSlice = null, phase = null, tenant = null;
        string? sourceRoot = null, correlateFindings = null;
        var conversationIds = new List<string>();
        var slowToolMs = 10_000;
        var stallMs = 120_000;
        var abortMs = 300_000;
        var warehouse = true;
        var staticGovernance = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--transcripts": tr = Next(args, ref i); break;
                case "--tenant": tenant = Next(args, ref i); break;
                case "--conversation-id":
                    var id = Next(args, ref i);
                    if (!string.IsNullOrWhiteSpace(id)) conversationIds.Add(id);
                    break;
                case "--governance": gov = Next(args, ref i); break;
                case "--out": outv = Next(args, ref i); break;
                case "--json-out": jsonOut = Next(args, ref i); break;
                case "--baseline": baseline = Next(args, ref i); break;
                case "--tenant-slice-file": tenantSlice = Next(args, ref i); break;
                case "--source-root": sourceRoot = Next(args, ref i); break;
                case "--correlate-findings": correlateFindings = Next(args, ref i); break;
                case "--static-governance": staticGovernance = true; break;
                case "--slow-tool-ms":
                    var raw = Next(args, ref i);
                    if (raw is not null && int.TryParse(raw, out var ms)) slowToolMs = ms;
                    break;
                case "--stall-ms":
                    var stallRaw = Next(args, ref i);
                    if (stallRaw is not null && int.TryParse(stallRaw, out var stall)) stallMs = stall;
                    break;
                case "--abort-ms":
                    var abortRaw = Next(args, ref i);
                    if (abortRaw is not null && int.TryParse(abortRaw, out var abort)) abortMs = abort;
                    break;
                case "--phase": phase = Next(args, ref i); break;
                case "--no-warehouse": warehouse = false; break;
            }
        }

        if (gov is null || outv is null) return null;

        if (staticGovernance)
        {
            if (!string.IsNullOrWhiteSpace(tr) || conversationIds.Count > 0 || baseline is not null)
                return null;

            return new CliOptions(
                CliInputMode.StaticGovernance,
                null,
                null,
                [],
                gov,
                outv,
                jsonOut,
                null,
                tenantSlice,
                slowToolMs,
                stallMs,
                abortMs,
                warehouse,
                phase,
                sourceRoot,
                correlateFindings);
        }

        var hasTranscripts = !string.IsNullOrWhiteSpace(tr);
        var hasCosmos = conversationIds.Count > 0;
        if (hasTranscripts == hasCosmos) return null;
        if (hasCosmos && string.IsNullOrWhiteSpace(tenant)) return null;

        return new CliOptions(
            hasCosmos ? CliInputMode.Cosmos : CliInputMode.File,
            tr,
            tenant,
            conversationIds,
            gov,
            outv,
            jsonOut,
            baseline,
            tenantSlice,
            slowToolMs,
            stallMs,
            abortMs,
            warehouse,
            phase,
            sourceRoot,
            correlateFindings);
    }

    private static string? Next(string[] a, ref int i) => (++i < a.Length) ? a[i] : null;
}
