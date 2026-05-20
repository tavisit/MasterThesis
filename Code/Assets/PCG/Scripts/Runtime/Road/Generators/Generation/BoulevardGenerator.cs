using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.Spline;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class BoulevardGenerator
    {
        public static HashSet<string> BuildPriorityEdgeKeys(
            RoadGraph streetGraph,
            CityNucleus[] nuclei,
            float bearingPenaltyWeight,
            int maxLines)
        {
            var edgeKeys = new HashSet<string>();
            foreach (var path in BuildNucleusPaths(streetGraph, nuclei, bearingPenaltyWeight, maxLines))
            {
                foreach (var edge in path)
                {
                    edgeKeys.Add(RoadGraphKeyUtility.ToEdgeKey(edge.From.Position, edge.To.Position));
                }
            }

            return edgeKeys;
        }

        public static HashSet<RoadNode> CollectBoulevardPathNodes(
            RoadGraph streetGraph,
            CityNucleus[] nuclei,
            float bearingPenaltyWeight,
            int maxLines)
        {
            var nodes = new HashSet<RoadNode>();
            if (streetGraph == null || nuclei == null || nuclei.Length < 2 || streetGraph.Nodes.Count == 0)
            {
                return nodes;
            }

            foreach (var path in BuildNucleusPaths(streetGraph, nuclei, bearingPenaltyWeight, maxLines))
            {
                foreach (var edge in path)
                {
                    nodes.Add(edge.From);
                    nodes.Add(edge.To);
                }
            }

            return nodes;
        }

        public static List<SplineContainer> Generate(
            RoadGraph streetGraph,
            CityNucleus[] nuclei,
            Transform parent,
            RoadSettings roadSettings,
            UrbanMorphology morphology,
            float bearingPenaltyWeight,
            int maxLines)
        {
            var list = new List<SplineContainer>();

            if (nuclei == null || nuclei.Length < 2 || streetGraph.Nodes.Count == 0)
            {
                return list;
            }

            foreach (var path in BuildNucleusPaths(streetGraph, nuclei, bearingPenaltyWeight, maxLines))
            {
                RoadGraph pathGraph = PathToChainGraph(path);
                if (pathGraph == null || pathGraph.Edges.Count == 0)
                {
                    continue;
                }

                int seed = path.GetHashCode();

                list.AddRange(RoadSplineBuilder.BuildSplines(
                    pathGraph, parent, roadSettings, morphology, seed));
            }

            foreach (var c in list)
            {
                if (c != null)
                {
                    c.gameObject.name = "RoadSpline_Boulevard";
                }
            }

            return list;
        }

        private static List<List<RoadEdge>> BuildNucleusPaths(
            RoadGraph streetGraph,
            CityNucleus[] nuclei,
            float bearingPenaltyWeight,
            int maxLines)
        {
            var result = new List<List<RoadEdge>>();
            if (nuclei == null || nuclei.Length < 2 || streetGraph.Nodes.Count == 0)
            {
                return result;
            }

            var adj = NucleusPathFinder.BuildAdjacency(streetGraph);
            var nucleusNodes = new RoadNode[nuclei.Length];
            int validNucleusCount = 0;
            for (int i = 0; i < nuclei.Length; i++)
            {
                nucleusNodes[i] = NucleusPathFinder.FindClosestNode(streetGraph, nuclei[i].Centre);
                if (nucleusNodes[i] != null)
                {
                    validNucleusCount++;
                }
            }
            int minimumConnectionsNeeded = Mathf.Max(0, validNucleusCount - 1);
            int lineBudget = maxLines <= 0
                ? int.MaxValue
                : Mathf.Max(maxLines, minimumConnectionsNeeded);

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

            var parentByNucleus = new int[nuclei.Length];
            for (int i = 0; i < nuclei.Length; i++)
            {
                parentByNucleus[i] = i;
            }

            int Find(int x)
            {
                while (parentByNucleus[x] != x)
                {
                    x = parentByNucleus[x] = parentByNucleus[parentByNucleus[x]];
                }
                return x;
            }

            foreach (var (_, i, j) in allPairs)
            {
                if (result.Count >= lineBudget)
                {
                    break;
                }

                if (Find(i) == Find(j))
                {
                    continue;
                }
                RoadNode start = nucleusNodes[i];
                RoadNode end = nucleusNodes[j];
                Vector2 bearing = (nuclei[j].Centre - nuclei[i].Centre).normalized;
                List<RoadEdge> path = NucleusPathFinder.FindPath(
                    streetGraph, adj, start, end, bearing, bearingPenaltyWeight);

                if (path != null && path.Count > 0)
                {
                    parentByNucleus[Find(i)] = Find(j);
                    result.Add(path);
                }
            }

            return result;
        }

        private static RoadGraph PathToChainGraph(List<RoadEdge> path)
        {
            if (path == null || path.Count == 0)
            {
                return null;
            }

            List<RoadEdge> ordered = OrderPathEdgesIntoWalk(path, out RoadNode walkStart);
            if (ordered == null)
            {
                ordered = path;
                walkStart = ResolveWalkStart(path);
            }

            List<RoadNode> chainNodes = BuildChainNodeSequence(ordered, walkStart);
            if (chainNodes == null && !ReferenceEquals(ordered, path))
            {
                walkStart = ResolveWalkStart(path);
                chainNodes = BuildChainNodeSequence(path, walkStart);
            }

            if (chainNodes == null || chainNodes.Count < 2)
            {
                return null;
            }

            return ChainNodesToGraph(chainNodes);
        }

        private static List<RoadNode> BuildChainNodeSequence(IReadOnlyList<RoadEdge> orderedPath, RoadNode start)
        {
            if (orderedPath == null || orderedPath.Count == 0 || start == null)
            {
                return null;
            }

            var nodes = new List<RoadNode>(orderedPath.Count + 1) { start };
            RoadNode current = start;
            for (int i = 0; i < orderedPath.Count; i++)
            {
                RoadNode next = NextNodeOnEdge(orderedPath[i], current);
                if (next == null)
                {
                    return null;
                }

                nodes.Add(next);
                current = next;
            }

            return nodes;
        }

        private static RoadNode ResolveWalkStart(IReadOnlyList<RoadEdge> path)
        {
            if (path.Count == 1)
            {
                return path[0].From;
            }

            RoadEdge e0 = path[0];
            RoadEdge e1 = path[1];
            if (EdgeContainsNode(e1, e0.To))
            {
                return e0.From;
            }

            if (EdgeContainsNode(e1, e0.From))
            {
                return e0.To;
            }

            return FindPathEndpoint(path) ?? e0.From;
        }

        private static RoadNode FindPathEndpoint(IReadOnlyList<RoadEdge> path)
        {
            var degree = new Dictionary<RoadNode, int>();
            foreach (var edge in path)
            {
                IncrementDegree(degree, edge.From);
                IncrementDegree(degree, edge.To);
            }

            RoadNode endpoint = null;
            int endpointCount = 0;
            foreach (var pair in degree)
            {
                if (pair.Value != 1)
                {
                    continue;
                }

                endpointCount++;
                endpoint = pair.Key;
            }

            return endpointCount == 1 || endpointCount == 2 ? endpoint : null;
        }

        private static void IncrementDegree(Dictionary<RoadNode, int> degree, RoadNode node)
        {
            degree.TryGetValue(node, out int count);
            degree[node] = count + 1;
        }

        private static bool EdgeContainsNode(RoadEdge edge, RoadNode node)
        {
            return edge.From == node || edge.To == node;
        }

        private static RoadNode NextNodeOnEdge(RoadEdge edge, RoadNode current)
        {
            if (edge.From == current)
            {
                return edge.To;
            }

            if (edge.To == current)
            {
                return edge.From;
            }

            return null;
        }

        private static RoadGraph ChainNodesToGraph(IReadOnlyList<RoadNode> chainNodes)
        {
            var g = new RoadGraph();
            RoadNode prev = g.AddNode(chainNodes[0].Position);
            for (int i = 1; i < chainNodes.Count; i++)
            {
                RoadNode cur = g.AddNode(chainNodes[i].Position);
                g.AddEdge(prev, cur);
                prev = cur;
            }

            return g;
        }

        private static List<RoadEdge> OrderPathEdgesIntoWalk(IReadOnlyList<RoadEdge> path, out RoadNode walkStart)
        {
            walkStart = null;
            var adj = new Dictionary<RoadNode, List<RoadEdge>>();
            void AddAdj(RoadNode n, RoadEdge e)
            {
                if (!adj.TryGetValue(n, out var list))
                {
                    list = new List<RoadEdge>();
                    adj[n] = list;
                }

                list.Add(e);
            }

            foreach (var e in path)
            {
                AddAdj(e.From, e);
                AddAdj(e.To, e);
            }

            RoadNode leaf = FindPathEndpoint(path);
            if (leaf == null)
            {
                leaf = path[0].From;
            }

            walkStart = leaf;

            var used = new HashSet<RoadEdge>();
            var ordered = new List<RoadEdge>(path.Count);
            RoadNode cur = leaf;
            RoadNode prev = null;
            while (ordered.Count < path.Count)
            {
                RoadEdge pick = null;
                if (adj.TryGetValue(cur, out var incident))
                {
                    foreach (var e in incident)
                    {
                        if (used.Contains(e))
                        {
                            continue;
                        }

                        RoadNode other = e.From == cur ? e.To : e.From;
                        if (other == prev && incident.Count > 1)
                        {
                            continue;
                        }

                        pick = e;
                        break;
                    }

                    if (pick == null)
                    {
                        foreach (var e in incident)
                        {
                            if (!used.Contains(e))
                            {
                                pick = e;
                                break;
                            }
                        }
                    }
                }

                if (pick == null)
                {
                    return null;
                }

                used.Add(pick);
                ordered.Add(pick);
                prev = cur;
                cur = pick.From == cur ? pick.To : pick.From;
            }

            return ordered;
        }

    }
}
