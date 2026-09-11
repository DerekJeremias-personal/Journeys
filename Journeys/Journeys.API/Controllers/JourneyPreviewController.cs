using Journeys.API.Models;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Journeys.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class JourneyPreviewController : ControllerBase
    {
        private readonly ILogger<JourneyPreviewController> _logger;
        private readonly ILoyaltyAccountService _accountService;
        private readonly ILoyaltyAccountAdapter _loyaltyAccountAdapter;
        private readonly IDynamicDataAdapter _dynamicDataAdapter;
        private readonly ICampaignService _campaignService;

        public JourneyPreviewController(
            ILogger<JourneyPreviewController> logger,
            ILoyaltyAccountService accountService,
            ILoyaltyAccountAdapter loyaltyAccountAdapter,
            IDynamicDataAdapter dynamicDataAdapter,
            ICampaignService campaignService)
        {
            _logger = logger;
            _accountService = accountService;
            _loyaltyAccountAdapter = loyaltyAccountAdapter;
            _dynamicDataAdapter = dynamicDataAdapter;
            _campaignService = campaignService;
        }

        [HttpPost("{tenantId}/preview")]
        public async Task<IActionResult> JourneyPreview(
            string tenantId,
            [FromBody] JourneyPreviewRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tenantId))
                return BadRequest("TenantId is required");

            if (string.IsNullOrEmpty(request.LoyaltyAccountId))
                return BadRequest("LoyaltyAccountId is required");

            try
            {
                // Get Account (to verify it exists and get tenantId)
                var account = await _accountService.GetLoyaltyAccountAsync(tenantId, request.LoyaltyAccountId, false);
                if (account == null)
                    return NotFound(new { error = $"Loyalty account '{request.LoyaltyAccountId}' not found for tenant '{tenantId}'." });

                // Get All Related Event Container Models
                var wrapperModels = await _loyaltyAccountAdapter.GetRelatedLoyaltyAccountModelsAsync(tenantId);
                if (wrapperModels == null || wrapperModels.Count == 0)
                {
                    _logger.LogWarning("No event container models found for tenant {TenantId}", tenantId);
                    return Ok(new
                    {
                        SpendTotal = 0m,
                        PointsAwarded = new Dictionary<string, decimal>(),
                        PointBalances = new List<AccountPointBalance>()
                    });
                }

                // Query Events Across All Container Models Using Account ID
                var allWrappedEvents = new List<WrappedEventPayload>();

                foreach (var wrapperModel in wrapperModels)
                {
                    // Query wrapper model directly using account ID as partition key
                    // Handle pagination to get all events for this account in this container
                    string? continuationToken = null;
                    do
                    {
                        var pageResult = await _dynamicDataAdapter.GetEntitiesByPKAsync<WrappedEventPayload>(
                            tenantId,
                            request.LoyaltyAccountId,
                            wrapperModel.ID,
                            pageSize: 1000,
                            continuationToken: continuationToken);

                        if (pageResult?.Entities != null)
                        {
                            allWrappedEvents.AddRange(pageResult.Entities);
                        }

                        continuationToken = pageResult?.ContinuationToken;
                    } while (!string.IsNullOrEmpty(continuationToken));
                }

                // Convert WrappedEventPayload to EventPayloadResponseDto and filter by date
                var filteredEntities = allWrappedEvents
                    .Where(HasEventPayload)
                    .Where(w => w.TimeOfOccurrence >= request.StartDateUTC && w.TimeOfOccurrence <= request.EndDateUTC)
                    .Select(w => new EventPayloadResponseDto
                    {
                        TenantId = tenantId,
                        LoyaltyAccountId = w.AccountId,
                        TimeOfOccurrence = w.TimeOfOccurrence,
                        LastProcessed = w.LastProcessed,
                        Event = w.Event,
                        AppliedCampaigns = w.AppliedCampaigns,
                        AppliedRuleSetIds = w.AppliedRuleSetIds,
                        ProviderStates = w.ProviderStates ?? new Dictionary<string, ProviderStateBaseDto>(),
                        OutcomeStates = w.OutcomeStates ?? new List<OutcomeStateBaseDto>(),
                        JourneyStates = w.JourneyStates ?? new Dictionary<string, JourneyStateDto>()
                    })
                    .ToList();
                var appliedCampaignIds = filteredEntities
                    .SelectMany(e => e.AppliedCampaigns ?? Enumerable.Empty<string>())
                    .Distinct()
                    .ToList();
                string campaignId = appliedCampaignIds.FirstOrDefault() ?? "";

                var spendField = await GetSpendFieldForCampaignAsync(tenantId, campaignId);
                if (!string.IsNullOrEmpty(spendField))
                {
                    // Convert "event.value" → "value"
                    spendField = spendField.StartsWith("event.", StringComparison.OrdinalIgnoreCase)
                        ? spendField.Substring("event.".Length)
                        : spendField;
                }
                // Spend Total
                var spendTotal = filteredEntities.Where(e => e.Event.ValueKind == JsonValueKind.Object)
                    .Sum(e => ExtractSpendFromEvent(e.Event, spendField));
                // Points Awarded
                var pointsAwarded = filteredEntities
                 .SelectMany(e => e.OutcomeStates ?? Enumerable.Empty<OutcomeStateBaseDto>())
                 .Where(o =>
                     o.PointAccountTypeId != null &&
                     o.IssuingOutcomeId != null &&
                     o.IssuingOutcomeId.Contains("-Awarded|"))
                 .GroupBy(o => o.PointAccountTypeId!)
                 .ToDictionary(
                     g => g.Key,
                     g => g.Sum(o => o.PointsDeposited - o.PointsWithdrawn)
                 );

                // Point Balances
                var ledgers = await _accountService.GetLoyaltyAccountPointsAsync(
                    tenantId,
                    request.LoyaltyAccountId,
                    true);

                if (ledgers == null)
                    return NotFound(new { error = "Point ledgers not found for account." });

                var balances = ledgers.Select(l => new AccountPointBalance
                {
                    AccountId = l.AccountId,
                    PointAccountTypeId = l.PointAccountTypeId,
                    CurrentBalance = l.CurrentBalance,
                    LifetimeTotal = l.LifetimeTotal
                }).ToList();

                // Final Response
                return Ok(new
                {
                    SpendTotal = spendTotal,
                    PointsAwarded = pointsAwarded,
                    PointBalances = balances
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving journey preview for tenant {TenantId}, account {LoyaltyAccountId}",
                    tenantId, request.LoyaltyAccountId);
                return StatusCode(500, new { error = "An error occurred while retrieving journey preview.", tenantId });
            }
        }

        #region Helper Methods

        private static bool HasEventPayload(WrappedEventPayload wrapped) =>
            wrapped.Event.ValueKind != JsonValueKind.Undefined &&
            wrapped.Event.ValueKind != JsonValueKind.Null;

      
        // Extracts total spend from an event using a dynamic spend field.
        private static decimal ExtractSpendFromEvent(JsonElement evt, string spendField = "value")
        {
            decimal total = 0m;

            // Item-level spend
            if (evt.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                    total += ExtractDecimalFromObject(item, spendField);
                return total;
            }

            // Event-level spend
            return ExtractDecimalFromObject(evt, spendField);
        }

        private static decimal ExtractDecimalFromObject(JsonElement obj, string spendField)
        {
            if (obj.TryGetProperty(spendField, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number)
                    return val.GetDecimal();

                if (val.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(val.GetString(), out var parsed))
                    return parsed;
            }

            return 0m;
        }


        // Get the spend field for a campaign dynamically
        private async Task<string> GetSpendFieldForCampaignAsync(string tenantId, string campaignId)
        {
            var campaign = await _campaignService.FetchCampaignAsync(tenantId, campaignId, status: CampaignStatusStrings.Live);
            if (campaign == null || campaign.Journey == null)
                return "value"; // fallback

            string journeyJson = JsonSerializer.Serialize(campaign.Journey);
            using var journeyDoc = JsonDocument.Parse(journeyJson);
            var root = journeyDoc.RootElement;

            string? FindSpendFieldInJourney(JsonElement node)
            {
                // Rules
                if (node.TryGetProperty("Rules", out var rules) &&
                    rules.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rule in rules.EnumerateArray())
                    {
                        if (!rule.TryGetProperty("OutcomesJsonElement", out var outcomes) ||
                            outcomes.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var outcome in outcomes.EnumerateArray())
                        {
                            if (outcome.TryGetProperty("DollarAmountProvider", out var dollarProvider) &&
                                dollarProvider.TryGetProperty("RowPropertyProvider", out var rowProp) &&
                                rowProp.TryGetProperty("PropertyPath", out var propPath))
                            {
                                var spendField = propPath.GetString();
                                if (!string.IsNullOrEmpty(spendField))
                                    return spendField;
                            }
                        }
                    }
                }

                // Recurse children
                if (node.TryGetProperty("Children", out var children) &&
                    children.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in children.EnumerateArray())
                    {
                        var found = FindSpendFieldInJourney(child);
                        if (!string.IsNullOrEmpty(found))
                            return found;
                    }
                }

                return null;
            }

            return FindSpendFieldInJourney(root) ?? "value";
        }
        #endregion
    }

}
