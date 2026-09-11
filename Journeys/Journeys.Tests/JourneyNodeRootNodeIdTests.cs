using Journeys.Core.Extensions;
using Journeys.Core.RulesEngine.Journey;
using Journeys.DTO.Models;

namespace Journeys.Tests;

public class JourneyNodeRootNodeIdTests
{
    [Fact]
    public void FillMissingRootNodeIds_SingleNode_SetsRootNodeIdToId()
    {
        var root = new JourneyNode("solo", id: "root-id", rootNodeId: null);
        root.FillMissingRootNodeIds();
        Assert.Equal("root-id", root.RootNodeId);
    }

    [Fact]
    public void FillMissingRootNodeIds_Nested_FillsNullChildrenWithCanonicalRoot()
    {
        var child = new JourneyNode("child", id: "child-id", rootNodeId: null);
        var root = new JourneyNode("root", id: "root-id", rootNodeId: null, children: new List<JourneyNode> { child });
        root.FillMissingRootNodeIds();
        Assert.Equal("root-id", root.RootNodeId);
        Assert.Equal("root-id", child.RootNodeId);
    }

    [Fact]
    public void FillMissingRootNodeIds_UsesExplicitRootRootNodeIdForDescendants()
    {
        var child = new JourneyNode("child", id: "child-id", rootNodeId: null);
        var root = new JourneyNode("root", id: "root-id", rootNodeId: "canonical-root", children: new List<JourneyNode> { child });
        root.FillMissingRootNodeIds();
        Assert.Equal("canonical-root", root.RootNodeId);
        Assert.Equal("canonical-root", child.RootNodeId);
    }

    [Fact]
    public void FillMissingRootNodeIds_DoesNotOverwriteNonNullChildRootNodeId()
    {
        var child = new JourneyNode("child", id: "child-id", rootNodeId: "child-keeps-this");
        var root = new JourneyNode("root", id: "root-id", rootNodeId: "canonical-root", children: new List<JourneyNode> { child });
        root.FillMissingRootNodeIds();
        Assert.Equal("child-keeps-this", child.RootNodeId);
    }

    [Fact]
    public void FromDto_FillsMissingRootNodeIds()
    {
        var dto = new JourneyDto
        {
            Id = "j-root",
            Name = "J",
            RootNodeId = null,
            Children = new List<JourneyDto>
            {
                new JourneyDto { Id = "j-child", Name = "C", RootNodeId = null }
            }
        };
        var node = dto.FromDto()!;
        Assert.Equal("j-root", node.RootNodeId);
        Assert.Single(node.Children!);
        Assert.Equal("j-root", node.Children![0].RootNodeId);
    }
}
