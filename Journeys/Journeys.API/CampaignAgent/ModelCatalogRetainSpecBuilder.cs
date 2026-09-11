using System;
using System.Collections.Generic;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Builds a <see cref="ModelCatalogRetainSpec"/> for a turn: catalog-only platform model names from config
/// plus the primary campaign's event model ids (the campaign under construction).
/// </summary>
public static class ModelCatalogRetainSpecBuilder
{
    public static ModelCatalogRetainSpec Build(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyCollection<string> catalogOnlyModelNames)
    {
        var context = CampaignAgentThreadContextBuilder.Build(messages);
        var retainIds = context.PrimaryCampaign?.EventModelIds ?? Array.Empty<string>();
        return new ModelCatalogRetainSpec(catalogOnlyModelNames, retainIds);
    }
}
