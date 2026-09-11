using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;

namespace Journeys.Core.Utility;

public sealed class WrapperContractValidationResult
{
    public bool IsStructurallyValid => Errors.Count == 0;

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Validates <c>*AndRuleState</c> wrapper models against the canonical <c>WrappedEventPayload</c> shape.
/// </summary>
public static class WrapperModelContractValidator
{
    private sealed record CanonicalAttribute(
        string Symbol,
        string ExpectedType,
        string? ExpectedElementType,
        bool RequiresModelId,
        bool Required = true);

    private static readonly CanonicalAttribute[] Canonical =
    [
        new("accountid", "Primitive", null, false),
        new("naturalkey", "Primitive", null, false),
        new("timeofoccurrence", "Primitive", null, false),
        new("lastprocessed", "Primitive", null, false, Required: false),
        new("event", "ModelObject", "Object", true),
        new("eventwrapper", "ModelDynamic", null, false, Required: false),
        new("appliedcampaigns", "ModelList", "String", false),
        new("appliedrulesetids", "ModelList", "String", false),
        new("providerstates", "ModelKeyValue", "Object", true),
        new("journeystates", "ModelKeyValue", "Object", true),
        new("outcomestates", "ModelList", "Object", true),
    ];

    private static readonly HashSet<string> DeprecatedSymbols =
        new(StringComparer.OrdinalIgnoreCase) { "journeys", "outcomes" };

    public static bool LooksLikeEventWrapper(ModelDto? model) =>
        model != null
        && model.IsContainer
        && !string.IsNullOrWhiteSpace(model.Name)
        && model.Name.EndsWith("AndRuleState", StringComparison.OrdinalIgnoreCase);

    public static WrapperContractValidationResult Validate(ModelDto? wrapper, ModelDto? linkedEventModel = null)
    {
        if (wrapper == null)
            return new WrapperContractValidationResult { Errors = ["Wrapper model is null."] };

        if (!LooksLikeEventWrapper(wrapper))
            return new WrapperContractValidationResult();

        var errors = new List<string>();
        var warnings = new List<string>();
        var bySymbol = BuildAttributeMap(wrapper);

        foreach (var deprecated in DeprecatedSymbols)
        {
            if (bySymbol.ContainsKey(deprecated))
                errors.Add($"Deprecated wrapper attribute '{deprecated}' — use canonical symbol (e.g. journeystates/outcomestates).");
        }

        foreach (var spec in Canonical)
        {
            if (!bySymbol.TryGetValue(spec.Symbol, out var attr))
            {
                if (spec.Required)
                    errors.Add($"Missing required wrapper attribute '{spec.Symbol}'.");
                else
                    warnings.Add($"Optional wrapper attribute '{spec.Symbol}' is missing.");
                continue;
            }

            if (!TypeMatches(attr, spec.ExpectedType))
                errors.Add($"Attribute '{spec.Symbol}' must be type {spec.ExpectedType}, found {attr.Type ?? attr.GetType().Name}.");

            if (spec.ExpectedElementType != null && !ElementTypeMatches(attr, spec.ExpectedElementType))
                errors.Add($"Attribute '{spec.Symbol}' element type must be {spec.ExpectedElementType}.");

            if (spec.RequiresModelId && !HasValidModelId(attr))
                errors.Add($"Attribute '{spec.Symbol}' requires modelId and modelType when element type is Object.");
        }

        if (linkedEventModel != null
            && bySymbol.TryGetValue("event", out var eventAttr)
            && HasValidModelId(eventAttr)
            && !string.Equals(GetModelId(eventAttr), linkedEventModel.ID, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Wrapper event.modelId ({GetModelId(eventAttr)}) must match linked event model id ({linkedEventModel.ID}).");
        }

        return new WrapperContractValidationResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    private static Dictionary<string, ModelAttributeDto> BuildAttributeMap(ModelDto wrapper)
    {
        var map = new Dictionary<string, ModelAttributeDto>(StringComparer.OrdinalIgnoreCase);
        if (wrapper.Attributes == null)
            return map;

        foreach (var attr in wrapper.Attributes)
        {
            if (!string.IsNullOrWhiteSpace(attr.Symbol))
                map[attr.Symbol] = attr;
        }

        return map;
    }

    private static bool TypeMatches(ModelAttributeDto attr, string expectedType) =>
        string.Equals(NormalizeType(attr), expectedType, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeType(ModelAttributeDto attr)
    {
        if (!string.IsNullOrWhiteSpace(attr.Type))
            return attr.Type;
        return attr switch
        {
            ModelAttributePrimitiveDto => "Primitive",
            ModelAttributeObjectDto => "ModelObject",
            ModelAttributeListDto => "ModelList",
            ModelAttributeKeyValueDto => "ModelKeyValue",
            ModelAttributeDynamicDto => "ModelDynamic",
            _ => attr.GetType().Name
        };
    }

    private static bool ElementTypeMatches(ModelAttributeDto attr, string expectedElementType)
    {
        var elementType = attr switch
        {
            ModelAttributeListDto list => list.ListAttributeDataType,
            ModelAttributeKeyValueDto kv => kv.KeyValueAttributeDataType,
            ModelAttributeObjectDto => "Object",
            _ => null
        };

        return string.Equals(elementType, expectedElementType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasValidModelId(ModelAttributeDto attr) =>
        !string.IsNullOrWhiteSpace(GetModelId(attr)) && Guid.TryParse(GetModelId(attr), out _);

    private static string? GetModelId(ModelAttributeDto attr) =>
        attr switch
        {
            ModelAttributeObjectDto o => o.ModelId,
            ModelAttributeListDto l => l.ModelId,
            ModelAttributeKeyValueDto kv => kv.ModelId,
            _ => null
        };
}
