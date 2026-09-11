using System.Collections.Generic;
using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelRankerTests
{
    [Fact]
    public void Filters_to_eventable_and_excludes_wrappers()
    {
        var models = new[]
        {
            EventableModel("o1", "Order", monetary: true),
            WrapperModel("w1", "OrderAndRuleState"),
            NonEventableModel("c1", "Customer")
        };

        var set = EventModelRanker.Rank(models);

        Assert.Single(set.Candidates);
        Assert.Equal("o1", set.Candidates[0].EventModelId);
    }

    [Fact]
    public void Order_synonym_with_monetary_attribute_is_strong_match_and_recommended()
    {
        var models = new[]
        {
            EventableModel("o1", "Order", monetary: true),
            EventableModel("e1", "EmailOpened", monetary: false)
        };

        var set = EventModelRanker.Rank(models);

        Assert.Equal("o1", set.RecommendedDefaultId);
        var top = set.Candidates[0];
        Assert.Equal("o1", top.EventModelId);
        Assert.True(top.IsStrongMatch);
        Assert.True(top.HasMonetaryAttribute);
        Assert.Contains(top.MatchReasons, r => r.StartsWith("name:"));
        Assert.Contains(top.MatchReasons, r => r.StartsWith("amount:"));
    }

    [Fact]
    public void Exact_synonym_name_is_strong_even_without_monetary_attribute()
    {
        var models = new[] { EventableModel("t1", "Transaction", monetary: false) };

        var set = EventModelRanker.Rank(models);

        Assert.Equal("t1", set.RecommendedDefaultId);
        Assert.True(set.Candidates[0].IsStrongMatch);
    }

    [Fact]
    public void Substring_name_without_monetary_attribute_is_not_auto_default()
    {
        // "PreorderSurvey" contains "order" as a substring but is not an exact synonym and has no amount.
        var models = new[] { EventableModel("p1", "PreorderSurvey", monetary: false) };

        var set = EventModelRanker.Rank(models);

        Assert.Single(set.Candidates);
        Assert.Null(set.RecommendedDefaultId);
        Assert.False(set.Candidates[0].IsStrongMatch);
    }

    [Fact]
    public void Empty_input_yields_empty_set_with_no_default()
    {
        var set = EventModelRanker.Rank(new List<ModelDto>());

        Assert.Empty(set.Candidates);
        Assert.Null(set.RecommendedDefaultId);
        Assert.False(set.FetchFailed);
    }

    [Fact]
    public void Monetary_boost_ranks_order_above_unrelated_event()
    {
        var models = new[]
        {
            EventableModel("e1", "EmailOpened", monetary: false),
            EventableModel("o1", "Invoice", monetary: true)
        };

        var set = EventModelRanker.Rank(models);

        Assert.Equal("o1", set.Candidates[0].EventModelId);
    }

    private static ModelDto EventableModel(string id, string name, bool monetary) =>
        new()
        {
            ID = id,
            Name = name,
            ModelType = "event",
            Tag = "eventable",
            IsContainer = false,
            Attributes = monetary
                ? new List<ModelAttributeDto> { Amount("grandTotal") }
                : new List<ModelAttributeDto> { Primitive("note", "string") }
        };

    private static ModelDto WrapperModel(string id, string name) =>
        new() { ID = id, Name = name, ModelType = "event", Tag = "eventable", IsContainer = true };

    private static ModelDto NonEventableModel(string id, string name) =>
        new() { ID = id, Name = name, ModelType = "loyalty", Tag = "entity", IsContainer = false };

    private static ModelAttributeDto Amount(string symbol) => Primitive(symbol, "decimal");

    private static ModelAttributeDto Primitive(string symbol, string dataType) =>
        new ModelAttributePrimitiveDto { Symbol = symbol, DisplayName = symbol, DataType = dataType };
}
