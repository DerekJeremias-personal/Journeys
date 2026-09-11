using Backend.Dto.Structures.Model;
using Backend.Dto.Utilities;
using Journeys.DTO.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Journeys.Core.Utility;

/// <summary>
/// Validates Journeys-specific event model metadata for EventsController / EventService processing.
/// Backend SaveModel remains extensible; this enforces the consumer contract in Journeys Core.
/// </summary>
public static class EventModelContractValidation
{
    private const string NaturalKeySymbolsMetaKey = "NaturalKeySymbols";
    private const string AccountXIdSymbolMetaKey = "AccountXIdSymbol";

    /// <summary>
    /// Ensures NaturalKeySymbols is present, parses as a JSON array of strings,
    /// and that each symbol maps to the event model schema (top-level attribute or path rooted on a declared attribute).
    /// </summary>
    public static void ValidateNaturalKeySymbols(ModelDto eventModel)
    {
        if (eventModel == null)
            throw new ArgumentNullException(nameof(eventModel));

        if (!TryParseNaturalKeySymbols(eventModel, out var symbols, out var errorMessage))
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { NaturalKeySymbolsMetaKey, errorMessage! }
            });
        }

        var errors = new Dictionary<string, string>();
        foreach (var raw in symbols)
        {
            var symbol = raw?.Trim();
            if (string.IsNullOrEmpty(symbol))
            {
                errors["NaturalKeySymbols"] = "NaturalKeySymbols array contains an empty entry.";
                continue;
            }

            if (!SymbolDeclaredOnEventModel(symbol, eventModel) &&
                !symbol.Equals("type", StringComparison.InvariantCultureIgnoreCase) &&
                !symbol.Equals("extaccountid", StringComparison.InvariantCultureIgnoreCase))
            {
                errors[$"NaturalKeySymbol:{symbol}"] =
                    $"Symbol '{symbol}' in {NaturalKeySymbolsMetaKey} is not declared on event model '{eventModel.Name}'. " +
                    "Each symbol must match an attribute Symbol, or for dotted paths the first segment must match an attribute (e.g. parent.child).";
            }
        }

        if (errors.Count > 0)
            throw new APIErrorsException(errors);
    }

    /// <summary>
    /// Parses NaturalKeySymbols metadata as a JSON string array without validating symbol declarations.
    /// </summary>
    public static bool TryParseNaturalKeySymbols(ModelDto eventModel, out string[] symbols, out string? errorMessage)
    {
        symbols = Array.Empty<string>();
        errorMessage = null;

        var naturalKeyJson = GetMetaDataString(eventModel, NaturalKeySymbolsMetaKey);
        if (string.IsNullOrWhiteSpace(naturalKeyJson))
        {
            errorMessage =
                $"{NaturalKeySymbolsMetaKey} must be set in modelMetaData with a JSON array of attribute symbols used to compose the business natural key (e.g. \"[\\\"orderId\\\"]\").";
            return false;
        }

        try
        {
            symbols = JsonHelper.DeserializeObject<string[]>(naturalKeyJson, false) ?? Array.Empty<string>();
        }
        catch (JsonException ex)
        {
            errorMessage = $"{NaturalKeySymbolsMetaKey} must be valid JSON array of strings. Parse error: {ex.Message}";
            return false;
        }

        if (symbols.Length == 0)
        {
            errorMessage = $"{NaturalKeySymbolsMetaKey} must contain at least one symbol.";
            return false;
        }

        return true;
    }

    public static void ValidateAccountXIdSymbol(ModelDto eventModel)
    {
        var warnings = GetAccountXIdSymbolWarnings(eventModel);
        if (warnings.Count > 0)
            throw new APIErrorsException(warnings.ToDictionary(_ => AccountXIdSymbolMetaKey, w => w));
    }

    public static List<string> GetAccountXIdSymbolWarnings(ModelDto eventModel)
    {
        var warnings = new List<string>();
        if (eventModel == null)
            return warnings;

        var path = GetMetaDataString(eventModel, AccountXIdSymbolMetaKey);
        if (string.IsNullOrWhiteSpace(path))
        {
            warnings.Add(
                $"{AccountXIdSymbolMetaKey} must be set in modelMetaData with the attribute symbol path used for loyalty account lookup.");
            return warnings;
        }

        if (!SymbolDeclaredOnEventModel(path.Trim(), eventModel))
        {
            warnings.Add(
                $"Symbol path '{path}' in {AccountXIdSymbolMetaKey} is not declared on event model '{eventModel.Name}'.");
        }

        return warnings;
    }

    private static string? GetMetaDataString(ModelDto model, string key) =>
        model.ModelMetaData != null && model.ModelMetaData.TryGetValue(key, out var v) ? v : null;

    /// <summary>
    /// True if <paramref name="symbol"/> matches a top-level attribute, or dotted paths whose first segment matches an attribute.
    /// </summary>
    public static bool SymbolDeclaredOnEventModel(string symbol, ModelDto eventModel)
    {
        if (eventModel.Attributes == null || eventModel.Attributes.Count == 0)
            return false;

        if (eventModel.Attributes.Any(a =>
                a.Symbol != null &&
                string.Equals(a.Symbol, symbol, StringComparison.OrdinalIgnoreCase)))
            return true;

        var dot = symbol.IndexOf('.');
        if (dot <= 0)
            return false;

        var root = symbol[..dot];
        return eventModel.Attributes.Any(a =>
            a.Symbol != null &&
            string.Equals(a.Symbol, root, StringComparison.OrdinalIgnoreCase));
    }
}
