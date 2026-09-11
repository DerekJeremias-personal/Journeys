using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class WorkflowUserPhraseCatalogTests
{
    [Fact]
    public void Does_not_include_technical_advance_phrases()
    {
        var all = WorkflowUserPhraseCatalog.ApprovalPhrases
            .Concat(WorkflowUserPhraseCatalog.TagFirstPhrases);
        Assert.DoesNotContain(all, p => p.Contains("create the campaign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ContainsAny_matches_approve()
    {
        Assert.True(WorkflowUserPhraseCatalog.ContainsAny("yes approve", WorkflowUserPhraseCatalog.ApprovalPhrases));
    }

    [Theory]
    [InlineData("Please test with a sample payload")]
    [InlineData("process event for test user")]
    [InlineData("run a test on the campaign")]
    public void ContainsAny_matches_journey_verification_intent(string phrase)
    {
        Assert.True(WorkflowUserPhraseCatalog.ContainsAny(phrase, WorkflowUserPhraseCatalog.JourneyVerificationIntentPhrases));
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("yep")]
    [InlineData("yeah")]
    [InlineData("sure")]
    [InlineData("ok")]
    [InlineData("okay")]
    [InlineData("confirm")]
    public void ContainsAny_matches_common_affirmatives(string phrase)
    {
        Assert.True(WorkflowUserPhraseCatalog.ContainsAny(phrase, WorkflowUserPhraseCatalog.ApprovalPhrases));
    }
}
