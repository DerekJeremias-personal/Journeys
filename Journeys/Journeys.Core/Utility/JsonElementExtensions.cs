


using Backend.Dto.Interfaces;
using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;
using Backend.Dto.Structures.Model.Restrictions;
using MassTransit.Internals.GraphValidation;
using System.Text.Json;

namespace Journeys.Core.Utility;

public static class JsonElementExtensions
{
    private static bool TryGetNestedElement(this JsonElement element, string propertyName, out JsonElement result)
    {
        result = default;
        var comparer = StringComparer.OrdinalIgnoreCase;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (comparer.Equals(prop.Name, propertyName))
                    {
                        result = prop.Value;
                        return true;
                    }
                    if (TryGetNestedElement(prop.Value, propertyName, out result))
                        return true;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (TryGetNestedElement(item, propertyName, out result))
                        return true;
                }
                break;
        }

        return false;
    }

    public static Dictionary<string, string> ValidateWithModel(this IDynamicEntity node, ModelDto modelDetails)
    {
        string json = JsonSerializer.Serialize(node, node.GetType(), JsonUtility.GetDefaultOptions());
        using var document = JsonDocument.Parse(json);

        var errors = new Dictionary<string, string>();
        foreach (var attribute in modelDetails.Attributes)
        {
            // Use recursive function to pull out json property given the attribute symbol
            var valueExists = document.RootElement.TryGetNestedElement(attribute.Symbol, out var valueElement);

            // check required first
            var requiredConstraint = attribute.Restrictions?
                .OfType<RequiredRestrictionDto>()
                .FirstOrDefault();

            if (!valueExists)
            {
                if (requiredConstraint != null)
                    errors[attribute.Symbol] = requiredConstraint.ErrorMessage;
                continue;
            }

            // now type‐check only when present
            if (attribute is ListRestrictionDto)
            {
                if (valueElement.ValueKind != JsonValueKind.Array)
                {
                    errors[attribute.Symbol] = "Expected a JSON array.";
                    continue;
                }
            }
            else if (attribute is ModelAttributeKeyValueDto)
            {
                if (valueElement.ValueKind != JsonValueKind.Object)
                {
                    errors[attribute.Symbol] = "Expected a JSON object.";
                    continue;
                }
            }
            else if (valueExists && !(Enum.TryParse<ModelAttributeDataTypeDto>(attribute.DataType, true, out var dt) && dt switch
            {
                ModelAttributeDataTypeDto.String => valueElement.ValueKind == JsonValueKind.String || valueElement.ValueKind == JsonValueKind.Number || valueElement.ValueKind == JsonValueKind.Null,
                ModelAttributeDataTypeDto.Number => valueElement.ValueKind == JsonValueKind.Number || valueElement.ValueKind == JsonValueKind.Null,
                ModelAttributeDataTypeDto.Date => valueElement.ValueKind == JsonValueKind.String && DateTime.TryParse(valueElement.GetString(), out _),
                ModelAttributeDataTypeDto.Boolean => valueElement.ValueKind is JsonValueKind.True or JsonValueKind.False,
                ModelAttributeDataTypeDto.List => valueElement.ValueKind is JsonValueKind.Array or JsonValueKind.False,
                ModelAttributeDataTypeDto.Object => valueElement.ValueKind is JsonValueKind.Object or JsonValueKind.False,
                _ => false
            }) && (errors[attribute.Symbol] = $"Expected type '{dt}'") != null)
            {
                continue;
            }

            // If no constraints exist or their status is in draft continue to next attribute
            if (attribute.Restrictions == null ||
                attribute.Restrictions.Count == 0)
                continue;

            // Start applying attribute constraint validation now that constraint(s) exist and data type is valid
            foreach (var constraint in attribute.Restrictions)
            {
                switch (constraint)
                {
                    // Don't allow empty strings for elements that are required
                    case RequiredRestrictionDto:
                        if (valueElement.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(valueElement.GetString()))
                        {
                            errors[attribute.Symbol] = constraint.ErrorMessage;
                            continue;
                        }
                        break;

                    // Check value exists, ensure value is numeric type, cast to decimal to avoid overflow and precision loss, and constrain the range
                    case NumericRestrictionDto:
                        if (!valueExists) break;
                        decimal numericValue;
                        if (valueElement.ValueKind == JsonValueKind.Number)
                        {
                            numericValue = valueElement.GetDecimal();
                        }
                        else if (valueElement.ValueKind == JsonValueKind.String &&
                                 decimal.TryParse(valueElement.GetString(), out var parsedNum))
                        {
                            numericValue = parsedNum;
                        }
                        else
                        {
                            errors[attribute.Symbol] = constraint.ErrorMessage;
                            break;
                        }
                        var numericConstraint = constraint as NumericRestrictionDto;
                        if (numericValue < numericConstraint?.MinValue || numericValue > numericConstraint?.MaxValue)
                            errors[attribute.Symbol] = constraint.ErrorMessage;
                        break;

                    // Check value exists, ensure DateTime type, and constrain the range
                    case DateRestrictionDto:
                        if (!valueExists) break;
                        DateTime dateValue;
                        if (valueElement.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(valueElement.GetString(), out var parsedDate))
                        {
                            dateValue = parsedDate;
                        }
                        else if (valueElement.ValueKind == JsonValueKind.Number)
                        {
                            try
                            {
                                dateValue = valueElement.GetDateTime();
                            }
                            catch
                            {
                                errors[attribute.Symbol] = constraint.ErrorMessage;
                                break;
                            }
                        }
                        else
                        {
                            errors[attribute.Symbol] = constraint.ErrorMessage;
                            break;
                        }
                        var dateContraint = constraint as DateRestrictionDto;
                        if (dateValue < dateContraint?.MinDate || dateValue > dateContraint?.MaxDate)
                            errors[attribute.Symbol] = constraint.ErrorMessage;
                        break;

                    // Adams - TODO: implement list validation for list of T type
                    // Check value exists, ensure List type, and constrain the range
                    case ListRestrictionDto:
                        if (!valueExists) break;
                        if (valueElement.ValueKind != JsonValueKind.Array)
                        {
                            errors[attribute.Symbol] = constraint.ErrorMessage;
                            break;
                        }
                        var listConstraint = constraint as ListRestrictionDto;
                        var count = valueElement.GetArrayLength();
                        if (count < listConstraint?.MinValue || count > listConstraint?.MaxValue)
                            errors[attribute.Symbol] = constraint.ErrorMessage;
                        break;

                        //default:
                        //    continue;
                }
            }
        }
        return errors;
    }
}
