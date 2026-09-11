using Journeys.Core.RulesEngine.Rules;
using Xunit;

namespace Journeys.Tests.RulesEngine.Rules
{
    public class RuleBaseTests
    {
        [Fact]
        public void GetHistoricalStateKey_returns_campaignId_pipe_ruleId()
        {
            var key = RuleBase.GetHistoricalStateKey("campaign-1", "rule-1");
            Assert.Equal("campaign-1|rule-1", key);
        }

        [Fact]
        public void GetHistoricalStateKey_throws_when_campaignId_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                RuleBase.GetHistoricalStateKey(null!, "rule-1"));
        }

        [Fact]
        public void GetHistoricalStateKey_throws_when_ruleId_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                RuleBase.GetHistoricalStateKey("campaign-1", null!));
        }

        [Fact]
        public void GetHistoricalStateKey_throws_when_campaignId_empty()
        {
            Assert.Throws<ArgumentNullException>(() =>
                RuleBase.GetHistoricalStateKey("", "rule-1"));
        }

        [Fact]
        public void GetHistoricalStateKey_throws_when_ruleId_empty()
        {
            Assert.Throws<ArgumentNullException>(() =>
                RuleBase.GetHistoricalStateKey("campaign-1", ""));
        }
    }
}
