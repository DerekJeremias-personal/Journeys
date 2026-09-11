namespace Journeys.Core.RulesEngine.Comparitors
{
    public class BoolEvaluation : IEvaluatable
    {
        public bool Evaluate<T>(T left, T right)
        {
            if (left == null || right == null) return false;

            return left?.Equals(right) ?? right == null;
        }
    }
}
