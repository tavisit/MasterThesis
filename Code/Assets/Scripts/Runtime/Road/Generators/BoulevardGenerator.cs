using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class BoulevardGenerator
    {
        public static (List<SplineContainer> boulevards, RoadGraph remainingStreets) Generate(
            RoadGraph streetGraph,
            CityNucleus[] nuclei,
            Transform parent,
            UrbanMorphology morphology,
            int connectionsPerNucleus = 2,
            float mountainTunnelThreshold = 8f,
            float bearingPenaltyWeight = 0.6f)
        {
            var boulevardContainers = new List<SplineContainer>();
            if (nuclei == null || nuclei.Length < 2)
            {
                return (boulevardContainers, streetGraph);
            }
            var adj = BuildAdjacency(streetGraph);

            var nucleusNodes = new RoadNode[nuclei.Length];
            for (int i = 0; i < nuclei.Length; i++)
            {
                nucleusNodes[i] = FindClosestNode(streetGraph, nuclei[i].Centre);
            }

            var connected = new HashSet<(int, int)>();

            for (int i = 0; i < nuclei.Length; i++)
            {
                if (nucleusNodes[i] == null)
                {
                    continue;
                }
                var distances = new List<(float d, int j)>();
                for (int j = 0; j < nuclei.Length; j++)
                {
                    if (i == j || nucleusNodes[j] == null)
                    {
                        continue;
                    }

                    distances.Add((Vector2.Distance(nuclei[i].Centre, nuclei[j].Centre), j));
                }
                distances.Sort((a, b) => a.d.CompareTo(b.d));

                int count = 0;
                foreach (var (_, j) in distances)
                {
                    if (count >= connectionsPerNucleus)
                    {
                        break;
                    }

                    int a = Mathf.Min(i, j), b = Mathf.Max(i, j);
                    if (connected.Contains((a, b)))
                    {
                        continue;
                    }

                    connected.Add((a, b));
                    count++;

                    Vector2 directBearing =
                        (nuclei[j].Centre - nuclei[i].Centre).normalized;

                    List<RoadEdge> path = AStar(
                        streetGraph, adj,
                        nucleusNodes[i], nucleusNodes[j],
                        directBearing,
                        bearingPenaltyWeight);

                    if (path == null || path.Count == 0)
                    {
                        continue;
                    }

                    var container = BuildSplineFromPath(path, parent, morphology);
                    if (container != null)
                    {
                        boulevardContainers.Add(container);
                    }
                }
            }
            return (boulevardContainers, streetGraph);
        }

        private static Dictionary<RoadNode, List<RoadEdge>> BuildAdjacency(RoadGraph graph)
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

        private static RoadNode FindClosestNode(RoadGraph graph, Vector2 centre)
        {
            RoadNode best = null;
            float bestDist = float.MaxValue;
            foreach (var node in graph.Nodes)
            {
                float d = Vector2.Distance(
                    new Vector2(node.Position.x, node.Position.z), centre);
                if (d < bestDist) { bestDist = d; best = node; }
            }
            return best;
        }

        private static List<RoadEdge> AStar(
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
                    RoadNode neighbour = edge.From == current ? edge.To : edge.From;
                    float edgeLen = Vector3.Distance(current.Position, neighbour.Position);

                    Vector2 edgeDir = new Vector2(
                        neighbour.Position.x - current.Position.x,
                        neighbour.Position.z - current.Position.z);

                    float edgeDirLen = edgeDir.magnitude;
                    float alignment = edgeDirLen > 0f
                        ? Mathf.Clamp01(Vector2.Dot(edgeDir / edgeDirLen, directBearing))
                        : 0f;

                    float deviationPenalty = bearingPenaltyWeight * edgeLen * (1f - alignment);
                    float tentative = gScore[current] + edgeLen + deviationPenalty;

                    if (tentative < gScore[neighbour])
                    {
                        gScore[neighbour] = tentative;
                        prev[neighbour] = (current, edge);
                        float fScore = tentative + Heuristic(neighbour, end);
                        open.Add((fScore, idCounter++, neighbour));
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

        private const float BoulevardYBoost = 0.1f;

        private static SplineContainer BuildSplineFromPath(
            List<RoadEdge> path,
            Transform parent,
            UrbanMorphology morphology)
        {
            if (path == null || path.Count == 0)
            {
                return null;
            }

            var go = new GameObject("BoulevardSpline");
            go.transform.SetParent(parent, false);

            var container = go.AddComponent<SplineContainer>();
            var spline = container.Spline;
            spline.Clear();

            var positions = new List<Vector3>();
            Vector3 Lift(Vector3 p) => new Vector3(p.x, p.y + BoulevardYBoost, p.z);

            positions.Add(Lift(path[0].From.Position));
            foreach (var edge in path)
            {
                positions.Add(Lift(edge.To.Position));
            }
            var reduced = new List<Vector3> { positions[0] };
            for (int i = 1; i < positions.Count - 1; i++)
            {
                if (i % 3 == 0)
                {
                    reduced.Add(positions[i]);
                }
            }

            reduced.Add(positions[positions.Count - 1]);
            TangentMode mode = morphology == UrbanMorphology.Organic
                ? TangentMode.AutoSmooth
                : TangentMode.Linear;

            foreach (var pos in reduced)
            {
                spline.Add(new BezierKnot(
                    new float3(pos.x, pos.y, pos.z),
                    float3.zero, float3.zero
                ), mode);
            }

            return container;
        }
    }
}
