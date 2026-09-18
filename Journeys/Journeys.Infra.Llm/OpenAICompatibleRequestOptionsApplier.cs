using Microsoft.Extensions.AI;

namespace Journeys.Infra.Llm;

/// <summary>
/// Copies Ollama runtime knobs onto <see cref="ChatOptions.AdditionalProperties"/>
/// (<c>num_ctx</c>, <c>think</c>, <c>reasoning_effort</c>) without mutating the source options.
/// </summary>
public static class OpenAICompatibleRequestOptionsApplier
{
    public const string NumCtxKey = "num_ctx";
    public const string ThinkKey = "think";
    public const string ReasoningEffortKey = "reasoning_effort";

    public static ChatOptions Apply(ChatOptions? options, OpenAICompatibleRuntimeOptions runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        var applied = options?.Clone() ?? new ChatOptions();
        if (runtime.NumCtx is null && runtime.ReasoningEffort is null)
            return applied;

        var props = new AdditionalPropertiesDictionary();
        if (applied.AdditionalProperties is not null)
        {
            foreach (var pair in applied.AdditionalProperties)
                props[pair.Key] = pair.Value;
        }

        if (runtime.NumCtx is int numCtx)
            props[NumCtxKey] = numCtx;

        if (runtime.ReasoningEffort is "off")
        {
            props[ThinkKey] = false;
        }
        else if (runtime.ReasoningEffort is string effort)
        {
            props[ThinkKey] = effort;
            props[ReasoningEffortKey] = effort;
        }

        applied.AdditionalProperties = props;
        return applied;
    }
}
