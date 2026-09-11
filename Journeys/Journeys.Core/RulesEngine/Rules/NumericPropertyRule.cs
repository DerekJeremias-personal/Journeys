using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Comparitors.Enums;

namespace Journeys.Core.RulesEngine.Rules
{
    public class NumericPropertyRule : SimpleRule<decimal>
    {
        public override string Kind => RuleKindDiscriminators.NumericPropertyRule;

        public NumericPropertyRule() { }
        public NumericPropertyRule(string propertyPath, NumEvalType comparison, decimal constant)
            : base(new PathValueProvider(propertyPath), new ConstantValueProvider(constant), new NumericEvaluation(comparison)) { }

        public NumericPropertyRule(string leftPropertyPath, NumEvalType comparison, string rightPropertyPath)
            : base(new PathValueProvider(leftPropertyPath), new PathValueProvider(rightPropertyPath), new NumericEvaluation(comparison)) { }
    }
}

