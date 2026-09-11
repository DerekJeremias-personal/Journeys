using System;
using Journeys.Core.Extensions;
using Xunit;

namespace Journeys.Tests.Utility
{
    public class DateTimeOffsetMinDateTests
    {
        [Fact]
        public void IsMinDate_true_for_DateTimeOffset_MinValue()
        {
            Assert.True(DateTimeOffset.MinValue.IsMinDate());
        }

        [Fact]
        public void IsNullOrMinDate_true_for_year_0001_utc()
        {
            DateTimeOffset? unset = new DateTimeOffset(1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.True(unset.IsNullOrMinDate());
        }

        [Fact]
        public void IsNullOrMinDate_true_for_null()
        {
            DateTimeOffset? missing = null;
            Assert.True(missing.IsNullOrMinDate());
        }

        [Fact]
        public void IsNullOrMinDate_false_for_real_event_time()
        {
            DateTimeOffset? value = new DateTimeOffset(2026, 9, 15, 16, 30, 21, TimeSpan.Zero);
            Assert.False(value.IsNullOrMinDate());
        }
    }
}
