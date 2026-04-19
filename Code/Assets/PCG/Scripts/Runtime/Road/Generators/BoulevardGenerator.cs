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
                    edgeKeys.Add(ToEdgeKey(edge.From.Position, edge.To.Position));
                }
            }

            return edgeKeys;
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
                RoadGraph pathGraph;
                pathGraph = PathToChainGraph(path);

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

            int lineBudget = maxLines <= 0 ? int.MaxValue : maxLines;
            var adj = NucleusPathFinder.BuildAdjacency(streetGraph);
            var nucleusNodes = new RoadNode[nuclei.Length];
            for (int i = 0; i < nuclei.Length; i++)
            {
                nucleusNodes[i] = NucleusPathFinder.FindClosestNode(streetGraph, nuclei[i].Centre);
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
                parentByNucleus[Find(i)] = Find(j);

                RoadNode start = nucleusNodes[i];
                RoadNode end = nucleusNodes[j];
                Vector2 bearing = (nuclei[j].Centre - nuclei[i].Centre).normalized;
                List<RoadEdge> path = NucleusPathFinder.FindPath(
                    streetGraph, adj, start, end, bearing, bearingPenaltyWeight);

                if (path != null && path.Count > 0)
                {
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

            var chainNodes = new List<RoadNode> { path[0].From };
            RoadNode current = path[0].From;
            foreach (var edge in path)
            {
                if (edge.From == current)
                {
                    current = edge.To;
                }
                else if (edge.To == current)
                {
                    current = edge.From;
                }
                else
                {
                    return null;
                }

                chainNodes.Add(current);
            }

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

        private static string ToEdgeKey(Vector3 a, Vector3 b)
        {
            string pa = ToPointKey(a);
            string pb = ToPointKey(b);
            return string.CompareOrdinal(pa, pb) <= 0 ? pa + "|" + pb : pb + "|" + pa;
        }

        private static string ToPointKey(Vector3 p)
        {
            int x = Mathf.RoundToInt(p.x * 10f);
            int y = Mathf.RoundToInt(p.y * 10f);
            int z = Mathf.RoundToInt(p.z * 10f);
            return x + "," + y + "," + z;
        }
    }
}
