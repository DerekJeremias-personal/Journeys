using System;
using System.Collections.Generic;
using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models.RulesEngine;
using Xunit;

namespace Journeys.Tests.Utility
{
    public class WrappedEventPayloadPersistTests
    {
        [Fact]
        public void OmitEmptyCollections_nulls_empty_lists_and_keeps_non_empty()
        {
            var payload = new WrappedEventPayload
            {
                AppliedCampaigns = new List<string>(),
                AppliedRuleSetIds = new List<string> { "rs-1" },
                OutcomeStates = new List<OutcomeStateBaseDto>(),
                ProviderStates = new Dictionary<string, ProviderStateBaseDto>(),
                JourneyStates = new Dictionary<string, JourneyStateDto>()
            };

            payload.OmitEmptyCollections();

            Assert.Null(payload.AppliedCampaigns);
            Assert.Equal(new[] { "rs-1" }, payload.AppliedRuleSetIds);
            Assert.Null(payload.OutcomeStates);
            Assert.NotNull(payload.ProviderStates);
            Assert.Empty(payload.ProviderStates);
            Assert.NotNull(payload.JourneyStates);
        }

        [Fact]
        public void Serialize_after_OmitEmptyCollections_does_not_emit_empty_list_properties()
        {
            var payload = new WrappedEventPayload
            {
                Id = "id-1",
                Status = WrappedEventPayloadStatus.ACTIVE,
                NaturalKey = "user|test_003",
                AccountId = "acct-1",
                TimeOfOccurrence = new DateTimeOffset(2026, 9, 15, 16, 30, 21, TimeSpan.Zero),
                Event = JsonSerializer.SerializeToElement(new { profileid = "test_003" })
            };
            payload.OmitEmptyCollections();

            var json = JsonSerializer.SerializeToElement(payload, JsonUtility.GetDefaultOptions());

            Assert.False(json.TryGetProperty("appliedcampaigns", out _));
            Assert.False(json.TryGetProperty("appliedCampaigns", out _));
            Assert.False(json.TryGetProperty("appliedrulesetids", out _));
            Assert.False(json.TryGetProperty("appliedRuleSetIds", out _));
            Assert.False(json.TryGetProperty("outcomestates", out _));
            Assert.False(json.TryGetProperty("outcomeStates", out _));
        }

        [Fact]
        public void Serialize_emits_canonical_lowercase_wrapper_symbols()
        {
            var payload = new WrappedEventPayload
            {
                Id = "id-1",
                Status = WrappedEventPayloadStatus.STABLE,
                NaturalKey = "xyz_021",
                AccountId = "acct-1",
                TimeOfOccurrence = new DateTimeOffset(2026, 9, 15, 16, 30, 21, TimeSpan.Zero),
                LastProcessed = new DateTimeOffset(2026, 9, 15, 16, 31, 0, TimeSpan.Zero),
                Event = JsonSerializer.SerializeToElement(new { orderId = "xyz_021", items = new[] { new { sku = "EXPENSIVE_ITEM" } } }),
                AppliedCampaigns = new List<string> { "camp-1" },
                AppliedRuleSetIds = new List<string> { "Bronze Point Economy" },
                OutcomeStates = new List<OutcomeStateBaseDto> { new() { IssuingOutcomeId = "o1" } },
                JourneyStates = new Dictionary<string, JourneyStateDto>
                {
                    ["bestrewardTiersystem"] = new JourneyStateDto(new List<string> { "node-1" })
                }
            };

            var json = JsonSerializer.SerializeToElement(payload, JsonUtility.GetDefaultOptions());

            Assert.True(json.TryGetProperty("naturalkey", out var naturalKey));
            Assert.Equal("xyz_021", naturalKey.GetString());
            Assert.True(json.TryGetProperty("timeofoccurrence", out _));
            Assert.True(json.TryGetProperty("lastprocessed", out _));
            Assert.True(json.TryGetProperty("accountid", out _));
            Assert.True(json.TryGetProperty("event", out _));
            Assert.True(json.TryGetProperty("appliedcampaigns", out var campaigns));
            Assert.Equal(JsonValueKind.Array, campaigns.ValueKind);
            Assert.Equal("camp-1", campaigns[0].GetString());
            Assert.True(json.TryGetProperty("appliedrulesetids", out var ruleSets));
            Assert.Equal("Bronze Point Economy", ruleSets[0].GetString());
            Assert.True(json.TryGetProperty("outcomestates", out var outcomes));
            Assert.Equal(JsonValueKind.Array, outcomes.ValueKind);
            Assert.True(json.TryGetProperty("journeystates", out _));
            Assert.True(json.TryGetProperty("providerstates", out _));

            Assert.False(json.TryGetProperty("naturalKey", out _));
            Assert.False(json.TryGetProperty("timeOfOccurrence", out _));
            Assert.False(json.TryGetProperty("appliedCampaigns", out _));
            Assert.False(json.TryGetProperty("appliedRuleSetIds", out _));
            Assert.False(json.TryGetProperty("outcomeStates", out _));
            Assert.False(json.TryGetProperty("journeyStates", out _));
            Assert.False(json.TryGetProperty("providerStates", out _));
        }

        [Fact]
        public void Deserialize_reads_legacy_camelCase_wrapper_json()
        {
            const string json = """
                {
                  "id": "id-1",
                  "status": "Active",
                  "naturalKey": "xyz_021",
                  "accountId": "acct-1",
                  "timeOfOccurrence": "2026-09-15T16:30:21+00:00",
                  "appliedCampaigns": ["camp-1"],
                  "appliedRuleSetIds": ["Bronze Point Economy"],
                  "event": { "orderId": "xyz_021" }
                }
                """;

            var payload = JsonSerializer.Deserialize<WrappedEventPayload>(json, JsonUtility.GetDefaultOptions());

            Assert.NotNull(payload);
            Assert.Equal("xyz_021", payload.NaturalKey);
            Assert.Equal("acct-1", payload.AccountId);
            Assert.Equal(new[] { "camp-1" }, payload.AppliedCampaigns);
            Assert.Equal(new[] { "Bronze Point Economy" }, payload.AppliedRuleSetIds);
        }

        [Fact]
        public void Deserialize_reads_canonical_lowercase_wrapper_json()
        {
            const string json = """
                {
                  "id": "id-1",
                  "status": "Stable",
                  "naturalkey": "xyz_021",
                  "accountid": "acct-1",
                  "timeofoccurrence": "2026-09-15T16:30:21+00:00",
                  "appliedcampaigns": ["camp-1"],
                  "outcomestates": [{ "issuingOutcomeId": "o1" }],
                  "event": { "orderId": "xyz_021" }
                }
                """;

            var payload = JsonSerializer.Deserialize<WrappedEventPayload>(json, JsonUtility.GetDefaultOptions());

            Assert.NotNull(payload);
            Assert.Equal("xyz_021", payload.NaturalKey);
            Assert.Equal("acct-1", payload.AccountId);
            Assert.Equal(new[] { "camp-1" }, payload.AppliedCampaigns);
            Assert.NotNull(payload.OutcomeStates);
            Assert.Single(payload.OutcomeStates);
            Assert.Equal("o1", payload.OutcomeStates[0].IssuingOutcomeId);
        }

        [Fact]
        public void Persist_options_lowercase_nested_engine_state_and_preserve_event_json()
        {
            var payload = new WrappedEventPayload
            {
                Id = "id-1",
                Status = WrappedEventPayloadStatus.STABLE,
                NaturalKey = "xyz_021",
                AccountId = "acct-1",
                Event = JsonSerializer.SerializeToElement(new { orderId = "xyz_021", items = new[] { new { sku = "EXPENSIVE_ITEM" } } }),
                AppliedCampaigns = new List<string> { "camp-1" },
                OutcomeStates = new List<OutcomeStateBaseDto> { new() { IssuingOutcomeId = "o1" } },
                JourneyStates = new Dictionary<string, JourneyStateDto>
                {
                    ["bestrewardTiersystem"] = new JourneyStateDto(new List<string> { "node-1" })
                },
                ProviderStates = new Dictionary<string, ProviderStateBaseDto>
                {
                    ["simple"] = new ProviderStateBaseDto("prov-1", "Simple")
                }
            };

            var json = JsonSerializer.SerializeToElement(payload, JsonUtility.GetWrapperPersistOptions());

            Assert.True(json.TryGetProperty("journeystates", out var journeys));
            Assert.True(journeys.TryGetProperty("bestrewardTiersystem", out var journey));
            Assert.True(journey.TryGetProperty("nodememberships", out var nodes));
            Assert.Equal(JsonValueKind.Array, nodes.ValueKind);
            Assert.Equal("node-1", nodes[0].GetString());
            Assert.False(journey.TryGetProperty("nodeMemberships", out _));

            Assert.True(json.TryGetProperty("outcomestates", out var outcomes));
            Assert.True(outcomes[0].TryGetProperty("issuingoutcomeid", out var oid));
            Assert.Equal("o1", oid.GetString());
            Assert.False(outcomes[0].TryGetProperty("issuingOutcomeId", out _));

            Assert.True(json.TryGetProperty("providerstates", out var providers));
            Assert.True(providers.TryGetProperty("simple", out var provider));
            Assert.True(provider.TryGetProperty("providerid", out var providerId));
            Assert.Equal("prov-1", providerId.GetString());
            Assert.False(provider.TryGetProperty("providerId", out _));

            Assert.True(json.TryGetProperty("event", out var evt));
            Assert.True(evt.TryGetProperty("orderId", out _));
            Assert.True(evt.TryGetProperty("items", out var items));
            Assert.Equal(JsonValueKind.Array, items.ValueKind);
        }
    }
}
