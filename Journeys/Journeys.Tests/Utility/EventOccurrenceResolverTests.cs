using System;
using Backend.Dto.Dynamic;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility
{
    public class EventOccurrenceResolverTests
    {
        [Fact]
        public void TryGet_reads_timestamp_nested_under_accountDetails()
        {
            var expected = new DateTimeOffset(2026, 9, 15, 16, 30, 21, TimeSpan.Zero);
            var data = DynamicHelper.Import(new
            {
                ExtAccountId = "test_003",
                Type = "user",
                AccountDetails = new
                {
                    profileid = "test_003",
                    timestamp = expected
                }
            });

            var result = EventOccurrenceResolver.TryGet(data, "timestamp");

            Assert.NotNull(result);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TryGet_reads_top_level_timestamp()
        {
            var expected = new DateTimeOffset(2025, 9, 15, 16, 30, 20, TimeSpan.Zero);
            var data = DynamicHelper.Import(new
            {
                orderId = "xyz_021",
                timestamp = expected
            });

            var result = EventOccurrenceResolver.TryGet(data, "timestamp");

            Assert.Equal(expected, result);
        }

        [Fact]
        public void TryGet_returns_null_when_symbol_missing()
        {
            var data = DynamicHelper.Import(new { ExtAccountId = "test_003", Type = "user" });

            Assert.Null(EventOccurrenceResolver.TryGet(data, "timestamp"));
        }

        [Fact]
        public void TryGet_returns_null_when_symbol_blank()
        {
            var data = DynamicHelper.Import(new { timestamp = DateTimeOffset.UtcNow });

            Assert.Null(EventOccurrenceResolver.TryGet(data, null));
            Assert.Null(EventOccurrenceResolver.TryGet(data, ""));
        }
    }
}
