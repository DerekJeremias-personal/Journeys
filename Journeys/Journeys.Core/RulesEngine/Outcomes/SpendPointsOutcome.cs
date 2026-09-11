using Journeys.DTO.Models;
using Journeys.Core.Models;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Journeys.Core.RulesEngine.Providers;
using Microsoft.Extensions.Logging;

namespace Journeys.Core.RulesEngine.Outcomes
{
    public class SpendPointsOutcome : PointOutcomeBase
    {
        private readonly ILogger<SpendPointsOutcome> _logger;

        public override string Kind => OutcomeKindDiscriminators.SpendPointsOutcome;

        public SpendPointsOutcome()
        {
            Id = "DEC00000-0000-0000-0000-000000000000";
        }

        public string PointSourceAccountId { get; set; }
        public string LedgerTypeId { get; set; }
        [JsonIgnore]
        public decimal PointsWithdrawn { get; set; } = 0;

        [JsonIgnore]
        public decimal RoundedPointsWithdrawn { get; set; } = 0;

        public IValueProvider? WithdrawlAmountProvider { get; set; }


        public async override Task<OutcomeResult> AwardOutcomeAsync(RulesEngineState state, ILoyaltyAccountService loyaltyAccountService, CancellationToken token)
        {
            try
            {
                if (string.IsNullOrEmpty(state?.TenantId))
                    throw new ArgumentNullException(nameof(state.TenantId));

                if (string.IsNullOrEmpty(state.EventModelId))
                    throw new ArgumentNullException(state.EventModelId);
                await EnsurePointAccounType(state.TenantId);

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
                            BurnDate = DateTime.UtcNow,
                            EventId = EventId,
                            EventType = EventType,
                            OutcomeId = Id,
                            PointsWithdrawn = PointOutcomeBase.RoundToSignificantDigits(PointsWithdrawn, pat?.RoundingDecimalPlaces ?? 0, pat?.RoundingOption ?? MidpointRounding.AwayFromZero),
                            Type = LedgerEntryType.Withdrawl
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
            }
            catch (ArgumentNullException ane)
            {
                _logger?.LogError(ane, "SpendPointsOutcome: Missing required state properties.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex);
                throw;
            }

            return new OutcomeResult
            {
                IssuingOutcomeId = $"{this.Id}-Awarded",
                IssuingOutcomeKind = this.Kind,
                IssuingOutcome = this,
                IssuingEventId = EventId,
                IssuingEventType = this.EventType,
            };
        }

        public async override Task<OutcomeResult> CalculateOutcomeAsync(RulesEngineState state, CancellationToken token)
        {
            try
            {
                if (string.IsNullOrEmpty(state?.TenantId))
                    throw new ArgumentNullException(nameof(state.TenantId));

                PointsWithdrawn = await WithdrawlAmountProvider.GetValue<decimal?>(state, token) ?? 0;

                var pat = this.AffectedPointAccountTypes?.FirstOrDefault();
                RoundedPointsWithdrawn = PointOutcomeBase.RoundToSignificantDigits(PointsWithdrawn, pat?.RoundingDecimalPlaces ?? 0, pat?.RoundingOption ?? MidpointRounding.AwayFromZero);


                EventId = await ResolveEventIdAsync(state, token);
                if (string.IsNullOrWhiteSpace(EventId))
                {
                    throw new Exception("SpendPointsOutcome: No EventId provided");
                }
                EventType = await ResolveEventTypeAsync(state, token);
                if (string.IsNullOrWhiteSpace(EventType))
                {
                    throw new Exception("SpendPointsOutcome: No EventType provided");
                }
                return new OutcomeResult
                {
                    IssuingOutcomeId = $"{this.Id}-Calculated",
                    IssuingOutcomeKind = this.Kind,
                    IssuingOutcome = this,
                    IssuingEventId = EventId,
                    IssuingEventType = this.EventType,
                    PointsRevoked = RoundedPointsWithdrawn
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in SpendPointsOutcome.CalculateOutcomeAsync");
                throw;
            }
        }
    }
}
