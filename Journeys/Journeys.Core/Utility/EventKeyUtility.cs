using System;

namespace Journeys.Core.Utility
{
    /// <summary>
    /// Centralized generation of the event key used for ledger entries, TimeToLive records, points details, etc.
    /// Format: eventtype|eventid (lowercase). Throws if either argument is null.
    /// </summary>
    public static class EventKeyUtility
    {
        /// <summary>
        /// Produces the canonical event key: lowercase eventType and eventId joined by '|'.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="eventType"/> or <paramref name="eventId"/> is null.</exception>
        public static string ToEventKey(string eventType, string eventId)
        {
            if (eventType == null)
                throw new ArgumentNullException(nameof(eventType));
            if (eventId == null)
                throw new ArgumentNullException(nameof(eventId));
            return $"{eventType.ToLowerInvariant()}|{eventId.ToLowerInvariant()}";
        }
    }
}
