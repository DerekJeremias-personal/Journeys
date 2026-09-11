using Journeys.DTO.Models;
using Journeys.Core.Models;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Journeys.Core.Caching;
using System.IO;

namespace Journeys.Core.RulesEngine.Outcomes
{
    public class DepositPointsOutcome : PointOutcomeBase
    {
        public override string Kind => OutcomeKindDiscriminators.DepositPointsOutcome;

        public DepositPointsOutcome()
        {
            Id = "ADD00000-0000-0000-0000-000000000000";
        }

        public string PointSourceAccountId { get; set; }

        public decimal PointsPerDollar { get; set; }
        
        public IValueProvider? EarnDateProvider { get; set; }

        public IValueProvider? DollarAmountProvider { get; set; }

        [JsonIgnore]
        public decimal AwardedPoints { get; set; } = 0;

        [JsonIgnore]
        public decimal RoundedAwardedPoints { get; set; } = 0;

        public async override Task<OutcomeResult> AwardOutcomeAsync(RulesEngineState state, ILoyaltyAccountService loyaltyAccountService, CancellationToken token)
        {
            if (string.IsNullOrEmpty(state?.TenantId))
                throw new ArgumentNullException(nameof(state.TenantId));

            if (string.IsNullOrEmpty(state.EventModelId))
                throw new ArgumentNullException(state.EventModelId);

            await EnsurePointAccounType(state.TenantId);

            var primaryPat = AffectedPointAccountTypes.FirstOrDefault();
            if (primaryPat == null || string.IsNullOrWhiteSpace(primaryPat.Id))
                throw new InvalidOperationException("DepositPointsOutcome must have point account type specified.");

            DateTimeOffset? earnDate = DateTime.UtcNow;
            if (EarnDateProvider != null)
                earnDate = await EarnDateProvider?.GetValue<DateTimeOffset>(state, token);

            foreach (var pat in AffectedPointAccountTypes)
            {
                var dto = new PointLedgerDto
                {
                    TenantId = state.TenantId,
                    AccountId = state.LoyaltyAccountId,
                    PointAccountTypeId = pat.Id,
                    LedgerEntries = new List<LedgerEntryDto> 
                    {
                        new LedgerEntryDto
                        {
                            EntryId = Guid.NewGuid().ToString(),
                            EarnDate = earnDate,
                            EventId = EventId,
                            EventType = EventType,
                            OutcomeId = Id,
                            PointsDeposited = PointOutcomeBase.RoundToSignificantDigits(AwardedPoints, pat?.RoundingDecimalPlaces ?? 0, pat?.RoundingOption ?? MidpointRounding.AwayFromZero),
                            Type = LedgerEntryType.Deposit
                        }
                    }
                };

                var res = await loyaltyAccountService.UpsertLoyaltyAccountPointsAsync(state.TenantId, dto, state.LoyaltyAccount);
                if (res == null) throw new Exception("Failed to update target point ledger.");

                dto.Id = res.Id;
                PointResults.Add(dto);

                //TODO: Update Point source (reduce available points by this amount)
                if (!string.IsNullOrEmpty(PointSourceAccountId))
                {
                }

            }
            return new OutcomeResult
            {
                IssuingOutcomeId = $"{this.Id}-Awarded",
                IssuingOutcomeKind = this.Kind,
                IssuingOutcome = this,
                IssuingEventId = EventId,
                IssuingEventType = this.EventType,
                PointAccountTypeId = primaryPat.Id,
                RuleSetId = state.AppliedRuleSets.FirstOrDefault()
            };
        }

        public async override Task<OutcomeResult> CalculateOutcomeAsync(RulesEngineState state, CancellationToken token)
        {
            if (string.IsNullOrEmpty(state?.TenantId))
                throw new ArgumentNullException(nameof(state.TenantId));

            if (DollarAmountProvider == null)
                throw new InvalidOperationException("DepositPointsOutcome: DollarAmountProvider is required.");

            var amount = await DollarAmountProvider.GetValue<decimal?>(state, token);
            AwardedPoints = amount * PointsPerDollar ?? 0;

            var pat = this.AffectedPointAccountTypes?.FirstOrDefault();
            RoundedAwardedPoints = PointOutcomeBase.RoundToSignificantDigits(AwardedPoints, pat?.RoundingDecimalPlaces ?? 0, pat?.RoundingOption ?? MidpointRounding.AwayFromZero);

            EventId = await ResolveEventIdAsync(state, token);
            if (string.IsNullOrWhiteSpace(EventId))
            {
                //throw new Exception("DepositPointsOutcome: No EventId provided");
                //HACK: Due to model mismatch and multi-model rule-trees we run into the issue
                //where a qualifying rule needs to be able to run against a campaign but then
                //all rules run.  With an "always run" rule on the economy it gets here on 
                //a dealer and pukes.  Returning null and handling the null reference in the caller
                //was the faster fix.
                return null;
            }
            EventType = await ResolveEventTypeAsync(state, token);
            if (string.IsNullOrWhiteSpace(EventType))
            {
                throw new Exception("DepositPointsOutcome: No EventType provided");
            }
            return new OutcomeResult
            {
                IssuingOutcomeId = $"{this.Id}-Calculated",
                IssuingOutcomeKind = this.Kind,
                IssuingOutcome = this,
                IssuingEventId = EventId,
                IssuingEventType = this.EventType,
                PointsAwarded = RoundedAwardedPoints
            };
        }

        
    }
}
