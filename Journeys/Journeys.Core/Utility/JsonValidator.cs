using System.Text.Json;
using Json.Schema;

namespace Journeys.Core.Utility;

public class JsonValidator
{
    private JsonDocument? _jsonDocument;
    private JsonSchema? _jsonSchema;
    private EvaluationResults? _evaluationResults;

    public void SetModelFromFile(string modelFilePath)
    {
        _jsonSchema = JsonSchema.FromFile(modelFilePath);
    }

    public void SetModelFromString(string modelString)
    {
        _jsonSchema = JsonSchema.FromText(modelString);
    }
    
    public bool ValidateString(string jsonString)
    {
        if (_jsonSchema == null)
        {
            throw new Exception("JSON Model not set");
        }
        
        _jsonDocument = JsonDocument.Parse(jsonString);

        _evaluationResults = _jsonSchema.Evaluate(_jsonDocument);

        return _evaluationResults.IsValid;
    }

    public async Task<bool> ValidateStreamAsync(Stream fileStream)
    {
        if (_jsonSchema == null)
        {
            throw new Exception("JSON Model not set");
        }
        
        _jsonDocument = await JsonDocument.ParseAsync(fileStream);

        _evaluationResults = _jsonSchema.Evaluate(_jsonDocument);

        return _evaluationResults.IsValid;
    }

    public IReadOnlyDictionary<string, string>? GetErrors()
    {
        return _evaluationResults?.Errors;
    }

    public JsonDocument? GetConfigDocument()
    {
        return _jsonDocument;
    }
}
