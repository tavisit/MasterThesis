using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class MetroGenerator
    {
        private const float MetroYBoost = 6f;

        public static List<SplineContainer> Generate(
            RoadGraph streetGraph,
            CityNucleus[] nuclei,
            Transform parent2,
            UrbanMorphology morphology,
            float bearingPenaltyWeight = 0.6f)
        {
            var metroContainers = new List<SplineContainer>();

            if (nuclei == null || nuclei.Length < 2 || streetGraph.Nodes.Count == 0)
            {
                return metroContainers;
            }

            var adj = BuildAdjacency(streetGraph);

            var nucleusNodes = new RoadNode[nuclei.Length];
            for (int i = 0; i < nuclei.Length; i++)
            {
                nucleusNodes[i] = FindClosestNode(streetGraph, nuclei[i].Centre);
            }

            var allPairs = new List<(float dist, int i, int j)>();
            for (int i = 0; i < nuclei.Length; i++)
            {
                for (int j = i + 1; j < nuclei.Length; j++)
                {
                    if (nucleusNodes[i] != null && nucleusNodes[j] != null)
                    {
                        allPairs.Add((Vector2.Distance(nuclei[i].Centre, nuclei[j].Centre), i, j));
                    }
                }
            }

            allPairs.Sort((a, b) => a.dist.CompareTo(b.dist));

            var parent = new int[nuclei.Length];
            for (int i = 0; i < nuclei.Length; i++)
            {
                parent[i] = i;
            }

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    x = parent[x] = parent[parent[x]];
                }

                return x;
            }

            foreach (var (_, i, j) in allPairs)
            {
                if (Find(i) == Find(j))
                {
                    continue;
                }

                parent[Find(i)] = Find(j);

                Vector2 directBearing = (nuclei[j].Centre - nuclei[i].Centre).normalized;

                List<RoadEdge> path = AStar(
                    streetGraph, adj,
                    nucleusNodes[i], nucleusNodes[j],
                    directBearing, bearingPenaltyWeight);

                SplineContainer container = path != null && path.Count > 0
                    ? BuildSplineFromPath(path, parent2, morphology)
                    : BuildDirectSpline(nucleusNodes[i].Position, nucleusNodes[j].Position,
                                        parent2, morphology);

                if (container != null)
                {
                    metroContainers.Add(container);
                }
            }

            return metroContainers;
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


        private static SplineContainer BuildSplineFromPath(
            List<RoadEdge> path,
            Transform parent,
            UrbanMorphology morphology)
        {
            if (path == null || path.Count == 0)
            {
                return null;
            }

            var positions = new List<Vector3>();
            Vector3 Lift(Vector3 p) => new Vector3(p.x, p.y + MetroYBoost, p.z);

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

            return BuildContainer(reduced, parent, morphology, "MetroSpline");
        }

        private static SplineContainer BuildDirectSpline(
            Vector3 from,
            Vector3 to,
            Transform parent,
            UrbanMorphology morphology)
        {
            Vector3 Lift(Vector3 p) => new Vector3(p.x, p.y + MetroYBoost, p.z);
            var positions = new List<Vector3> { Lift(from), Lift(to) };
            return BuildContainer(positions, parent, morphology, "MetroSpline_Direct");
        }

        private static SplineContainer BuildContainer(
            List<Vector3> positions,
            Transform parent,
            UrbanMorphology morphology,
            string name)
        {
            if (positions.Count < 2)
            {
                return null;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var container = go.AddComponent<SplineContainer>();
            var spline = container.Spline;
            spline.Clear();

            TangentMode mode = morphology == UrbanMorphology.Organic
                    ? TangentMode.AutoSmooth
                    : TangentMode.Linear; ;

            foreach (var pos in positions)
            {
                spline.Add(new BezierKnot(
                    new float3(pos.x, pos.y, pos.z),
                    float3.zero, float3.zero), mode);
            }

            return container;
        }
    }
}
