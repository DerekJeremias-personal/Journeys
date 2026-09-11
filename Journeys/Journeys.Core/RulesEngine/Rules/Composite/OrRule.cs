
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Rules;

namespace Journeys.Core.RulesEngine.Rules.Composite
{
    public class OrRule : CompositeRuleBase
    {
        public override string Kind => RuleKindDiscriminators.OrRule;

        public async override Task<bool> Evaluate(RulesEngineState obj, CancellationToken token)
        {
            var cts = new CancellationTokenSource();
            var managedToken = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token).Token;
            var evals = Children.Select(x => x.Evaluate(obj, managedToken)).ToList();
            do
            {
                var next = await Task.WhenAny(evals);
                evals.Remove(next);

                if (next.Result)
                {
                    cts.Cancel();
                    return true;
                }
            } while (evals.Any());
            return evals.All(x => x.Result);
        }
    }

}

