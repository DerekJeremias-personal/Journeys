using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Microsoft.Extensions.Logging;

namespace Journeys.Core.RulesEngine.Rules
{
    public class DatePropertyRule : SimpleRule<decimal>
    {
        public override string Kind => RuleKindDiscriminators.DatePropertyRule;

        public DatePropertyRule() { }

        public DatePropertyRule(string propertyPath, DateEvalType comparison, decimal constant, ILogger<DateEvaluation> logger)
            : base(new PathValueProvider(propertyPath), new ConstantValueProvider(constant), new DateEvaluation(comparison, logger)) { }

        public DatePropertyRule(string leftPropertyPath, DateEvalType comparison, string rightPropertyPath, ILogger<DateEvaluation> logger)
            : base(new PathValueProvider(leftPropertyPath), new PathValueProvider(rightPropertyPath), new DateEvaluation(comparison, logger)) { }
    }
}

