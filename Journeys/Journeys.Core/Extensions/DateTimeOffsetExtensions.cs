using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Extensions
{
    public static class DateTimeOffsetExtensions
    {
        /// <summary>
        /// Ignore timezone adjusted MinValues for dates
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static bool IsMinDate(this DateTimeOffset date)
        {
            // DateTimeOffset.MinValue is 0001-01-01. Timezone-adjusted mins can land in year 0.
            return date.Year <= 1;
        }

        /// <summary>
        /// Ignore timezone adjusted MinValues for dates
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static bool IsNullOrMinDate(this DateTimeOffset? date)
        {
            if (date == null) return true;
            return date.Value.IsMinDate();
        }
    }

    public static class DateTimeExtensions
    {
        /// <summary>
        /// Ignore timezone adjusted MinValues for dates
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static bool IsMinDate(this DateTime date)
        {
            return date.Year <= 1;
        }
    }

}

