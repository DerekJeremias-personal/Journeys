using Journeys.Core.Interfaces.Services;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Rules;
using Journeys.DTO.Models;

namespace Journeys.Core.RulesEngine.Outcomes;

public class NotificationOutcome : OutcomeBase
{
    public override string Kind => OutcomeKindDiscriminators.NotificationOutcome;

    public string? NotificationConfigId { get; set; }

    public NotificationOutcome()
    {
        Id = "BEEEEEE9-0000-0000-0000-000000000000";
    }

    public override async Task<OutcomeResult> AwardOutcomeAsync(RulesEngineState state, ILoyaltyAccountService loyaltyAccountService, CancellationToken token)
    {
        if (state.CalculateOnly)
            return BuildResult(state, isAwarded: false);

        if (state.NotificationService == null)
            throw new InvalidOperationException("NotificationOutcome.AwardOutcomeAsync requires RulesEngineState.NotificationService.");

        var payload = new NotificationOutcomePayload
        {
            TenantId = state.TenantId,
            LoyaltyAccountId = state.LoyaltyAccountId,
            ExtAccountId = string.IsNullOrWhiteSpace(state.LoyaltyAccount?.ExtAccountId) ? null : state.LoyaltyAccount.ExtAccountId,
            CampaignId = ResolveCampaignId(state),
            RuleSetId = ResolveRuleSetId(state),
            IssuingOutcomeId = Id,
            IssuingOutcomeKind = Kind,
            EventId = EventId ?? state.EventId,
            EventType = EventType ?? state.EventType,
            EventModelId = state.EventModelId,
            AwardedAtUtc = DateTimeOffset.UtcNow
        };

        bool sent;
        try
        {
            sent = await state.NotificationService.SendNotificationAsync(state.TenantId, NotificationConfigId!, payload);
        }
        catch
        {
            if (!state.TreatNotificationSendThrowAsFalse)
                throw;
            sent = false;
        }

        return BuildResult(state, sent, payload.EventId, payload.EventType, payload.CampaignId, payload.RuleSetId);
    }

    public override async Task<OutcomeResult> CalculateOutcomeAsync(RulesEngineState state, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(NotificationConfigId))
            return null;

        if (state.NotificationService == null)
            return null;

        var config = await state.NotificationService.GetNotificationConfigAsync(state.TenantId, NotificationConfigId);
        if (config == null)
            return null;

        if (!string.Equals(config.Status, ConfigStatus.ACTIVE, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.IsNullOrWhiteSpace(config.EventModelId)
            && !string.Equals(config.EventModelId, state.EventModelId, StringComparison.OrdinalIgnoreCase))
            return null;

        EventId = await ResolveEventIdAsync(state, token) ?? state.EventId;
        EventType = await ResolveEventTypeAsync(state, token) ?? state.EventType;

        return BuildResult(state, isAwarded: false);
    }

    private OutcomeResult BuildResult(
        RulesEngineState state,
        bool isAwarded,
        string? eventId = null,
        string? eventType = null,
        string? campaignId = null,
        string? ruleSetId = null)
    {
        return new OutcomeResult
        {
            IssuingOutcomeId = Id,
            IssuingOutcomeKind = Kind,
            IssuingOutcome = this,
            IssuingEventId = eventId ?? EventId ?? state.EventId,
            IssuingEventType = eventType ?? EventType ?? state.EventType,
            CampaignId = campaignId ?? ResolveCampaignId(state),
            RuleSetId = ruleSetId ?? ResolveRuleSetId(state),
            IsAwarded = isAwarded
        };
    }

    private string ResolveCampaignId(RulesEngineState state)
    {
        return FindCalculated(state)?.CampaignId ?? state.CurrentCampaignId ?? string.Empty;
    }

    private string ResolveRuleSetId(RulesEngineState state)
    {
        return FindCalculated(state)?.RuleSetId ?? state.AppliedRuleSets.LastOrDefault() ?? string.Empty;
    }

    private OutcomeResult? FindCalculated(RulesEngineState state)
    {
        if (state.EarnedOutcomes == null || state.EarnedOutcomes.IsEmpty)
            return null;

        return state.EarnedOutcomes.Values
            .SelectMany(x => x)
            .FirstOrDefault(x => ReferenceEquals(x.IssuingOutcome, this) || x.IssuingOutcomeId == Id);
    }
}
