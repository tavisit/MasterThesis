using System.Collections.Generic;
using System.Linq;

using Assets.Scripts.Runtime.Graph;

using NUnit.Framework;

using UnityEngine;

public class RoadGraphTests
{
    [Test]
    public void RoadGraph_StartsEmpty()
    {
        var graph = new RoadGraph();

        Assert.AreEqual(0, graph.Nodes.Count);
        Assert.AreEqual(0, graph.Edges.Count);
    }

    [Test]
    public void RoadGraph_AddNode_IncreasesNodeCount()
    {
        var graph = new RoadGraph();
        graph.AddNode(Vector3.zero, RoadType.Street);

        Assert.AreEqual(1, graph.Nodes.Count);
    }

    [Test]
    public void RoadGraph_AddMultipleNodes_AssignsUniqueIds()
    {
        var graph = new RoadGraph();
        var a = graph.AddNode(Vector3.zero, RoadType.Street);
        var b = graph.AddNode(Vector3.one, RoadType.Street);
        var c = graph.AddNode(Vector3.up, RoadType.Metro);

        Assert.AreNotEqual(a.Id, b.Id);
        Assert.AreNotEqual(b.Id, c.Id);
        Assert.AreNotEqual(a.Id, c.Id);
    }

    [Test]
    public void RoadGraph_AddEdge_IncreasesEdgeCount()
    {
        var graph = new RoadGraph();
        var a = graph.AddNode(Vector3.zero, RoadType.Street);
        var b = graph.AddNode(Vector3.one, RoadType.Street);
        graph.AddEdge(a, b, RoadType.Street);

        Assert.AreEqual(1, graph.Edges.Count);
    }

    [Test]
    public void RoadGraph_AddEdge_StoresCorrectFromAndTo()
    {
        var graph = new RoadGraph();
        var a = graph.AddNode(Vector3.zero, RoadType.Street);
        var b = graph.AddNode(Vector3.one, RoadType.Street);
        var edge = graph.AddEdge(a, b, RoadType.Street);

        Assert.AreSame(a, edge.From);
        Assert.AreSame(b, edge.To);
    }

    [Test]
    public void RoadGraph_RetainEdges_RemovesFilteredEdges()
    {
        var graph = new RoadGraph();
        var a = graph.AddNode(Vector3.zero, RoadType.Street);
        var b = graph.AddNode(Vector3.one, RoadType.Street);
        var c = graph.AddNode(Vector3.up * 2, RoadType.Metro);
        graph.AddEdge(a, b, RoadType.Street);
        graph.AddEdge(b, c, RoadType.Metro);

        graph.RetainEdges(e => e.Type == RoadType.Street);

        Assert.AreEqual(1, graph.Edges.Count);
        Assert.AreEqual(RoadType.Street, graph.Edges[0].Type);
    }

    [Test]
    public void RoadGraph_RetainEdges_RemovesOrphanedNodes()
    {
        var graph = new RoadGraph();
        var a = graph.AddNode(Vector3.zero, RoadType.Street);
        var b = graph.AddNode(Vector3.one, RoadType.Street);
        var c = graph.AddNode(Vector3.up * 2, RoadType.Metro);
        graph.AddEdge(a, b, RoadType.Street);
        graph.AddEdge(b, c, RoadType.Metro);

        graph.RetainEdges(e => e.Type == RoadType.Street);

        Assert.IsFalse(graph.Nodes.Contains(c), "Orphaned metro node should be removed.");
    }

    [Test]
    public void RoadGraph_SubgraphUpToEdge_LimitsEdgeCount()
    {
        var graph = new RoadGraph();
        var a = graph.AddNode(Vector3.zero, RoadType.Street);
        var b = graph.AddNode(Vector3.one, RoadType.Street);
        var c = graph.AddNode(Vector3.up * 2, RoadType.Street);
        graph.AddEdge(a, b, RoadType.Street);
        graph.AddEdge(b, c, RoadType.Street);

        var sub = graph.SubgraphUpToEdge(1);

        Assert.AreEqual(1, sub.Edges.Count);
    }

    [Test]
    public void RoadGraph_ExtractChains_SingleChain_ReturnsOneChain()
    {
        var graph = new RoadGraph();
        var a = graph.AddNode(new Vector3(0, 0, 0), RoadType.Street);
        var b = graph.AddNode(new Vector3(1, 0, 0), RoadType.Street);
        var c = graph.AddNode(new Vector3(2, 0, 0), RoadType.Street);
        graph.AddEdge(a, b, RoadType.Street);
        graph.AddEdge(b, c, RoadType.Street);

        var chains = graph.ExtractChains();

        Assert.AreEqual(1, chains.Count);
    }

    [Test]
    public void RoadGraph_ExtractChains_TwoDisconnectedEdges_ReturnsTwoChains()
    {
        var graph = new RoadGraph();
        var a = graph.AddNode(new Vector3(0, 0, 0), RoadType.Street);
        var b = graph.AddNode(new Vector3(1, 0, 0), RoadType.Street);
        var c = graph.AddNode(new Vector3(10, 0, 0), RoadType.Street);
        var d = graph.AddNode(new Vector3(11, 0, 0), RoadType.Street);
        graph.AddEdge(a, b, RoadType.Street);
        graph.AddEdge(c, d, RoadType.Street);

        var chains = graph.ExtractChains();

        Assert.AreEqual(2, chains.Count);
    }

    [Test]
    public void RoadGraph_RetainNodes_RemovesUnreferencedNodes()
    {
        var graph = new RoadGraph();
        var a = graph.AddNode(Vector3.zero, RoadType.Street);
        var b = graph.AddNode(Vector3.one, RoadType.Street);
        graph.AddEdge(a, b, RoadType.Street);

        graph.RetainNodes(new HashSet<RoadNode> { a });

        Assert.AreEqual(1, graph.Nodes.Count);
        Assert.AreSame(a, graph.Nodes[0]);
    }
}
