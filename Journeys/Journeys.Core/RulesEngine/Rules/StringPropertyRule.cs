
using Backend.Dto.Dynamic;
using Backend.Dto.Interfaces;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;

namespace Journeys.Core.RulesEngine.Rules
{
    public class StringPropertyRule : SimpleRule<string>
    {
        public override string Kind => RuleKindDiscriminators.StringPropertyRule;

        public StringPropertyRule() { }
        public StringPropertyRule(string leftPropertyPath, StringEvalType comparison, ConstantValueProvider constant)
            : base(new PathValueProvider(leftPropertyPath), constant, new StringEvaluation(comparison)) { }

        public StringPropertyRule(string leftPropertyPath, StringEvalType comparison, PathValueProvider rightProperty)
            : base(new PathValueProvider(leftPropertyPath), rightProperty, new StringEvaluation(comparison)) { }


        public override async Task<bool> Evaluate(RulesEngineState obj, CancellationToken token)
        {
            if (Evaluator == null)
            {
                throw new NullReferenceException("The IEvaluatable property must be set prior to evaluating the rule");
            }

            if (LeftProvider == null || RightProvider == null)
            {
                throw new NullReferenceException("Both left and right value providers must be set prior to evaluating the rule");
            }

            if (((StringEvaluation)Evaluator).Comparison == StringEvalType.InCollection)
            {
                var leftValue = LeftProvider.GetValue<IDynamicEntity>(obj, token);
                var rightValue = RightProvider.GetValue<DynamicList>(obj, token);
                await Task.WhenAll(leftValue, rightValue);

                if (leftValue.Result == null || rightValue.Result == null)
                {
                    throw new ArgumentNullException("Both left and right values must be non-null for Contains comparison");
                }

                return Evaluator.Evaluate(leftValue.Result, rightValue.Result);
            }
            else
            {
                var leftValue = LeftProvider.GetValue<string>(obj, token);
                var rightValue = RightProvider.GetValue<string>(obj, token);

                await Task.WhenAll(leftValue, rightValue);

                return Evaluator.Evaluate(leftValue.Result, rightValue.Result);
            }
        }
    }
}

