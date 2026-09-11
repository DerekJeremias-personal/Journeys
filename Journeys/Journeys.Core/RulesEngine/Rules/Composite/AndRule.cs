

using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Rules;

namespace Journeys.Core.RulesEngine.Rules.Composite
{
    public class AndRule : CompositeRuleBase
    {
        public override string Kind => RuleKindDiscriminators.AndRule;

        public async override Task<bool> Evaluate(RulesEngineState obj, CancellationToken token)
        {
            var evals = Children.Select(x => x.Evaluate(obj, token)).ToArray();
            await Task.WhenAll(evals);
            return evals.All(x => x.Result);
        }
    }

}

