using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Models;
using Journeys.Tests.Stubs;

namespace Journeys.Tests.RulesEngine.Providers
{
    public class AggregateValueProviderTests
    {
        private readonly PointAccountType _escrowPointAccountType;
        private readonly PointAccountType _expPointAccountType;
        private readonly PointAccountType _spendablePointAccountType;

        public AggregateValueProviderTests()
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
        public async Task Sum()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 2.0M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new PathValueProvider("event.quantity");
            var aggProvider = new AggregateValueProvider(AggregateType.Sum, rowProvider, rowPathProvider);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task Min()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 50.0M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new PathValueProvider("event.unitPrice");
            var aggProvider = new AggregateValueProvider(AggregateType.Min, rowProvider, rowPathProvider);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task Max()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 100.0M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new PathValueProvider("event.unitPrice");
            var aggProvider = new AggregateValueProvider(AggregateType.Max, rowProvider, rowPathProvider);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task Average()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 75M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new PathValueProvider("event.unitPrice");
            var aggProvider = new AggregateValueProvider(AggregateType.Average, rowProvider, rowPathProvider);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task Count()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 2.0M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new ConstantValueProvider(1M);
            var aggProvider = new AggregateValueProvider(AggregateType.Count, rowProvider, rowPathProvider);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        public NumericPropertyRule GetUnitPriceConstraint(decimal comparisonAmount, NumEvalType comparison)
        {
            return new NumericPropertyRule("event.unitPrice", comparison, comparisonAmount);
        }

        [Fact]
        public async Task FilteredSum()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 50.75M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new PathValueProvider("event.lineTotal");
            var aggProvider = new AggregateValueProvider(AggregateType.Sum, rowProvider, rowPathProvider);
            aggProvider.Constraint = GetUnitPriceConstraint(50M, NumEvalType.Equal);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task FilteredMin()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 107.50M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new PathValueProvider("event.lineTotal");
            var aggProvider = new AggregateValueProvider(AggregateType.Min, rowProvider, rowPathProvider);
            aggProvider.Constraint = GetUnitPriceConstraint(100, NumEvalType.Equal);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task FilteredMax()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 50.75M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new PathValueProvider("event.lineTotal");
            var aggProvider = new AggregateValueProvider(AggregateType.Max, rowProvider, rowPathProvider);
            aggProvider.Constraint = GetUnitPriceConstraint(50, NumEvalType.Equal);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task FilteredAverage()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 50.75M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new PathValueProvider("event.lineTotal");
            var aggProvider = new AggregateValueProvider(AggregateType.Average, rowProvider, rowPathProvider);
            aggProvider.Constraint = GetUnitPriceConstraint(50, NumEvalType.Equal);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }

        [Fact]
        public async Task FilteredCount()
        {
            var profile = TestDataFactory.GetProfileObject();
            var expected = 1.0M;
            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new ConstantValueProvider(1M);
            var aggProvider = new AggregateValueProvider(AggregateType.Count, rowProvider, rowPathProvider);
            aggProvider.Constraint = GetUnitPriceConstraint(100, NumEvalType.Equal);
            var calculated = await aggProvider.GetValue<decimal>(profile, CancellationToken.None);
            Assert.Equal(expected, calculated);
        }
    }
}