using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Tests.Stubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Tests.RulesEngine.Providers
{
    public class PointBalanceProviderTests
    {
        private readonly PointAccountType _escrowPointAccountType;
        private readonly PointAccountType _expPointAccountType;
        private readonly PointAccountType _spendablePointAccountType;

        public PointBalanceProviderTests()
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
        public async Task GetBalanceValue()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 100;
            var provider = new PointBalanceProvider(
                profile.LoyaltyAccount.PointLedgers.FirstOrDefault(x => x.Id == "escrowledger").PointAccountType.Id);

            var calculated = await provider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

    }
}
