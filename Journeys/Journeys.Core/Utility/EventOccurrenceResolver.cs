using Backend.Dto.Interfaces;
using Journeys.Core.Extensions;
using Journeys.Core.RulesEngine.Providers;

namespace Journeys.Core.Utility
{
    /// <summary>
    /// Resolves TimeOfOccurrence from an inbound event payload.
    /// Loyalty-account process payloads nest the timestamp under AccountDetails.
    /// </summary>
    public static class EventOccurrenceResolver
    {
        public static DateTimeOffset? TryGet(IDynamicEntity? data, string? timeOfEventSymbol)
        {
            if (data == null || string.IsNullOrWhiteSpace(timeOfEventSymbol))
                return null;

            var direct = Read(data, timeOfEventSymbol);
            if (!direct.IsNullOrMinDate())
                return direct;

            var details = new PathValueProvider("accountDetails").GetValue<IDynamicEntity>(data);
            if (details == null)
                return null;

            var nested = Read(details, timeOfEventSymbol);
            return nested.IsNullOrMinDate() ? null : nested;
        }

        private static DateTimeOffset? Read(IDynamicEntity data, string symbol)
        {
            return new PathValueProvider(symbol).GetValue<DateTimeOffset?>(data);
        }
    }
}
