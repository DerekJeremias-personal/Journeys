using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;

namespace Journeys.Core.Utility;

public static class EventWrapperModelFactory
{
    public static EventWrapperBuildResult Build(
        ModelDto eventModel,
        ModelDto referenceWrapper,
        string? existingWrapperId = null)
    {
        if (eventModel == null || string.IsNullOrWhiteSpace(eventModel.ID))
        {
            return Failure("EVENT_MODEL_INVALID", "Event model is required and must include a valid id.");
        }

        if (referenceWrapper == null
            || !EventWrapperEngineReferenceIds.TryExtract(referenceWrapper, out var engineIds)
            || engineIds == null)
        {
            return Failure("INVALID_REFERENCE_WRAPPER", "Reference wrapper does not expose canonical engine model ids.");
        }

        var modelType = string.IsNullOrWhiteSpace(referenceWrapper.ModelType)
            ? "loyalty"
            : referenceWrapper.ModelType;

        var model = new ModelDto
        {
            Name = $"{eventModel.Name}AndRuleState",
            IsContainer = true,
            PartitionKey1Symbol = "accountid",
            ModelType = modelType,
            Attributes = BuildCanonicalAttributes(eventModel.ID, modelType, engineIds)
        };

        if (!string.IsNullOrWhiteSpace(existingWrapperId))
            model.ID = existingWrapperId;

        var validation = WrapperModelContractValidator.Validate(model, eventModel);
        if (!validation.IsStructurallyValid)
        {
            return Failure(
                "FACTORY_VALIDATION_FAILED",
                string.Join("; ", validation.Errors));
        }

        return new EventWrapperBuildResult
        {
            Success = true,
            Model = model,
            ReferenceWrapperModelId = referenceWrapper.ID
        };
    }

    private static List<ModelAttributeDto> BuildCanonicalAttributes(
        string eventModelId,
        string modelType,
        EventWrapperEngineReferenceIds engineIds) =>
    [
        new ModelAttributePrimitiveDto { Symbol = "accountid", DataType = "String", Status = "Live" },
        new ModelAttributePrimitiveDto { Symbol = "naturalkey", DataType = "String", Status = "Live" },
        new ModelAttributePrimitiveDto { Symbol = "timeofoccurrence", DataType = "Date", Status = "Live" },
        new ModelAttributePrimitiveDto { Symbol = "lastprocessed", DataType = "Date", Status = "Live" },
        new ModelAttributeObjectDto
        {
            Symbol = "event",
            DataType = "Object",
            ModelId = eventModelId,
            ModelType = modelType,
            Status = "Live"
        },
        new ModelAttributeDynamicDto
        {
            Symbol = "eventwrapper",
            DataType = "Dynamic",
            Status = "Live"
        },
        new ModelAttributeListDto
        {
            Symbol = "appliedcampaigns",
            DataType = "List",
            ListAttributeDataType = "String",
            Status = "Live"
        },
        new ModelAttributeListDto
        {
            Symbol = "appliedrulesetids",
            DataType = "List",
            ListAttributeDataType = "String",
            Status = "Live"
        },
        new ModelAttributeKeyValueDto
        {
            Symbol = "providerstates",
            DataType = "KeyValue",
            KeyValueAttributeDataType = "Object",
            ModelId = engineIds.ProviderStatesModelId,
            ModelType = modelType,
            Status = "Live"
        },
        new ModelAttributeKeyValueDto
        {
            Symbol = "journeystates",
            DataType = "KeyValue",
            KeyValueAttributeDataType = "Object",
            ModelId = engineIds.JourneyStatesModelId,
            ModelType = modelType,
            Status = "Live"
        },
        new ModelAttributeListDto
        {
            Symbol = "outcomestates",
            DataType = "List",
            ListAttributeDataType = "Object",
            ModelId = engineIds.OutcomeStatesModelId,
            ModelType = modelType,
            Status = "Live"
        }
    ];

    private static EventWrapperBuildResult Failure(string code, string message) =>
        new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message
        };
}
