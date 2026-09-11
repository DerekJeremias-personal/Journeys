namespace Journeys.DTO.Responses
{
    /// <summary>
    /// Health payload for GET /Health. Serialised as camelCase to satisfy the
    /// exp admin-web <c>HealthStatusSchema</c> (status/timestamp required; the rest optional).
    /// </summary>
    public class HealthStatusResponse
    {
        /// <summary>"healthy" | "degraded" | "unhealthy".</summary>
        public string Status { get; set; } = "healthy";

        /// <summary>ISO-8601 with offset (DateTimeOffset serialises with offset by default).</summary>
        public DateTimeOffset Timestamp { get; set; }

        public string? Environment { get; set; }

        /// <summary>Optional per-dependency health, e.g. { "cosmos": true }.</summary>
        public Dictionary<string, bool>? Services { get; set; }
    }
}
