using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Journeys.DTO.Models;

/// <summary>
/// Allowed values for model attribute JSON <c>type</c> discriminator (Backend ModelAttributeDto).
/// Keep in sync with <c>ModelAttributeDtoJsonConverter</c> in Backend.Dto.
/// </summary>
public static class ModelAttributeDtoTypeDiscriminators
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        "Primitive", "Object", "List", "KeyValue", "Dynamic", "RawJson", "Tree",
        "ModelObject", "ModelList", "ModelKeyValue", "ModelDynamic", "ModelRawJson", "ModelTaxonomy"
    };
}

/// <summary>
/// Compact facts returned on successful campaign upsert (agent / API consumers).
/// </summary>
public class CampaignUpsertAssistantDigestDto
{
    public int SchemaVersion { get; set; } = 1;

    public string TenantId { get; set; } = string.Empty;

    public string CampaignId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ExtCampaignId { get; set; }

    public string? Etag { get; set; }

    public List<EventModelBindingDigestDto> EventModelBindings { get; set; } = new();

    public JourneyDigestDto JourneyDigest { get; set; } = new();

    public ValidationStampDto Validation { get; set; } = new();
}

public class EventModelBindingDigestDto
{
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Resolved backend model type when loaded (see ModelDto.ModelType).</summary>
    public string? ResolvedModelType { get; set; }
}

public class JourneyDigestDto
{
    public int JourneyNodeCount { get; set; }

    public int RuleSetCount { get; set; }

    public Dictionary<string, int> OutcomeKindCounts { get; set; } = new();
}

public class ValidationStampDto
{
    /// <summary>Tier A definition validation result after successful upsert.</summary>
    public string TierA { get; set; } = "passed";
}

/// <summary>
/// Heavy assistant context: model symbols, governance list, optional sample scaffold (GET + MCP).
/// </summary>
public class CampaignAssistantContextDto
{
    public int SchemaVersion { get; set; } = 1;

    public string TenantId { get; set; } = string.Empty;

    public string CampaignId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<string> AllowedModelAttributeTypes { get; set; } = ModelAttributeDtoTypeDiscriminators.All;

    public List<EventModelAssistantContextDto> EventModels { get; set; } = new();

    public SamplePayloadScaffoldDto? SampleScaffold { get; set; }

    public List<string> Warnings { get; set; } = new();

    public CampaignShellDigest? CampaignShell { get; set; }

    public PointAccountManifestDigest? PointAccountManifest { get; set; }

    public CampaignJourneyArtifactDigest? Journey { get; set; }
}

public class EventModelAssistantContextDto
{
    public string ModelId { get; set; } = string.Empty;

    public string? ModelType { get; set; }

    public string? Name { get; set; }

    /// <summary>Set when the model could not be loaded from any configured candidate type.</summary>
    public string? LoadWarning { get; set; }

    public List<AttributeSymbolRowDto> Attributes { get; set; } = new();

    public EventProcessingContractDigest? ProcessingContract { get; set; }
}

public class AttributeSymbolRowDto
{
    public string Symbol { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public string AttributeType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    /// <summary>v1: Live attributes are included in the sample template checklist.</summary>
    public bool IncludeInSampleTemplate { get; set; }

    public string? RestrictionSummary { get; set; }
}

public class SamplePayloadScaffoldDto
{
    public List<string> RequiredEventSymbols { get; set; } = new();

    /// <summary>Minimal JSON with <c>event</c> object and null placeholders for required symbols.</summary>
    public string? JsonTemplate { get; set; }

    public string? AccountLinkSymbolPath { get; set; }

    public List<string> NaturalKeySymbols { get; set; } = new();

    public Dictionary<string, string> FieldRoles { get; set; } = new();
}
