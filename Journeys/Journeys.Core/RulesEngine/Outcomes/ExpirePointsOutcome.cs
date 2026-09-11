using Journeys.Core.Interfaces.Services;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.DTO.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Outcomes
{
    public class ExpirePointsOutcome : PointOutcomeBase
    {
        private readonly ILogger<ExpirePointsOutcome> _logger;

        public override string Kind => OutcomeKindDiscriminators.ExpirePointsOutcome;

        public ExpirePointsOutcome()
        {
            Id = "0FF00000-0000-0000-0000-000000000000";
        }

        public decimal? ExpirationAmount { get; set; }
        public decimal? ExpirationPercent { get; set; }

        public IValueProvider? ExpirationDateProvider { get; set; }
        [JsonIgnore]
        public DateTimeOffset? ExpirationDate { get; set; } = null;

        public IValueProvider? EarnDateProvider { get; set; }
        [JsonIgnore]
        public DateTimeOffset? EarnDate { get; set; } = null;

        public IValueProvider? ExpireAmountProvider { get; set; }


        public async override Task<OutcomeResult> AwardOutcomeAsync(RulesEngineState state, ILoyaltyAccountService loyaltyAccountService, CancellationToken token)
        {
            try
            {
                try
                {
                    if (string.IsNullOrEmpty(state?.TenantId))
                        throw new ArgumentNullException(nameof(state.TenantId));

                    await EnsurePointAccounType(state.TenantId);

                    if (!this.AffectedPointAccountTypes?.Any() ?? true)
                        throw new ArgumentNullException(nameof(this.AffectedPointAccountTypes));

                    if ((ExpirationAmount != null && ExpirationAmount <= 0) &&
                        (ExpirationPercent != null && (ExpirationPercent <= 0 || ExpirationPercent > 1.0m)))
                        throw new ArgumentNullException(nameof(state.TenantId));
                }
                catch (ArgumentNullException ane)
                {
                    _logger?.LogError(ane);
                    throw;
                }

                if (EarnDate != null && EarnDate > DateTime.MinValue)
                {
                    var expiredSet = await loyaltyAccountService.ExpireLoyaltyAccountPointsByEarnDate(state.TenantId, state.LoyaltyAccountId,
                                this.AffectedPointAccountTypeIds, (DateTimeOffset)EarnDate, ExpirationPercent ?? 1);
                    var bExpired = (expiredSet != null);
                    if (!bExpired)
                    {
                        return null;
                    }

                }
                else if (ExpirationDate != null && ExpirationDate > DateTime.MinValue)
                {
                    var expiredSet = await loyaltyAccountService.ExpireLoyaltyAccountPointsByExpirationDate(state.TenantId, state.LoyaltyAccountId,
                                this.AffectedPointAccountTypeIds, (DateTimeOffset)ExpirationDate, ExpirationPercent ?? 1);
                    var bExpired = (expiredSet != null);
                    if (!bExpired)
                    {
                        return null;
                    }
                }
                else if (ExpirationAmount > 0)
                {
                    var expiredSet = await loyaltyAccountService.ExpireLoyaltyAccountPointsByAmount(state.TenantId, state.LoyaltyAccountId,
                                this.AffectedPointAccountTypeIds, (decimal)ExpirationAmount, false);
                    var bExpired = (expiredSet != null);
                    if (!bExpired)
                    {
                        return null;
                    }
                }
                else if (!string.IsNullOrEmpty(EventId))
                {
                    bool isPercent = (ExpirationAmount ?? 0) == 0;

                    var expiredSet = await loyaltyAccountService.ExpireLoyaltyAccountPointsByEventId(state.TenantId, state.LoyaltyAccountId, this.AffectedPointAccountTypeIds,
                                        EventId, ExpirationPercent ?? ExpirationPercent ?? 1, isPercent);
                    var bExpired = (expiredSet != null);
                    if (!bExpired)
                    {
                        return null;
                    }
                }
                else
                {
                    throw new Exception($"AwardOutcomeAsync: Missing Expiration criteria. AccountId: {state.LoyaltyAccountId}");
                }

                return new OutcomeResult
                {
                    IssuingOutcomeId = $"{this.Id}-Awarded",
                    IssuingOutcomeKind = this.Kind,
                    IssuingOutcome = this,
                    IssuingEventId = EventId,
                    IssuingEventType = this.EventType,
                    //PointEntries = new List<LedgerEntryDto>
                    //{
                    //    new LedgerEntryDto
                    //    {
                    //        EntryId = EventId,
                    //        EventType = this.EventType,
                    //        BurnDate = DateTime.UtcNow,
                    //        ExpirationDate = DateTime.UtcNow,
                    //        PointsWithdrawn = ExpirationAmount ?? 0 //TODO: This needs to handle percentages too....need to return what was actually expired
                    //    }
                    //}
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ExpirePointsOutcome.AwardOutcomeAsync");
                throw;
            }
        }

        public async override Task<OutcomeResult> CalculateOutcomeAsync(RulesEngineState state, CancellationToken token)
        {
            try
            {
                if (string.IsNullOrEmpty(state?.TenantId))
                    throw new ArgumentNullException(nameof(state.TenantId));

                //TODO: Not liking Map From/To base class providers...maybe should just be specific to the outcome subclass

                if (ExpireAmountProvider != null)
                    ExpirationAmount = await ExpireAmountProvider.GetValue<decimal?>(state, token) ?? 0;

                if (EarnDateProvider != null)
                    EarnDate = await EarnDateProvider.GetValue<DateTimeOffset>(state, token);

                if (ExpirationDateProvider != null)
                    ExpirationDate = await ExpirationDateProvider?.GetValue<DateTimeOffset>(state, token);

                EventId = await ResolveEventIdAsync(state, token);
                if (string.IsNullOrWhiteSpace(EventId))
                {
                    throw new Exception("ExpirePointsOutcome: No EventId provided");
                }
                EventType = await ResolveEventTypeAsync(state, token);
                if (string.IsNullOrWhiteSpace(EventType))
                {
                    throw new Exception("ExpirePointsOutcome: No EventType provided");
                }

                return new OutcomeResult
                {
                    IssuingOutcomeId = $"{this.Id}-Calculated",
                    IssuingOutcomeKind = this.Kind,
                    IssuingOutcome = this,
                    IssuingEventId = EventId,
                    IssuingEventType = this.EventType,
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ExpirePointsOutcome.CalculateOutcomeAsync");
                throw;
            }

        }
    }
}
