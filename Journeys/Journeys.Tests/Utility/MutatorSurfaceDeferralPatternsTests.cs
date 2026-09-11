using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class MutatorSurfaceDeferralPatternsTests
{
    [Theory]
    [InlineData("UpsertPointAccountType is not surfaced in this session.", true)]
    [InlineData("Create PATs via POST /api/pointaccounttype/upsert", true)]
    [InlineData("validate_campaign reported hard errors", false)]
    public void LooksLikeDeferral(string text, bool expected) =>
        Assert.Equal(expected, MutatorSurfaceDeferralPatterns.LooksLikeDeferral(text));
}
