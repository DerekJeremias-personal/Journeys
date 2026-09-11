using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;

namespace Journeys.Tests.Services;

public class CampaignJourneyPointOutcomePatValidatorTests
{
    [Fact]
    public async Task ValidateAsync_unknown_pat_id_throws()
    {
        var cache = new MapPatCache(new Dictionary<string, PointAccountType>(StringComparer.OrdinalIgnoreCase));
        var validator = new CampaignJourneyPointOutcomePatValidator(cache);
        var campaign = CampaignWithDepositPat("unknown-pat");

        var ex = await Assert.ThrowsAsync<APIErrorsException>(() =>
            validator.ValidateAsync("tenant-a", campaign));

        Assert.Contains(ex.Errors.Values, v =>
            v.Contains("TIER_A_OUTCOME_PAT_NOT_FOUND", StringComparison.Ordinal)
            && v.Contains("unknown-pat", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_known_pat_id_passes()
    {
        var pat = new PointAccountType(
            null,
            "Active",
            "Spendable",
            null,
            PointLedgerTypeStrings.SPENDABLE,
            null,
            null,
            null,
            true,
            "AwayFromZero",
            0,
            "tenant-a",
            "pat-known");
        var cache = new MapPatCache(new Dictionary<string, PointAccountType>(StringComparer.OrdinalIgnoreCase)
        {
            ["pat-known"] = pat
        });
        var validator = new CampaignJourneyPointOutcomePatValidator(cache);
        var campaign = CampaignWithDepositPat("pat-known");

        await validator.ValidateAsync("tenant-a", campaign);
    }

    private static Campaign CampaignWithDepositPat(string patId)
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "earn-rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                    },
                    Outcomes = new List<OutcomeBase>
                    {
                        new DepositPointsOutcome
                        {
                            DollarAmountProvider = new ConstantValueProvider(10m),
                            PointsPerDollar = 1m,
                            AffectedPointAccountTypeIds = new List<string> { patId },
                            EventIdProvider = new ConstantValueProvider("evt-1")
                        }
                    }
                }
            },
            "j1",
            "j1",
            null,
            null);

        return new Campaign(
            "ext",
            CampaignStatusStrings.Draft,
            "n",
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "tenant-a",
            "c1");
    }

    private sealed class MapPatCache : IPointAccountTypeCache
    {
        private readonly Dictionary<string, PointAccountType> _map;

        public MapPatCache(Dictionary<string, PointAccountType> map) => _map = map;

        public bool CachePointAccountType(string tenantId, PointAccountType pointAccountType) => throw new NotImplementedException();

        public Task<bool> EnsurePATsLoaded(string tenantId) => throw new NotImplementedException();

        public Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId) => throw new NotImplementedException();

        public Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId) =>
            Task.FromResult(_map.TryGetValue(pointAccountTypeId, out var pat) ? pat : null!);

        public Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId) => throw new NotImplementedException();

        public Task InvalidateTenantPointAccountTypesAsync(string tenantId) => throw new NotImplementedException();
    }
}
