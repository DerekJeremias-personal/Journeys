using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.Models;
using Journeys.Tests.Stubs;

namespace Journeys.Tests.RulesEngine.Rules
{
    public class NumericRule_PropertyToProperty_Tests
    {
        private readonly PointAccountType _escrowPointAccountType;
        private readonly PointAccountType _expPointAccountType;
        private readonly PointAccountType _spendablePointAccountType;

        public NumericRule_PropertyToProperty_Tests()
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
        public async Task TrueEquals()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new StringPropertyRule("loyaltyAccount.extAccountId", StringEvalType.Equal, new PathValueProvider("loyaltyAccount.id"));
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseEquals()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.taxAmount", NumEvalType.Equal, "globals.pointsMultiplier");
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task TrueGreaterThan()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("globals.pointsMultiplier", NumEvalType.GreaterThan, "event.taxAmount");
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseGreaterThan()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.taxAmount", NumEvalType.GreaterThan, "globals.pointsMultiplier");
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task TrueLessThan()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.taxAmount", NumEvalType.LessThan, "globals.pointsMultiplier");
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseLessThan()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("globals.pointsMultiplier", NumEvalType.LessThan, "event.taxAmount");
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task TrueGreaterThanOrEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("globals.pointsMultiplier", NumEvalType.GreaterThanOrEqual, "event.taxAmount");
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseGreaterThanOrEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.taxAmount", NumEvalType.GreaterThanOrEqual, "globals.pointsMultiplier");
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task TrueLessThanOrEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.taxAmount", NumEvalType.LessThanOrEqual, "globals.pointsMultiplier");
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseLessThanOrEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("globals.pointsMultiplier", NumEvalType.LessThanOrEqual, "event.taxAmount");
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

    }
}