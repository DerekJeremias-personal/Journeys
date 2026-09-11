using System.Text.Json;

namespace Journeys.Core.Interfaces.Utilities;

public interface IBatchFileReader
{ 
    IAsyncEnumerable<JsonElement> GetNextGroupAsync(Stream fileStream);
}
