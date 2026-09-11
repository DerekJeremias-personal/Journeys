using Journeys.Core.RulesEngine.Comparitors.Enums;
using Microsoft.Extensions.Logging;

namespace Journeys.Core.RulesEngine.Comparitors
{
    public class DateEvaluation : IEvaluatable
    {
        private readonly ILogger<DateEvaluation> _logger;

        public DateEvaluation(ILogger<DateEvaluation> logger)
        {
            _logger = logger;
        }

        public DateEvaluation(DateEvalType comparison, ILogger<DateEvaluation> logger)
        {
            Comparison = comparison;
            _logger = logger;
        }

        private static Dictionary<DateEvalType, Func<DateTimeOffset, DateTimeOffset, bool>> _comparisonMap = new Dictionary<DateEvalType, Func<DateTimeOffset, DateTimeOffset, bool>>
               {
                   { DateEvalType.Equal, (l, r) => l == r },
                   { DateEvalType.GreaterThan, (l, r) => l > r },
                   { DateEvalType.GreaterThanOrEqual, (l, r) => l >= r },
                   { DateEvalType.LessThan, (l, r) => l < r },
                   { DateEvalType.LessThanOrEqual, (l, r) => l <= r }
               };

        public DateEvalType Comparison { get; set; }

        public bool Evaluate<T>(T left, T right)
        {
            if (left == null || right == null) return false;

            var leftC = left as DateTimeOffset?;
            var rightC = right as DateTimeOffset?;

            try
            {
                if (leftC == null || rightC == null)
                {
                    throw new InvalidOperationException("Both left and right values must be of type DateTimeOffset");
                }

                if (_comparisonMap.TryGetValue(Comparison, out var func))
                {
                    return func(leftC.Value, rightC.Value);
                }
                throw new InvalidOperationException($"Invalid comparison type of {Enum.GetName(typeof(DateEvalType), Comparison)}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex);
                return false;
            }
        }
    }
}
