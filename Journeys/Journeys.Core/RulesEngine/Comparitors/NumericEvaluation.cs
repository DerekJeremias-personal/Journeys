using Journeys.Core.RulesEngine.Comparitors.Enums;

namespace Journeys.Core.RulesEngine.Comparitors
{
    public class NumericEvaluation : IEvaluatable
    {
        private static Dictionary<NumEvalType, Func<decimal, decimal, bool>> _comparisonMap = new Dictionary<NumEvalType, Func<decimal, decimal, bool>>
        {
            { NumEvalType.Equal, (l, r) => l == r },
            { NumEvalType.GreaterThan, (l, r) => l > r },
            { NumEvalType.GreaterThanOrEqual, (l, r) => l >= r },
            { NumEvalType.LessThan, (l, r) => l < r },
            { NumEvalType.LessThanOrEqual, (l, r) => l <= r }
        };

        public NumericEvaluation() { }
        public NumericEvaluation(NumEvalType comparison)
        {
            Comparison = comparison;
        }
        public NumEvalType Comparison { get; set; }
        public bool Evaluate<T>(T left, T right)
        {
            if (left == null || right == null) return false;

            if (_comparisonMap.TryGetValue(Comparison, out var func))
            {
                return func(Convert.ToDecimal(left), Convert.ToDecimal(right));
            }
            throw new InvalidOperationException($"Invalid comparison type of {Enum.GetName(Comparison)}");
        }
    }
}
