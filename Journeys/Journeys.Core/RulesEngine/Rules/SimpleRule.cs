
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;

namespace Journeys.Core.RulesEngine.Rules
{
    public class SimpleRule<T> : RuleBase
    {
        public override string Kind => RuleKindDiscriminators.SimpleRule;

        public IValueProvider? LeftProvider { get; set; }
        public IValueProvider? RightProvider { get; set; }
        public IEvaluatable? Evaluator { get; set; }

        public SimpleRule() { }
        public SimpleRule(IValueProvider leftProvider, IValueProvider rightProvider, IEvaluatable evaluator)
        {
            LeftProvider = leftProvider;
            RightProvider = rightProvider;
            Evaluator = evaluator;
        }

        public async override Task<bool> Evaluate(RulesEngineState obj, CancellationToken token)
        {
            if (Evaluator == null)
            {
                throw new NullReferenceException("The IEvaluatable property must be set prior to evaluating the rule");
            }

            if (LeftProvider == null || RightProvider == null)
            {
                throw new NullReferenceException("Both left and right value providers must be set prior to evaluating the rule");
            }

            var leftValue = LeftProvider.GetValue<T>(obj, token);
            var rightValue = RightProvider.GetValue<T>(obj, token);

            await Task.WhenAll(leftValue, rightValue);

            return Evaluator.Evaluate(leftValue.Result, rightValue.Result);
        }
    }

}

