using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

/// <summary>Advertises an alternate tool name while delegating invocation to the canonical function.</summary>
internal sealed class ToolAliasAIFunction : AIFunction
{
    private readonly AIFunction _inner;

    public ToolAliasAIFunction(AIFunction inner, string aliasName)
    {
        _inner = inner;
        Name = aliasName;
    }

    public override string Name { get; }

    public override string Description => _inner.Description;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) =>
        _inner.InvokeAsync(arguments, cancellationToken);
}
