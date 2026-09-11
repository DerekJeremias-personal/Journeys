using System.Text.Json;
using Journeys.Core.Interfaces.Utilities;
using Journeys.Core.Models;

namespace Journeys.Core.Utility.BatchFileReaders;

public class NdjsonBatchFileReader(DropboxConfig? dropboxConfig) : IBatchFileReader
{
    public async IAsyncEnumerable<JsonElement> GetNextGroupAsync(Stream fileStream)
    {
        var reader = new StreamReader(fileStream);

        while (await reader.ReadLineAsync() is { } line)
        {
            yield return JsonDocument.Parse(line).RootElement;
        }
    }
}
