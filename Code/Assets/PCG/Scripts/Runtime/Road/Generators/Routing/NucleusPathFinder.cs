using System.Collections.Generic;

using Assets.Scripts.Runtime.Graph;

using UnityEngine;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class NucleusPathFinder
    {
        public static RoadNode FindClosestNode(RoadGraph graph, Vector2 centre)
        {
            RoadNode best = null;
            float bestDist = float.MaxValue;
            foreach (var node in graph.Nodes)
            {
                float d = Vector2.Distance(
                    new Vector2(node.Position.x, node.Position.z), centre);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = node;
                }
            }

            return best;
        }

        public static Dictionary<RoadNode, List<RoadEdge>> BuildAdjacency(RoadGraph graph)
        {
            var adj = new Dictionary<RoadNode, List<RoadEdge>>(graph.Nodes.Count);
            foreach (var node in graph.Nodes)
            {
                adj[node] = new List<RoadEdge>();
            }

            foreach (var edge in graph.Edges)
            {
                adj[edge.From].Add(edge);
                adj[edge.To].Add(edge);
            }

            return adj;
        }

        public static List<RoadEdge> FindPath(
            RoadGraph graph,
            Dictionary<RoadNode, List<RoadEdge>> adj,
            RoadNode start,
            RoadNode end,
            Vector2 directBearing,
            float bearingPenaltyWeight)
        {
            var gScore = new Dictionary<RoadNode, float>(graph.Nodes.Count);
            var prev = new Dictionary<RoadNode, (RoadNode node, RoadEdge edge)>();

            foreach (var node in graph.Nodes)
            {
                gScore[node] = float.MaxValue;
            }

            gScore[start] = 0f;
            int idCounter = 0;

            var open = new SortedSet<(float f, int id, RoadNode n)>(
                Comparer<(float, int, RoadNode)>.Create(
                    (a, b) => a.Item1 != b.Item1
                        ? a.Item1.CompareTo(b.Item1)
                        : a.Item2.CompareTo(b.Item2)));

            open.Add((Heuristic(start, end), idCounter++, start));

            while (open.Count > 0)
            {
                var (_, _, current) = open.Min;
                open.Remove(open.Min);

                if (current == end)
                {
                    break;
                }

                if (!adj.TryGetValue(current, out var edges))
                {
                    continue;
                }

                foreach (var edge in edges)
                {
                    RoadNode nb = edge.From == current ? edge.To : edge.From;
                    float edgeLen = Vector3.Distance(current.Position, nb.Position);
                    Vector2 edgeDir2 = new Vector2(
                        nb.Position.x - current.Position.x,
                        nb.Position.z - current.Position.z);

                    float edgeDirLen = edgeDir2.magnitude;
                    float alignment = edgeDirLen > 0f
                        ? Mathf.Clamp01(Vector2.Dot(edgeDir2 / edgeDirLen, directBearing))
                        : 0f;

                    float penalty = bearingPenaltyWeight * edgeLen * (1f - alignment);
                    float tentative = gScore[current] + edgeLen + penalty;

                    if (tentative < gScore[nb])
                    {
                        gScore[nb] = tentative;
                        prev[nb] = (current, edge);
                        float fScore = tentative + Heuristic(nb, end);
                        open.Add((fScore, idCounter++, nb));
                    }
                }
            }

            if (!prev.ContainsKey(end))
            {
                return null;
            }

            var path = new List<RoadEdge>();
            RoadNode cur = end;
            while (prev.ContainsKey(cur))
            {
                var (prevNode, edge) = prev[cur];
                path.Insert(0, edge);
                cur = prevNode;
            }

            return path;
        }

        private static float Heuristic(RoadNode a, RoadNode b)
            => Vector3.Distance(a.Position, b.Position);
    }
}
