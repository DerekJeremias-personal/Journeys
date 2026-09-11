using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class VerificationRecordManifestBridgeTests
{
    private const string Manifest = """
        {"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"Spendable","isSpendable":true}]}
        """;

    [Fact]
    public void IsManifestSeedOnly_true_for_manifest_only_record()
    {
        var json = """{"pointAccountManifest":{"items":[{"id":"pat-1"}]}}""";
        Assert.True(VerificationRecordManifestBridge.IsManifestSeedOnly(json));
    }

    [Fact]
    public void IsManifestSeedOnly_false_after_process_event()
    {
        var json = """
            {"pointAccountManifest":{"items":[{"id":"pat-1"}]},"appliedRuleSetIds":["rs1"]}
            """;
        Assert.False(VerificationRecordManifestBridge.IsManifestSeedOnly(json));
    }

    [Fact]
    public void SeedOnCreationComplete_copies_manifest_into_empty_verification_record()
    {
        var s = StateWithManifest();
        VerificationRecordManifestBridge.SeedOnCreationComplete(s);

        Assert.Contains("pointAccountManifest", s.Artifacts.VerificationRecord!, StringComparison.Ordinal);
        Assert.Contains("\"items\"", s.Artifacts.VerificationRecord!, StringComparison.Ordinal);
        Assert.Contains("pat-1", s.Artifacts.VerificationRecord!, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeIntoProcessEventResult_preserves_manifest()
    {
        var s = StateWithManifest();
        var merged = VerificationRecordManifestBridge.MergeIntoProcessEventResult(
            s, """{"status":"processed","pointsAwarded":5}""");

        s.Artifacts.VerificationRecord = merged;

        Assert.Contains("pointAccountManifest", s.Artifacts.VerificationRecord!, StringComparison.Ordinal);
        Assert.Contains("pointsAwarded", s.Artifacts.VerificationRecord!, StringComparison.Ordinal);
        Assert.True(VerificationRecordManifestBridge.VerificationRecordHasManifestItems(s.Artifacts.VerificationRecord));
    }

    [Fact]
    public void SeedOnCreationComplete_skips_when_manifest_already_present()
    {
        var s = StateWithManifest();
        s.Artifacts.VerificationRecord = """
            {"pointAccountManifest":{"items":[{"id":"pat-1"}]},"status":"seeded"}
            """;

        VerificationRecordManifestBridge.SeedOnCreationComplete(s);

        Assert.Contains("seeded", s.Artifacts.VerificationRecord!, StringComparison.Ordinal);
    }

    private static CampaignWorkflowState StateWithManifest()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.PointAccountManifest = Manifest;
        return s;
    }
}
