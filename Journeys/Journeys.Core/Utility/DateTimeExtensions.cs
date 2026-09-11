using System;
using System.Collections.Generic;
using System.Linq;

namespace Journeys.Core.Utility
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Generates a comma-separated list of quoted "yyyyMM" formatted months between start and end dates (inclusive)
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <returns>Comma-separated string of quoted yyyyMM formatted months</returns>
        public static string GetMonthRangeAsYYYYMM(this DateTimeOffset startDate, DateTimeOffset endDate)
        {
            if (startDate > endDate)
            {
                throw new ArgumentException("Start date cannot be after end date", nameof(startDate));
            }

            var months = new List<string>();
            var currentDate = new DateTimeOffset(startDate.Year, startDate.Month, 1, 0, 0, 0, startDate.Offset);
            var endMonth = new DateTimeOffset(endDate.Year, endDate.Month, 1, 0, 0, 0, endDate.Offset);

            while (currentDate <= endMonth)
            {
                months.Add($"\"{currentDate.ToString("yyyyMM")}\"");
                currentDate = currentDate.AddMonths(1);
            }

            return string.Join(",", months);
        }

        /// <summary>
        /// Generates a list of quoted "yyyyMM" formatted months between start and end dates (inclusive)
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <returns>List of quoted yyyyMM formatted months</returns>
        public static List<string> GetMonthRangeAsYYYYMMList(this DateTimeOffset startDate, DateTimeOffset endDate)
        {
            if (startDate > endDate)
            {
                throw new ArgumentException("Start date cannot be after end date", nameof(startDate));
            }

            var months = new List<string>();
            var currentDate = new DateTimeOffset(startDate.Year, startDate.Month, 1, 0, 0, 0, startDate.Offset);
            var endMonth = new DateTimeOffset(endDate.Year, endDate.Month, 1, 0, 0, 0, endDate.Offset);

            while (currentDate <= endMonth)
            {
                months.Add($"\"{currentDate.ToString("yyyyMM")}\"");
                currentDate = currentDate.AddMonths(1);
            }

            return months;
        }

        /// <summary>
        /// Gets the quoted "yyyyMM" formatted string for a DateTimeOffset
        /// </summary>
        /// <param name="date">The date to format</param>
        /// <returns>Quoted yyyyMM formatted string</returns>
        public static string ToYYYYMM(this DateTimeOffset date)
        {
            return $"\"{date.ToString("yyyyMM")}\"";
        }
    }
} 