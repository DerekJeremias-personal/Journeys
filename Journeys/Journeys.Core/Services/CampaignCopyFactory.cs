#nullable enable

using System;
using System.Text.Json;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Services;

public static class CampaignCopyFactory
{
    public static CampaignDto ForNewProgram(CampaignDto source, string? nameOverride)
    {
        var copy = Clone(source);
        copy.Id = Guid.NewGuid().ToString();
        copy.Etag = null;
        copy.ExtCampaignId = Guid.NewGuid().ToString("N");
        copy.Status = CampaignStatusStrings.Draft.ToLowerInvariant();
        copy.Name = nameOverride ?? $"{source.Name} Copy";
        copy.DeployedDate = null;
        copy.ArchivedDate = null;
        copy.AssistantDigest = null;
        return copy;
    }

    public static CampaignDto ForRestoreFromArchive(CampaignDto archive)
    {
        var draft = Clone(archive);
        draft.Id = Guid.NewGuid().ToString();
        draft.Etag = null;
        draft.Status = CampaignStatusStrings.Draft.ToLowerInvariant();
        draft.DeployedDate = null;
        draft.ArchivedDate = null;
        draft.AssistantDigest = null;
        return draft;
    }

    private static CampaignDto Clone(CampaignDto source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<CampaignDto>(json)
            ?? throw new InvalidOperationException("Campaign copy could not be created.");
    }
}
