using Backend.Dto.Structures.Model;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelEligibilityValidationTests
{
    [Theory]
    [InlineData("eventable", true)]
    [InlineData("other", false)]
    [InlineData(null, false)]
    public void IsProcessEventEligible_respects_tag(string? tag, bool expected)
    {
        var model = new ModelDto { Tag = tag };
        Assert.Equal(expected, EventModelEligibilityValidation.IsProcessEventEligible(model));
    }

    [Fact]
    public void IsLoyaltyAccountCreationEvent_true_when_metadata_set()
    {
        var model = new ModelDto
        {
            Tag = "eventable",
            ModelMetaData = new Dictionary<string, string> { ["IsLoyaltyAccount"] = "true" }
        };

        Assert.True(EventModelEligibilityValidation.IsLoyaltyAccountCreationEvent(model));
        Assert.Equal(
            EventModelEligibilityValidation.RoleLoyaltyAccountCreation,
            EventModelEligibilityValidation.ResolveProcessingRole(model));
    }

    [Fact]
    public void GetEligibilityWarnings_missing_eventable_tag()
    {
        var model = new ModelDto
        {
            Tag = null,
            ModelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "w1",
                ["ProcessingType"] = "Engine"
            }
        };

        var warnings = EventModelEligibilityValidation.GetEligibilityWarnings(model);
        Assert.Contains(warnings, w => w.Contains("eventable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(warnings, w => w.Contains("SaveModel", StringComparison.OrdinalIgnoreCase));
    }
}
