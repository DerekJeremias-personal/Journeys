using System.Text.Json;
using Journeys.Core.Interfaces.Utilities;
using Journeys.Core.Models;

namespace Journeys.Core.Utility.BatchFileReaders;

public class JsonBatchFileReader(DropboxConfig? dropboxConfig) : IBatchFileReader
{
    public async IAsyncEnumerable<JsonElement> GetNextGroupAsync(Stream fileStream)
    {
        var jsonDocument = await JsonDocument.ParseAsync(fileStream);

        if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new Exception("JSON batch file does not contain array as root element.");
        }

        foreach (var objectElement in jsonDocument.RootElement.EnumerateArray())
        {
            yield return objectElement;
        }
    }
}
