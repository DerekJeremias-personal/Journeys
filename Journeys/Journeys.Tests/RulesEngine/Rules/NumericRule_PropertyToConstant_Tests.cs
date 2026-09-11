using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.Models;
using Journeys.Tests.Stubs;

namespace Journeys.Tests.RulesEngine.Rules
{
    public class NumericRule_PropertyToConstant_Tests
    {
        private readonly PointAccountType _escrowPointAccountType;
        private readonly PointAccountType _expPointAccountType;
        private readonly PointAccountType _spendablePointAccountType;

        public NumericRule_PropertyToConstant_Tests()
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
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.Equal, 150.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseEquals()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.Equal, 100.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task TrueGreaterThan()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.GreaterThan, 100.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseGreaterThan()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.GreaterThan, 160.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task FalseGreaterThanWhenEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.GreaterThan, 150.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task TrueLessThan()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.LessThan, 200.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseLessThan()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.LessThan, 140.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task FalseLessThanWhenEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.LessThan, 150.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }


        [Fact]
        public async Task TrueGreaterThanOrEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.GreaterThanOrEqual, 150.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseGreaterThanOrEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.GreaterThanOrEqual, 151.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task TrueLessThanOrEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.LessThanOrEqual, 150.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task FalseLessThanOrEqual()
        {
            var profile = TestDataFactory.GetProfileObject();
            var rule = new NumericPropertyRule("event.subtotal", NumEvalType.LessThanOrEqual, 149.0M);
            var result = await rule.Evaluate(profile, CancellationToken.None);
            Assert.False(result);
        }

    }
}