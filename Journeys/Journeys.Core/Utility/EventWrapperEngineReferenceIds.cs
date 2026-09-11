using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;

namespace Journeys.Core.Utility;

public sealed class EventWrapperEngineReferenceIds
{
    public required string ProviderStatesModelId { get; init; }

    public required string JourneyStatesModelId { get; init; }

    public required string OutcomeStatesModelId { get; init; }

    public static bool TryExtract(ModelDto referenceWrapper, out EventWrapperEngineReferenceIds? ids)
    {
        ids = null;
        ArgumentNullException.ThrowIfNull(referenceWrapper);

        if (!TryGetModelId(referenceWrapper, "providerstates", out var providerStatesModelId)
            || !TryGetModelId(referenceWrapper, "journeystates", out var journeyStatesModelId)
            || !TryGetModelId(referenceWrapper, "outcomestates", out var outcomeStatesModelId))
        {
            return false;
        }

        ids = new EventWrapperEngineReferenceIds
        {
            ProviderStatesModelId = providerStatesModelId,
            JourneyStatesModelId = journeyStatesModelId,
            OutcomeStatesModelId = outcomeStatesModelId
        };
        return true;
    }

    private static bool TryGetModelId(ModelDto wrapper, string symbol, out string modelId)
    {
        modelId = string.Empty;
        if (wrapper.Attributes == null)
            return false;

        foreach (var attribute in wrapper.Attributes)
        {
            if (!string.Equals(attribute.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                continue;

            var candidate = attribute switch
            {
                ModelAttributeListDto list => list.ModelId,
                ModelAttributeKeyValueDto keyValue => keyValue.ModelId,
                ModelAttributeObjectDto modelObject => modelObject.ModelId,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(candidate) || !Guid.TryParse(candidate, out _))
                return false;

            modelId = candidate;
            return true;
        }

        return false;
    }
}
