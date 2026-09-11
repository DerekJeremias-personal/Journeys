
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Rules;

namespace Journeys.Core.RulesEngine.Rules.Composite
{
    public class NotRule : CompositeRuleBase
    {
        public override string Kind => RuleKindDiscriminators.NotRule;

        public async override Task<bool> Evaluate(RulesEngineState obj, CancellationToken token)
        {
            if (Children.Count != 1)
            {
                throw new InvalidOperationException("The NotRule must have exactly one child rule");
            }
            return !await Children.Single().Evaluate(obj, token);
        }
    }

}

