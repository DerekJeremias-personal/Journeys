using Backend.Dto.Dynamic;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Tests.Stubs;
using Newtonsoft.Json;

namespace Journeys.Tests.RulesEngine.Providers
{
    public class PathValueProviderTests
    {
        private readonly PointAccountType _escrowPointAccountType;
        private readonly PointAccountType _expPointAccountType;
        private readonly PointAccountType _spendablePointAccountType;

        public PathValueProviderTests()
        {
            var cache = new StubPointAccountTypeCache();

            // Initialize point account types
            _escrowPointAccountType = TestDataFactory.GetEscrowPointAccount();
            _spendablePointAccountType = TestDataFactory.GetSpendablePointAccount();
            _expPointAccountType = TestDataFactory.GetExpPointAccount();

            // Cache the point account types before using them
            cache.CachePointAccountType(_escrowPointAccountType.TenantId, _escrowPointAccountType);
            cache.CachePointAccountType(_spendablePointAccountType.TenantId, _spendablePointAccountType);
            cache.CachePointAccountType(_expPointAccountType.TenantId, _expPointAccountType);

            // Make sure expiration type is properly linked
            _spendablePointAccountType.ExpiresToPointAccountTypeId = _expPointAccountType.Id;
            _escrowPointAccountType.ExpiresToPointAccountTypeId = _expPointAccountType.Id;

        }

        [Fact]
        public async Task GetStringValue()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = "test@bishoplabs.com";
            var provider = new PathValueProvider("loyaltyAccount.externalId");
            var calculated = await provider.GetValue<string>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task GetDateValue()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = "Active"; // new DateOnly(1980, 1, 1);
            var provider = new PathValueProvider("loyaltyAccount.status");
            var calculated = await provider.GetValue<string>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        private static RulesEngineState CreateStateWithEventValue(string propertyName, object value)
        {
            var state = new RulesEngineState(new List<Campaign>(), true)
            {
                TenantId = TestDataFactory.TENANT_ID
            };

            state.ImportDynamicModels(
                globals: new Dictionary<string, object>(),
                account: TestDataFactory.GenerateRichard(),
                evt: new Dictionary<string, object> { [propertyName] = value });

            return state;
        }

        [Fact]
        public async Task GetValue_LongPathValue_ReturnsDecimalNullable()
        {
            var state = CreateStateWithEventValue("amount", 42L);
            var provider = new PathValueProvider("event.amount");

            var result = await provider.GetValue<decimal?>(state, CancellationToken.None);

            Assert.Equal(42m, result);
        }

        [Fact]
        public async Task GetValue_LongPathValue_ReturnsDecimal()
        {
            var state = CreateStateWithEventValue("amount", 42L);
            var provider = new PathValueProvider("event.amount");

            var result = await provider.GetValue<decimal>(state, CancellationToken.None);

            Assert.Equal(42m, result);
        }

        [Fact]
        public async Task GetValue_DoublePathValue_ReturnsDecimalNullable()
        {
            var state = CreateStateWithEventValue("amount", 9.99);
            var provider = new PathValueProvider("event.amount");

            var result = await provider.GetValue<decimal?>(state, CancellationToken.None);

            Assert.Equal(9.99m, result);
        }

        [Fact]
        public async Task GetValue_UndefinedPath_ReturnsNull()
        {
            var state = CreateStateWithEventValue("amount", 42L);
            var provider = new PathValueProvider("event.nonexistent");

            var result = await provider.GetValue<decimal?>(state, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetValue_OverloadsReturnSameResult()
        {
            var state = CreateStateWithEventValue("amount", 42L);
            var provider = new PathValueProvider("event.amount");

            var fromState = await provider.GetValue<decimal?>(state, CancellationToken.None);
            var fromEntity = provider.GetValue<decimal?>(state);

            Assert.Equal(fromState, fromEntity);
        }
    }
}