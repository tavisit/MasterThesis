using System.Collections.Generic;
using System.Linq;

using Assets.Scripts.Runtime.Adapters;

using UnityEngine;

namespace Assets.Scripts.Runtime.Graph
{
    public enum ConnectionType { Road, Bridge, Tunnel }

    public sealed class RoadConnection
    {
        public RoadNode From { get; }
        public RoadNode To { get; }
        public ConnectionType ConnectionType { get; }

        public RoadConnection(RoadNode from, RoadNode to, ConnectionType type)
        {
            From = from; To = to; ConnectionType = type;
        }
    }

    public static class RoadGraphConnector
    {
        private const int MinComponentSize = 10;
        private const float MaxStitchDistance = 500f;
        private const int TerrainSamples = 10;
        private const float MinStitchAngleDegrees = 25f;

        public static List<RoadConnection> ConnectComponents(
            RoadGraph graph,
            float bridgeHeightThreshold = 8f,
            float tunnelHeightThreshold = 5f,
            TerrainAdapter terrain = null,
            int connectionsPerComponent = 1)
        {
            var connections = new List<RoadConnection>();
            PruneSmallComponents(graph);

            var adj = new Dictionary<RoadNode, List<RoadNode>>();
            foreach (var node in graph.Nodes) adj[node] = new List<RoadNode>();
            foreach (var edge in graph.Edges)
            {
                adj[edge.From].Add(edge.To);
                adj[edge.To].Add(edge.From);
            }

            var connCount = new Dictionary<RoadNode, int>();
            foreach (var node in graph.Nodes) 
                connCount[node] = 0;

            var blocked = new HashSet<(RoadNode, RoadNode)>();

            while (true)
            {
                var components = FindComponents(graph);
                var significant = components
                    .Where(c => c.Count >= MinComponentSize)
                    .ToList();

                if (significant.Count <= 1) break;

                float bestScore = float.MinValue;
                RoadNode bestA = null;
                RoadNode bestB = null;
                int bestI = -1, bestJ = -1;

                for (int i = 0; i < significant.Count; i++)
                {
                    int countI = significant[i]
                        .Max(n => connCount.TryGetValue(n, out int c) ? c : 0);

                    for (int j = i + 1; j < significant.Count; j++)
                    {
                        int countJ = significant[j]
                            .Max(n => connCount.TryGetValue(n, out int c) ? c : 0);

                        if (countI >= connectionsPerComponent &&
                            countJ >= connectionsPerComponent)
                            continue;

                        foreach (var a in significant[i])
                        {
                            foreach (var b in significant[j])
                            {
                                if (blocked.Contains((a, b))) continue;

                                float d = Vector3.Distance(a.Position, b.Position);
                                if (d >= MaxStitchDistance) continue;

                                float normDist = d / MaxStitchDistance;
                                float roadMidY = (a.Position.y + b.Position.y) * 0.5f;
                                float terrainMidH = SampleMaxHeight(a.Position, b.Position, terrain);
                                float terrainBonus = (terrainMidH - roadMidY) * 0.001f;
                                float score = (1f - normDist) + terrainBonus;

                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestA = a;
                                    bestB = b;
                                    bestI = i;
                                    bestJ = j;
                                }
                            }
                        }
                    }
                }

                if (bestA == null) break;

                if (!StitchAngleAcceptable(bestA, bestB, adj))
                {
                    blocked.Add((bestA, bestB));
                    blocked.Add((bestB, bestA));
                    continue;
                }

                ConnectionType connType = Classify(
                    bestA.Position, bestB.Position,
                    bridgeHeightThreshold, tunnelHeightThreshold, terrain);

                graph.AddEdge(bestA, bestB, bestA.Type);

                adj[bestA].Add(bestB);
                adj[bestB].Add(bestA);

                connections.Add(new RoadConnection(bestA, bestB, connType));

                Debug.Log($"[RoadGraphConnector] Stitched {bestI}({significant[bestI].Count}) " +
                          $"-> {bestJ}({significant[bestJ].Count}) | " +
                          $"dist={Vector3.Distance(bestA.Position, bestB.Position):F1} | type={connType}");

                int newCount = Mathf.Max(
                    significant[bestI].Max(n => connCount.TryGetValue(n, out int c) ? c : 0),
                    significant[bestJ].Max(n => connCount.TryGetValue(n, out int c) ? c : 0)) + 1;

                foreach (var n in significant[bestI])
                    if (connCount.ContainsKey(n)) connCount[n] = newCount;
                foreach (var n in significant[bestJ])
                    if (connCount.ContainsKey(n)) connCount[n] = newCount;
            }

            return connections;
        }

        private static bool StitchAngleAcceptable(
            RoadNode a,
            RoadNode b,
            Dictionary<RoadNode, List<RoadNode>> adj)
        {
            Vector3 dir = (b.Position - a.Position).normalized;

            if (adj.TryGetValue(a, out var aNbs))
                foreach (var nb in aNbs)
                    if (Vector3.Angle(dir, (nb.Position - a.Position).normalized)
                        < MinStitchAngleDegrees)
                        return false;

            if (adj.TryGetValue(b, out var bNbs))
                foreach (var nb in bNbs)
                    if (Vector3.Angle(-dir, (nb.Position - b.Position).normalized)
                        < MinStitchAngleDegrees)
                        return false;

            return true;
        }

        private static void PruneSmallComponents(RoadGraph graph)
        {
            var components = FindComponents(graph);

            var keepNodes = new HashSet<RoadNode>();
            foreach (var component in components)
            {
                if (component.Count >= MinComponentSize)
                {
                    foreach (var node in component)
                    {
                        keepNodes.Add(node);
                    }
                }
            }
            if (keepNodes.Count == graph.Nodes.Count)
            {
                return;
            }

            graph.RetainNodes(keepNodes);
        }

        private static ConnectionType Classify(
            Vector3 a,
            Vector3 b,
            float bridgeT,
            float tunnelT,
            TerrainAdapter terrain)
        {
            float roadMidY = (a.y + b.y) * 0.5f;
            float terrainMidH = SampleMaxHeight(a, b, terrain);
            float delta = terrainMidH - roadMidY;

            if (delta > tunnelT)
            {
                return ConnectionType.Tunnel;
            }

            if (delta < -bridgeT)
            {
                return ConnectionType.Bridge;
            }

                return ConnectionType.Road;
        }

        private static float SampleMaxHeight(Vector3 a, Vector3 b, TerrainAdapter terrain)
        {
            if (terrain == null)
            {
                return (a.y + b.y) * 0.5f;
            }

            float max = float.MinValue;
            for (int i = 0; i <= TerrainSamples; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)TerrainSamples);
                max = Mathf.Max(max, terrain.SampleHeight(p.x, p.z));
            }
            return max;
        }

        public static List<List<RoadNode>> FindComponents(RoadGraph graph)
        {
            var adj = new Dictionary<RoadNode, List<RoadNode>>();
            foreach (var node in graph.Nodes)
            {
                adj[node] = new List<RoadNode>();
            }
            
            foreach (var edge in graph.Edges)
            {
                adj[edge.From].Add(edge.To);
                adj[edge.To].Add(edge.From);
            }

            var visited = new HashSet<RoadNode>();
            var components = new List<List<RoadNode>>();

            foreach (var node in graph.Nodes)
            {
                if (visited.Contains(node))
                {
                    continue;
                }

                var component = new List<RoadNode>();
                var queue = new Queue<RoadNode>();
                queue.Enqueue(node);
                visited.Add(node);

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    component.Add(cur);
                    foreach (var nb in adj[cur])
                    {
                        if (!visited.Contains(nb))
                        {
                            visited.Add(nb);
                            queue.Enqueue(nb);
                        }
                    }
                }

                components.Add(component);
            }

            return components;
        }
    }
}
