using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class PointAccountManifestBuilderTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void AppendFromPatResult_two_pats_yields_two_items()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");

        var pat1 = JsonSerializer.Serialize(new PointAccountTypeDto
        {
            Id = "pat-1",
            Name = "Spendable A",
            Status = "Active",
            LedgerType = "Spendable",
            IsSpendable = true,
            RoundingDecimalPlaces = 0
        }, JsonOpts);

        var pat2 = JsonSerializer.Serialize(new PointAccountTypeDto
        {
            Id = "pat-2",
            Name = "Tier B",
            Status = "Active",
            LedgerType = "NonSpendable",
            IsSpendable = false,
            RoundingDecimalPlaces = 0
        }, JsonOpts);

        PointAccountManifestBuilder.AppendFromPatResult(state, pat1);
        PointAccountManifestBuilder.AppendFromPatResult(state, pat2);

        var manifest = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest);
        Assert.Equal(2, manifest.Items.Count);
        Assert.Contains(manifest.Items, i => i.Id == "pat-1");
        Assert.Contains(manifest.Items, i => i.Id == "pat-2");
    }

    [Fact]
    public void AppendFromPatResult_parses_double_encoded_string_result()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");

        var patJson = JsonSerializer.Serialize(new PointAccountTypeDto
        {
            Id = "pat-1",
            Name = "Spendable A",
            Status = "Active",
            LedgerType = "Spendable",
            IsSpendable = true,
            RoundingDecimalPlaces = 0
        }, JsonOpts);
        // Simulate an MCP transport that returns the PAT JSON wrapped as a JSON string.
        var doubleEncoded = JsonSerializer.Serialize(patJson);

        PointAccountManifestBuilder.AppendFromPatResult(state, doubleEncoded);

        var manifest = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest);
        Assert.Single(manifest.Items);
        Assert.Equal("pat-1", manifest.Items[0].Id);
    }

    [Fact]
    public void AppendFromListResult_registers_entities()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var listJson = """
            {
              "Count": 2,
              "Entities": [
                {"Id":"829eff38-6591-4c39-bfbd-db52c3256b53","Name":"user_spendable","LedgerType":"Spendable","IsSpendable":true,"Status":"Active"},
                {"Id":"f0ec0c6d-1282-497b-8bc5-e6be5fbe83ec","Name":"user_tier_qualification","LedgerType":"NonSpendable","IsSpendable":false,"Status":"Active"}
              ]
            }
            """;

        PointAccountManifestBuilder.AppendFromListResult(state, listJson);

        var manifest = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest);
        Assert.Equal(2, manifest.Items.Count);
        Assert.Contains(manifest.Items, i => i.Id == "829eff38-6591-4c39-bfbd-db52c3256b53");
        Assert.Contains(manifest.Items, i => i.Id == "f0ec0c6d-1282-497b-8bc5-e6be5fbe83ec");
    }

    [Fact]
    public void InferRole_is_spendable_true_returns_spendable()
    {
        var pat = new PointAccountTypeDto
        {
            Id = "pat-spend",
            Name = "dealer_spendable",
            Status = "Active",
            LedgerType = "Spendable",
            IsSpendable = true,
            RoundingDecimalPlaces = 0
        };

        Assert.Equal("spendable", PointAccountManifestBuilder.InferRole(pat));
    }

    [Fact]
    public void InferRole_nonspendable_ledger_returns_tier_qualification()
    {
        var pat = new PointAccountTypeDto
        {
            Id = "pat-tier",
            Name = "dealer_tier_qual",
            Status = "Active",
            LedgerType = "NonSpendable",
            IsSpendable = false,
            RoundingDecimalPlaces = 0
        };

        Assert.Equal("tierQualification", PointAccountManifestBuilder.InferRole(pat));
    }

    [Fact]
    public void InferRole_escrow_ledger_returns_escrow()
    {
        var pat = new PointAccountTypeDto
        {
            Id = "pat-escrow",
            Name = "return_hold",
            Status = "Active",
            LedgerType = "Escrow",
            IsSpendable = false,
            RoundingDecimalPlaces = 0
        };

        Assert.Equal("escrow", PointAccountManifestBuilder.InferRole(pat));
    }

    [Fact]
    public void InferRole_tier_like_name_wrong_ledger_emits_warning()
    {
        var pat = new PointAccountTypeDto
        {
            Id = "pat-tier",
            Name = "TierQualifyingPoints",
            Status = "Active",
            LedgerType = "Expired",
            IsSpendable = false,
            RoundingDecimalPlaces = 0
        };

        var warnings = new List<string>();
        var role = PointAccountManifestBuilder.InferRole(pat, warnings);

        Assert.Equal("tierQualification", role);
        Assert.Contains(warnings, w => w.Contains("NonSpendable", StringComparison.OrdinalIgnoreCase));
    }
}
