using Backend.Core.Llm;
using Backend.Llm.Anthropic;
using Journeys.Infra.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

public static class CampaignAgentLlmServiceCollectionExtensions
{
    public static IServiceCollection AddCampaignAgentLlm(this IServiceCollection services, IConfiguration configuration)
    {
        var kind = CampaignAgentLlmProvider.Resolve(configuration);
        if (kind == CampaignAgentLlmProviderKind.Anthropic)
        {
            services.AddSingleton(sp =>
                new AnthropicLlmChatClientFactory(
                    sp.GetRequiredService<IConfiguration>(),
                    "CampaignAgent",
                    sp.GetService<ILogger<AnthropicLlmChatClientFactory>>()));
            services.AddSingleton<ILlmChatClientFactory>(sp =>
                sp.GetRequiredService<AnthropicLlmChatClientFactory>());
            services.AddSingleton<ILlmPromptChatMapper, AnthropicLlmPromptChatMapper>();
        }
        else
        {
            services.AddSingleton(sp =>
                new OpenAICompatibleLlmChatClientFactory(
                    sp.GetRequiredService<IConfiguration>(),
                    "CampaignAgent",
                    sp.GetService<ILogger<OpenAICompatibleLlmChatClientFactory>>()));
            services.AddSingleton<ILlmChatClientFactory>(sp =>
                sp.GetRequiredService<OpenAICompatibleLlmChatClientFactory>());
            services.AddSingleton<ILlmPromptChatMapper, PassthroughLlmPromptChatMapper>();
        }

        return services;
    }
}
