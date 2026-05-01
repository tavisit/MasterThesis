using Assets.Scripts.Runtime.Graph;

using NUnit.Framework;

using UnityEngine;

public class RoadGraphConnectorTests
{
    private static RoadGraph BuildChain(Vector3 origin, int nodeCount, float spacing)
    {
        var graph = new RoadGraph();
        RoadNode prev = null;
        for (int i = 0; i < nodeCount; i++)
        {
            var node = graph.AddNode(origin + new Vector3(i * spacing, 0f, 0f), RoadType.Street);
            if (prev != null)
            {
                graph.AddEdge(prev, node, RoadType.Street);
            }

            prev = node;
        }

        return graph;
    }

    private static void AppendChain(RoadGraph graph, Vector3 origin, int nodeCount, float spacing)
    {
        RoadNode prev = null;
        for (int i = 0; i < nodeCount; i++)
        {
            var node = graph.AddNode(origin + new Vector3(i * spacing, 0f, 0f), RoadType.Street);
            if (prev != null)
            {
                graph.AddEdge(prev, node, RoadType.Street);
            }

            prev = node;
        }
    }

    [Test]
    public void FindComponents_TwoChains_YieldsTwoComponents()
    {
        var graph = BuildChain(Vector3.zero, 10, 10f);
        AppendChain(graph, new Vector3(200f, 0f, 0f), 10, 10f);

        var components = RoadGraphConnector.FindComponents(graph);

        Assert.AreEqual(2, components.Count);
        Assert.AreEqual(10, components[0].Count);
        Assert.AreEqual(10, components[1].Count);
    }

    [Test]
    public void ConnectComponents_TwoSignificantChains_MergesToOneComponent()
    {
        var graph = BuildChain(Vector3.zero, 10, 10f);
        AppendChain(graph, new Vector3(200f, 0f, 0f), 10, 10f);

        var stitches = RoadGraphConnector.ConnectComponents(
            graph,
            bridgeHeightThreshold: 8f,
            tunnelHeightThreshold: 5f,
            terrain: null,
            connectionsPerComponent: 2);

        Assert.GreaterOrEqual(stitches.Count, 1, "Expected at least one stitch edge between chains.");

        var components = RoadGraphConnector.FindComponents(graph);
        Assert.AreEqual(1, components.Count, "Graph should be fully connected.");
        Assert.AreEqual(20, components[0].Count);
    }

    [Test]
    public void ConnectComponents_PrunesSubThresholdComponent_KeepsOnlyLargeIsland()
    {
        var graph = BuildChain(Vector3.zero, 10, 10f);
        AppendChain(graph, new Vector3(500f, 0f, 0f), 5, 10f);

        var stitches = RoadGraphConnector.ConnectComponents(graph, terrain: null, connectionsPerComponent: 2);

        Assert.AreEqual(0, stitches.Count, "Small island is pruned; nothing significant to stitch.");
        Assert.AreEqual(10, graph.Nodes.Count);
        Assert.AreEqual(9, graph.Edges.Count);
    }

    [Test]
    public void ConnectComponents_SingleSignificantComponent_ReturnsNoStitches()
    {
        var graph = BuildChain(Vector3.zero, 12, 10f);

        var stitches = RoadGraphConnector.ConnectComponents(graph, terrain: null, connectionsPerComponent: 1);

        Assert.AreEqual(0, stitches.Count);
        Assert.AreEqual(1, RoadGraphConnector.FindComponents(graph).Count);
    }

    [Test]
    public void ConnectComponents_TwoChainsBeyondDefaultStitchDistance_UsesExtendedRange()
    {
        var graph = BuildChain(Vector3.zero, 10, 10f);
        AppendChain(graph, new Vector3(600f, 0f, 0f), 10, 10f);

        var stitches = RoadGraphConnector.ConnectComponents(
            graph,
            bridgeHeightThreshold: 8f,
            tunnelHeightThreshold: 5f,
            terrain: null,
            connectionsPerComponent: 2);

        Assert.GreaterOrEqual(
            stitches.Count,
            1,
            "Gap ~510m exceeds default 500m max stitch distance; extended range should still connect two large islands.");
        Assert.AreEqual(1, RoadGraphConnector.FindComponents(graph).Count);
        Assert.AreEqual(20, graph.Nodes.Count);
    }
}
