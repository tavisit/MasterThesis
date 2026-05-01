using System.Collections.Generic;

using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.Road.Generators;

using NUnit.Framework;

using UnityEngine;

public class NucleusPathFinderTests
{
    [Test]
    public void FindPath_LinearChain_ReturnsShortestEdgeSequence()
    {
        var graph = new RoadGraph();
        RoadNode a = graph.AddNode(new Vector3(0f, 0f, 0f));
        RoadNode b = graph.AddNode(new Vector3(10f, 0f, 0f));
        RoadNode c = graph.AddNode(new Vector3(20f, 0f, 0f));
        graph.AddEdge(a, b);
        graph.AddEdge(b, c);

        Dictionary<RoadNode, List<RoadEdge>> adj = NucleusPathFinder.BuildAdjacency(graph);
        List<RoadEdge> path = NucleusPathFinder.FindPath(
            graph,
            adj,
            a,
            c,
            new Vector2(1f, 0f),
            bearingPenaltyWeight: 0f);

        Assert.IsNotNull(path);
        Assert.AreEqual(2, path.Count);
        Assert.AreSame(a, path[0].From);
        Assert.AreSame(b, path[0].To);
        Assert.AreSame(b, path[1].From);
        Assert.AreSame(c, path[1].To);
    }

    [Test]
    public void FindPath_DisconnectedComponents_ReturnsNull()
    {
        var graph = new RoadGraph();
        RoadNode a = graph.AddNode(Vector3.zero);
        RoadNode b = graph.AddNode(new Vector3(10f, 0f, 0f));
        RoadNode c = graph.AddNode(new Vector3(0f, 0f, 100f));
        RoadNode d = graph.AddNode(new Vector3(10f, 0f, 100f));
        graph.AddEdge(a, b);
        graph.AddEdge(c, d);

        Dictionary<RoadNode, List<RoadEdge>> adj = NucleusPathFinder.BuildAdjacency(graph);
        List<RoadEdge> path = NucleusPathFinder.FindPath(
            graph,
            adj,
            a,
            c,
            new Vector2(0f, 1f),
            bearingPenaltyWeight: 0f);

        Assert.IsNull(path);
    }

    [Test]
    public void FindClosestNode_ReturnsNearestInXZ()
    {
        var graph = new RoadGraph();
        RoadNode far = graph.AddNode(new Vector3(100f, 0f, 0f));
        RoadNode near = graph.AddNode(new Vector3(5f, 0f, 1f));

        RoadNode best = NucleusPathFinder.FindClosestNode(graph, new Vector2(5.2f, 1.2f));

        Assert.AreSame(near, best);
        Assert.AreNotSame(far, best);
    }
}
