using System;
using System.Threading;
using System.Threading.Tasks;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

internal sealed class ModelReadDigestAIFunction : DelegatingAIFunction
{
    private readonly bool _enabled;
    private readonly ILogger? _logger;
    private readonly Func<CampaignWorkflowState>? _getState;

    public ModelReadDigestAIFunction(
        AIFunction innerFunction,
        bool enabled,
        ILogger? logger,
        Func<CampaignWorkflowState>? getState = null)
        : base(innerFunction)
    {
        _enabled = enabled;
        _logger = logger;
        _getState = getState;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var result = await InnerFunction.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);

        if (!ToolResultTextExtractor.TryExtractText(result, out var text, out var rewrap) || string.IsNullOrWhiteSpace(text))
        {
            _logger?.LogWarning(
                "Model read digest could not extract text from {Tool} result (type {Type}); returning raw result.",
                Name, result?.GetType().Name ?? "null");
            return result;
        }

        TryMergeWorkflowContractFromRaw(text);

        if (!_enabled)
            return result;

        try
        {
            var state = _getState?.Invoke();
            var creationComplete = state != null && CreationSnapshotArtifact.IsCreationComplete(state);
            var useVerifySlim = creationComplete
                && IsSingleModelReadTool(Name)
                && !VerificationDebugContext.IsActiveDebug(state!);
            var useCreationSlim = !creationComplete
                && IsSingleModelReadTool(Name)
                && IsCreationPhase(state?.Phase);
            var digest = useCreationSlim
                ? ModelReadDigester.DigestForCreation(text)
                : useVerifySlim
                    ? ModelReadDigester.DigestForVerification(text)
                    : ModelReadDigester.Digest(text);
            if (!digest.Transformed)
                return result;

            _logger?.LogInformation(
                "Model read digest for {Tool}: {Original} -> {Digest} chars.",
                Name, digest.OriginalChars, digest.DigestChars);

            return rewrap(digest.Json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Model read digest failed for {Tool}; returning original result.", Name);
            return result;
        }
    }

    private static bool IsSingleModelReadTool(string? toolName) =>
        string.Equals(toolName, "get_model", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "GetModel", StringComparison.OrdinalIgnoreCase);

    private static bool IsCreationPhase(CampaignWorkflowPhase? phase) =>
        phase is CampaignWorkflowPhase.EventModels
            or CampaignWorkflowPhase.DataAnalysis
            or CampaignWorkflowPhase.CampaignSetup
            or CampaignWorkflowPhase.PointAccountTypes
            or CampaignWorkflowPhase.CampaignBuild
            or CampaignWorkflowPhase.CampaignJourney;

    private void TryMergeWorkflowContractFromRaw(string rawJson)
    {
        var state = _getState?.Invoke();
        if (state == null)
            return;

        try
        {
            if (EventModelContractsAccumulator.TryMergeFromToolResult(state, Name, rawJson))
            {
                _logger?.LogDebug("Model read digest merged event contract from raw JSON for {Tool}.", Name);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Model read digest raw contract merge failed for {Tool}; continuing.", Name);
        }
    }
}
