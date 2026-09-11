using System.Collections.Generic;

namespace Journeys.DTO.Responses
{
    /// <summary>
    /// Continuation wrapper for tenant list APIs. Not present in <c>Backend.Dto</c>; matches JSON from backend list endpoints.
    /// </summary>
    public class ContinuableList<T>
    {
        public List<T> Items { get; set; } = new List<T>();

        public string? ContinuationToken { get; set; }
    }
}
