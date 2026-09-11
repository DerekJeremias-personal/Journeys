using System.Reflection;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Builds the Campaign Agent <see cref="IChatClient"/> stack with exactly one
/// <see cref="ChatClientBuilderExtensions.UseFunctionInvocation"/> layer and transcript sanitization
/// on every inner model call.
/// </summary>
internal static class CampaignAgentChatClientStackBuilder
{
    /// <summary>
    /// Produces: <c>FunctionInvocation → TranscriptSanitizer → (leaf from factory, FnInv stripped)</c>.
    /// </summary>
    public static IChatClient Build(IChatClient factoryClient, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(factoryClient);
        ArgumentNullException.ThrowIfNull(logger);

        var leaf = UnwrapFunctionInvokingLayers(factoryClient, out var removedLayers);
        if (removedLayers > 0)
        {
            logger.LogInformation(
                "Campaign agent removed {RemovedLayers} factory FunctionInvocation wrapper(s) to avoid duplicate tool_result transcripts.",
                removedLayers);
        }

        leaf = new CampaignAgentTranscriptSanitizerChatClient(leaf);
        return new ChatClientBuilder(leaf).UseFunctionInvocation().Build();
    }

    /// <summary>
    /// Returns true when the client (or its inner chain) already includes function invocation.
    /// </summary>
    public static bool HasFunctionInvocationLayer(IChatClient client)
    {
        var current = client;
        var visited = new HashSet<IChatClient>();
        while (visited.Add(current))
        {
            if (IsFunctionInvokingClient(current))
                return true;
            if (!TryGetInnerClient(current, out var inner))
                break;
            current = inner;
        }

        return false;
    }

    private static IChatClient UnwrapFunctionInvokingLayers(IChatClient client, out int removedLayers)
    {
        removedLayers = 0;
        while (IsFunctionInvokingClient(client) && TryGetInnerClient(client, out var inner))
        {
            client = inner;
            removedLayers++;
        }

        return client;
    }

    private static bool IsFunctionInvokingClient(IChatClient client) =>
        client.GetType().Name.Contains("FunctionInvoking", StringComparison.Ordinal);

    private static bool TryGetInnerClient(IChatClient client, out IChatClient inner)
    {
        var type = client.GetType();
        foreach (var propName in new[] { "InnerClient", "Inner" })
        {
            var prop = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop?.GetValue(client) is IChatClient wrapped)
            {
                inner = wrapped;
                return true;
            }
        }

        inner = null!;
        return false;
    }
}
