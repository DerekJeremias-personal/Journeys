using System;
using Xunit;
using Journeys.Core.Utility;

namespace Journeys.Tests.Utility
{
    public class DateTimeExtensionsTests
    {
        [Fact]
        public void GetMonthRangeAsYYYYMM_SingleMonth_ReturnsCorrectFormat()
        {
            // Arrange
            var startDate = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
            var endDate = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

            // Act
            var result = startDate.GetMonthRangeAsYYYYMM(endDate);

            // Assert
            Assert.Equal("\"202401\"", result);
        }

        [Fact]
        public void GetMonthRangeAsYYYYMM_MultipleMonths_ReturnsCommaSeparatedList()
        {
            // Arrange
            var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var endDate = new DateTimeOffset(2024, 3, 31, 23, 59, 59, TimeSpan.Zero);

            // Act
            var result = startDate.GetMonthRangeAsYYYYMM(endDate);

            // Assert
            Assert.Equal("\"202401\",\"202402\",\"202403\"", result);
        }

        [Fact]
        public void GetMonthRangeAsYYYYMM_CrossYearBoundary_ReturnsCorrectMonths()
        {
            // Arrange
            var startDate = new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero);
            var endDate = new DateTimeOffset(2024, 2, 28, 23, 59, 59, TimeSpan.Zero);

            // Act
            var result = startDate.GetMonthRangeAsYYYYMM(endDate);

            // Assert
            Assert.Equal("\"202312\",\"202401\",\"202402\"", result);
        }

        [Fact]
        public void GetMonthRangeAsYYYYMMList_MultipleMonths_ReturnsList()
        {
            // Arrange
            var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var endDate = new DateTimeOffset(2024, 3, 31, 23, 59, 59, TimeSpan.Zero);

            // Act
            var result = startDate.GetMonthRangeAsYYYYMMList(endDate);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("\"202401\"", result[0]);
            Assert.Equal("\"202402\"", result[1]);
            Assert.Equal("\"202403\"", result[2]);
        }

        [Fact]
        public void ToYYYYMM_SingleDate_ReturnsCorrectFormat()
        {
            // Arrange
            var date = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);

            // Act
            var result = date.ToYYYYMM();

            // Assert
            Assert.Equal("\"202406\"", result);
        }

        [Fact]
        public void GetMonthRangeAsYYYYMM_StartAfterEnd_ThrowsArgumentException()
        {
            // Arrange
            var startDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
            var endDate = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => startDate.GetMonthRangeAsYYYYMM(endDate));
        }

        [Fact]
        public void GetMonthRangeAsYYYYMM_SameMonth_ReturnsSingleMonth()
        {
            // Arrange
            var startDate = new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero);
            var endDate = new DateTimeOffset(2024, 5, 31, 23, 59, 59, TimeSpan.Zero);

            // Act
            var result = startDate.GetMonthRangeAsYYYYMM(endDate);

            // Assert
            Assert.Equal("\"202405\"", result);
        }
    }
} 