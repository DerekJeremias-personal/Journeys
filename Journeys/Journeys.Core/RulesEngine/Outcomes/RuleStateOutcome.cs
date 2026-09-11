using Journeys.Core.Interfaces.Services;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Outcomes
{
    public class RuleStateOutcome : OutcomeBase
    {
        public override string Kind => OutcomeKindDiscriminators.RuleStateOutcome;

        public async override Task<OutcomeResult> AwardOutcomeAsync(RulesEngineState state, ILoyaltyAccountService loyaltyAccountService, CancellationToken token)
        {
            //TODO: Implement
            return null;
        }

        public async override Task<OutcomeResult> CalculateOutcomeAsync(RulesEngineState state, CancellationToken token)
        {
            //TODO: Implement
            return null;
        }
    }
}
