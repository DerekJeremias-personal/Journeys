using System.Security.Cryptography;
using System.Text;
using Backend.Core.Llm;
using Journeys.API.CampaignAgent.DataWarehouse;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Workflow;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Journeys.API.CampaignAgent;

public class CampaignAgentPromptComposer : ICampaignAgentPromptComposer
{
    private const string PersonaFileName = "SystemPrompt.txt";

    private readonly IHostEnvironment _hostEnvironment;
    private readonly IMemoryCache _cache;
    private readonly ICampaignAgentTenantContextProvider _tenantContextProvider;
    private readonly IOptions<DataWarehouseProxyOptions> _dataWarehouseOptions;
    private readonly ILogger<CampaignAgentPromptComposer> _logger;

    public CampaignAgentPromptComposer(
        IHostEnvironment hostEnvironment,
        IMemoryCache cache,
        ICampaignAgentTenantContextProvider tenantContextProvider,
        IOptions<DataWarehouseProxyOptions> dataWarehouseOptions,
        ILogger<CampaignAgentPromptComposer> logger)
    {
        _hostEnvironment = hostEnvironment;
        _cache = cache;
        _tenantContextProvider = tenantContextProvider;
        _dataWarehouseOptions = dataWarehouseOptions;
        _logger = logger;
    }

    public async Task<CampaignAgentLlmPromptContext> BuildAsync(
        string tenantId,
        string? linkedCampaignId,
        IReadOnlyList<AgentMessage>? conversationMessages,
        string? workflowOrchestrationHint,
        CampaignWorkflowState workflowState,
        IReadOnlyList<string>? toolsThisTurn = null,
        CancellationToken cancellationToken = default)
    {
        var phase = workflowState.Phase;
        if (!CampaignWorkflowChecklist.IsBriefCaptured(workflowState))
            phase = CampaignWorkflowPhase.DataAnalysis;
        var persona = await GetCachedFileContentAsync(PersonaFileName, "persona", cancellationToken).ConfigureAwait(false);
        var sharedTooling = await GetCachedFileContentAsync(
            CampaignWorkflowPhaseGovernanceFiles.SharedFileName,
            "sharedTooling",
            cancellationToken).ConfigureAwait(false);
        var coreGov = await GetCachedFileContentAsync(
            CampaignWorkflowPhaseGovernanceFiles.CoreFileName,
            "governanceCore",
            cancellationToken).ConfigureAwait(false);
        var casingGov = await GetCachedFileContentAsync(
            CampaignWorkflowPhaseGovernanceFiles.JsonCasingContractFileName,
            "jsonCasingContract",
            cancellationToken).ConfigureAwait(false);
        var coachGov = await GetCachedFileContentAsync(
            CampaignWorkflowPhaseGovernanceFiles.CoachChecklistFileName,
            "coachChecklist",
            cancellationToken).ConfigureAwait(false);
        var phaseFileName = CampaignWorkflowPhaseGovernanceFiles.GetPhaseFileName(
            phase,
            _dataWarehouseOptions.Value.Enabled);
        var phaseGov = await GetCachedFileContentAsync(phaseFileName, $"phase:{phase}", cancellationToken).ConfigureAwait(false);
        var supplementalGov = await GetPhaseSupplementalGovernanceAsync(
            phase,
            workflowState,
            toolsThisTurn,
            cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(supplementalGov))
        {
            phaseGov = phaseGov.TrimEnd()
                + "\n\n---\n" + supplementalGov.Trim();
        }

        var governanceHash = ComputeSha256Hex(string.Join(
            "\n",
            new[] { sharedTooling.Trim(), coreGov.Trim(), casingGov.Trim(), coachGov.Trim(), phaseGov.Trim() }));

        var session = new StringBuilder();
        session.AppendLine($"Session context: authenticated tenantId is \"{tenantId}\". Use this tenant for tool calls unless the user explicitly names a different tenant.");
        if (!string.IsNullOrWhiteSpace(linkedCampaignId))
            session.AppendLine($"This turn is associated with linkedCampaignId \"{linkedCampaignId.Trim()}\" when relevant to tool calls or explanations.");

        if (conversationMessages is { Count: > 0 })
        {
            var threadCtx = CampaignAgentThreadContextBuilder.Build(conversationMessages);
            var block = threadCtx.ToPromptBlock();
            if (!string.IsNullOrEmpty(block))
                session.AppendLine().AppendLine(block);
        }

        var tenantSlice = await _tenantContextProvider.GetSliceAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var tenantSection = CampaignAgentTenantCuratedSection.Build(tenantSlice);
        if (tenantSection.ContentSha256Hex != null)
        {
            _logger.LogDebug(
                "Campaign agent tenant curated section: contentSha256={TenantCuratedHash}, cacheVersion={CacheVersion}",
                tenantSection.ContentSha256Hex,
                tenantSlice?.CacheVersion ?? "");
        }

        var phaseTitle = CampaignWorkflowPhaseGovernanceFiles.GetPhaseSectionTitle(phase);
        var segments = new List<LlmPromptSegment>
        {
            new(LlmPromptSegmentKind.Persona, LlmPromptSegmentStability.Stable, persona.Trim()),
            new(LlmPromptSegmentKind.Governance, LlmPromptSegmentStability.Stable,
                "\n---\nSHARED TOOLING GOVERNANCE (mandatory)\n---\n" + sharedTooling.Trim()),
            new(LlmPromptSegmentKind.Governance, LlmPromptSegmentStability.Stable,
                "\n---\nCAMPAIGN GOVERNANCE — CORE (mandatory)\n---\n" + coreGov.Trim()),
            new(LlmPromptSegmentKind.Governance, LlmPromptSegmentStability.Stable,
                "\n---\n" + CampaignWorkflowPhaseGovernanceFiles.JsonCasingContractSectionTitle + "\n---\n" + casingGov.Trim()),
            new(LlmPromptSegmentKind.Governance, LlmPromptSegmentStability.Stable,
                "\n---\n" + CampaignWorkflowPhaseGovernanceFiles.GetCoachChecklistSectionTitle() + "\n---\n" + coachGov.Trim()),
            new(LlmPromptSegmentKind.Governance, LlmPromptSegmentStability.Stable,
                $"\n---\n{phaseTitle}\n---\n" + phaseGov.Trim())
        };

        if (!string.IsNullOrEmpty(tenantSection.SectionText))
            segments.Add(new LlmPromptSegment(LlmPromptSegmentKind.TenantCurated, LlmPromptSegmentStability.Stable, "\n" + tenantSection.SectionText.TrimEnd()));

        var sessionBody = session.ToString().TrimEnd();
        var openingUser = conversationMessages?
            .OrderBy(m => m.Sequence)
            .FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?.Content;
        var artifactsBlock = CampaignWorkflowArtifactPromptBuilder.Build(workflowState, openingUser);
        if (!string.IsNullOrEmpty(artifactsBlock))
            sessionBody += "\n\n" + artifactsBlock;

        if (!string.IsNullOrWhiteSpace(workflowOrchestrationHint))
            sessionBody += "\n\n" + workflowOrchestrationHint.Trim();
        var sessionSegment = "\n---\nSESSION\n---\n" + sessionBody;
        segments.Add(new LlmPromptSegment(LlmPromptSegmentKind.Session, LlmPromptSegmentStability.Volatile, sessionSegment));

        var plan = new LlmPromptPlan(segments);
        IReadOnlyList<ChatMessage> ephemeral = Array.Empty<ChatMessage>();

        return new CampaignAgentLlmPromptContext(
            plan,
            ephemeral,
            governanceHash,
            tenantSection.ContentSha256Hex,
            tenantSlice?.CacheVersion);
    }

    private async Task<string?> GetPhaseSupplementalGovernanceAsync(
        CampaignWorkflowPhase phase,
        CampaignWorkflowState workflowState,
        IReadOnlyList<string>? toolsThisTurn,
        CancellationToken cancellationToken)
    {
        string? buildPhaseGov = null;
        if (IsCampaignBuildPhase(phase))
        {
            var package = BuildContextPackageResolver.Resolve(workflowState, toolsThisTurn);
            var files = await LoadGovernanceFileMapAsync(cancellationToken).ConfigureAwait(false);
            var slices = BuildSubStepGovernanceResolver.Resolve(package, files);
            buildPhaseGov = slices.Count == 0 ? null : string.Join("\n\n---\n", slices);
        }

        if (ShouldInjectCrossPhaseBuildSubStepGovernance(phase, workflowState) && !IsCampaignBuildPhase(phase))
        {
            var package = BuildContextPackageResolver.Resolve(workflowState, toolsThisTurn);
            var files = await LoadGovernanceFileMapAsync(cancellationToken).ConfigureAwait(false);
            var crossSlices = BuildSubStepGovernanceResolver.Resolve(package, files);
            buildPhaseGov = MergeGovernanceSlices(buildPhaseGov, crossSlices);
        }

        if (!string.IsNullOrWhiteSpace(buildPhaseGov))
            return buildPhaseGov;

        if (phase is CampaignWorkflowPhase.Verification or CampaignWorkflowPhase.Done)
        {
            var files = await LoadGovernanceFileMapAsync(cancellationToken).ConfigureAwait(false);
            var sections = new List<string>();

            foreach (var slice in BuildSubStepGovernanceResolver.ResolveVerificationHandoff(files))
                sections.Add(slice);

            await AppendSectionAsync(
                sections,
                CampaignWorkflowPhaseGovernanceFiles.PointAccountModelSectionTitle,
                CampaignWorkflowPhaseGovernanceFiles.PointAccountJourneyCheatSheetFileName,
                phase,
                cancellationToken);
            await AppendSectionAsync(
                sections,
                "RULES ENGINE VERIFICATION",
                CampaignWorkflowPhaseGovernanceFiles.RulesEnginePatternVerificationCheatSheetFileName,
                phase,
                cancellationToken);

            return sections.Count == 0 ? null : string.Join("\n\n---\n", sections);
        }

        return null;
    }

    private static bool ShouldInjectCrossPhaseBuildSubStepGovernance(
        CampaignWorkflowPhase phase,
        CampaignWorkflowState state)
    {
        if (phase is CampaignWorkflowPhase.Verification or CampaignWorkflowPhase.Done)
            return false;

        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return false;

        if (CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
            return false;

        var subStep = WorkflowSkillRegistry.ResolveBuildSubStep(state);
        return subStep is CampaignBuildSubStep.PatRequired or CampaignBuildSubStep.JourneyRequired;
    }

    private static string? MergeGovernanceSlices(string? existing, IReadOnlyList<string> extra)
    {
        if (extra.Count == 0)
            return existing;

        var combined = string.Join("\n\n---\n", extra);
        if (string.IsNullOrWhiteSpace(existing))
            return combined;

        if (existing.Contains(extra[0], StringComparison.Ordinal))
            return existing;

        return existing + "\n\n---\n" + combined;
    }

    private static bool IsCampaignBuildPhase(CampaignWorkflowPhase phase) =>
        phase is CampaignWorkflowPhase.CampaignBuild
            or CampaignWorkflowPhase.CampaignSetup
            or CampaignWorkflowPhase.PointAccountTypes
            or CampaignWorkflowPhase.CampaignJourney;

    private async Task<IReadOnlyDictionary<string, string>> LoadGovernanceFileMapAsync(CancellationToken cancellationToken)
    {
        var names = new[]
        {
            CampaignWorkflowPhaseGovernanceFiles.PointAccountModelFileName,
            CampaignWorkflowPhaseGovernanceFiles.PointAccountJourneyCheatSheetFileName,
            CampaignWorkflowPhaseGovernanceFiles.RulesEnginePatternFileName,
            CampaignWorkflowPhaseGovernanceFiles.JsonCasingContractFileName,
            CampaignWorkflowPhaseGovernanceFiles.CampaignDtoShapeFileName,
            CampaignWorkflowPhaseGovernanceFiles.ProcessEventPayloadTypesFileName
        };

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(_hostEnvironment.ContentRootPath, "CampaignAgent");
        foreach (var name in names)
        {
            var path = Path.Combine(dir, name);
            if (!File.Exists(path))
                continue;

            var content = await GetCachedFileContentAsync(
                name,
                $"supplemental:map:{name}",
                cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(content))
                map[name] = content;
        }

        return map;
    }

    private async Task AppendSectionAsync(
        List<string> sections,
        string title,
        string fileName,
        CampaignWorkflowPhase phase,
        CancellationToken cancellationToken)
    {
        var content = await GetCachedFileContentAsync(fileName, $"supplemental:{phase}:{fileName}", cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
            return;

        sections.Add($"{title}\n---\n{content.Trim()}");
    }

    private async Task<string> GetCachedFileContentAsync(string fileName, string cacheSegment, CancellationToken cancellationToken)
    {
        var dir = Path.Combine(_hostEnvironment.ContentRootPath, "CampaignAgent");
        var path = Path.Combine(dir, fileName);
        var cacheKey = $"CampaignAgent:Prompt:{cacheSegment}:{path}";

        if (!File.Exists(path))
        {
            _logger.LogWarning("Campaign agent prompt file missing: {Path}", path);
            return cacheSegment switch
            {
                "governanceCore" =>
                    "No CampaignGovernanceCore.txt — require explicit tenant and IDs; Draft-first; prefer THREAD CONTEXT.",
                "sharedTooling" =>
                    "No shared tooling governance file found. Use only session tools; do not invent tenant or IDs; persist only via successful tool calls; prefer read-only tools before mutations.",
                "coachChecklist" =>
                    "WORKFLOW checklist is advisory after the design brief is captured. Follow user intent; coach with validation feedback, not phase locks.",
                _ when cacheSegment.StartsWith("phase:", StringComparison.Ordinal) =>
                    "No workflow phase governance file found. Follow SESSION WORKFLOW and WORKFLOW ARTIFACTS; use only tools visible this turn.",
                _ => "You are an Journeys campaign and journey assistant. Require explicit tenantId and IDs for tools."
            };
        }

        var lastWrite = File.GetLastWriteTimeUtc(path);
        if (_cache.TryGetValue(cacheKey, out CachedPromptFile? entry)
            && entry != null
            && entry.LastWriteUtc == lastWrite)
            return entry.Content;

        var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        entry = new CachedPromptFile(content, lastWrite);
        _cache.Set(cacheKey, entry, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(1)
        });
        return content;
    }

    private static string ComputeSha256Hex(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record CachedPromptFile(string Content, DateTime LastWriteUtc);
}
