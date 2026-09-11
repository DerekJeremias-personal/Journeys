using Journeys.Core.Interfaces.Services;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Outcomes
{
    public class TagOutcome : OutcomeBase
    {
        private readonly ILogger<TagOutcome> _logger;

        public override string Kind => OutcomeKindDiscriminators.TagOutcome;

        public TagOutcome()
        {
            Id = "1abe1000-0000-0000-0000-000000000000";
        }

        public string Type { get; set; }
        public string EntityId { get; set; }
        public string Name { get; set; }

        public IValueProvider? ValueProvider { get; set; }
        public string Value { get; set; }

        public IValueProvider? EntityIdProvider { get; set; }

        public IValueProvider? EffectiveStartDateProvider { get; set; }
        public DateTimeOffset? EffectiveStartDate { get; set; }

        public IValueProvider? EffectiveEndDateProvider { get; set; }
        public DateTimeOffset? EffectiveEndDate { get; set; }

        public IValueProvider? EffectiveStartTimeProvider { get; set; }
        public TimeSpan? EffectiveStartTime { get; set; }

        public IValueProvider? EffectiveEndTimeProvider { get; set; }
        public TimeSpan? EffectiveEndTime { get; set; }

        public decimal? TtlSec { get; set; }


        public async override Task<OutcomeResult> AwardOutcomeAsync(RulesEngineState state, ILoyaltyAccountService loyaltyAccountService, CancellationToken token)
        {
            if (string.IsNullOrEmpty(EntityId) || string.IsNullOrEmpty(Value) ||
                (EffectiveStartDate ?? DateTimeOffset.MinValue) == DateTimeOffset.MinValue &&
                 (EffectiveEndDate ?? DateTimeOffset.MinValue) == DateTimeOffset.MinValue &&
                 (EffectiveStartTime ?? TimeSpan.MinValue) == TimeSpan.MinValue &&
                 (EffectiveEndTime ?? TimeSpan.MinValue) == TimeSpan.MinValue) return null;

            var res = await loyaltyAccountService.TagLoyaltyAccountAsync(state.TenantId, new DTO.Models.TagDto
            {
                Name = Name,
                Type = Type,
                EntityId = EntityId,
                Value = Value,
                EffectiveStartTime = EffectiveStartTime,
                EffectiveStartDate = EffectiveStartDate,
                EffectiveEndTime = EffectiveEndTime,
                EffectiveEndDate = EffectiveEndDate,
                TenantId = state.TenantId,
                TtlSec = TtlSec
            });

            return new OutcomeResult
            {
                IssuingOutcomeId = this.Id,
                IssuingOutcomeKind = this.Kind,
                IssuingOutcome = this,
                IssuingEventId = EventId,
                IssuingEventType = this.EventType,
            };
        }

        public async override Task<OutcomeResult> CalculateOutcomeAsync(RulesEngineState state, CancellationToken token)
        {
            if (EntityIdProvider != null)
                EntityId = await EntityIdProvider?.GetValue<string>(state, token);

            if (EffectiveStartDateProvider != null)
                EffectiveStartDate = await EffectiveStartDateProvider.GetValue<DateTimeOffset>(state, token);

            if (EffectiveEndDateProvider != null)
                EffectiveEndDate = await EffectiveEndDateProvider?.GetValue<DateTimeOffset>(state, token);

            if (EffectiveStartTimeProvider != null)
                EffectiveStartTime = await EffectiveStartTimeProvider.GetValue<TimeSpan>(state, token);

            if (EffectiveEndTimeProvider != null)
                EffectiveEndTime = await EffectiveEndTimeProvider?.GetValue<TimeSpan>(state, token);

            try
            {
                EventId = await ResolveEventIdAsync(state, token);
                if (string.IsNullOrWhiteSpace(EventId))
                {
                    throw new Exception("TagOutcome: No EventId provided");
                }
                EventType = await ResolveEventTypeAsync(state, token);
                if (string.IsNullOrWhiteSpace(EventType))
                {
                    throw new Exception("TagOutcome: No EventType provided");
                }

                return new OutcomeResult
                {
                    IssuingOutcomeId = this.Id,
                    IssuingOutcomeKind = this.Kind,
                    IssuingOutcome = this,
                    IssuingEventId = EventId,
                    IssuingEventType = this.EventType,
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in TagOutcome.CalculateOutcomeAsync");
                throw;
            }
        }
    }
}
