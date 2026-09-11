using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Tests.Stubs;
using Newtonsoft.Json;

namespace Journeys.Tests.RulesEngine.Providers
{
    public class ConstantValueProviderTests
    {
        private readonly PointAccountType _escrowPointAccountType;
        private readonly PointAccountType _expPointAccountType;
        private readonly PointAccountType _spendablePointAccountType;

        public ConstantValueProviderTests()
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
            var expected = "String Value";
            var constProvider = new ConstantValueProvider(expected);
            var profile = TestDataFactory.GetProfileObject();
            var calculated = await constProvider.GetValue<string>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task GetIntegerValue()
        {
            var expected = (decimal)42;
            var constProvider = new ConstantValueProvider(expected);
            var profile = TestDataFactory.GetProfileObject();
            var calculated = await constProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }
    }
}