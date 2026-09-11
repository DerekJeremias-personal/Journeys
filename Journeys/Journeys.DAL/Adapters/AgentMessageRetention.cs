using Journeys.Core.Models;
using Microsoft.Extensions.Configuration;

namespace Journeys.DAL.Adapters;

public static class AgentMessageRetention
{
    public const int DefaultRetentionDays = 30;

    public static void Apply(AgentMessage message, IConfiguration configuration)
    {
        var days = configuration.GetValue("CampaignAgent:MessageRetentionDays", DefaultRetentionDays);
        message.TtlSeconds = days <= 0 ? null : days * 86400;
    }
}
