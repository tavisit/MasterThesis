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
            float bearingPenaltyWeight = 0.6f,
            int maxLines = 1)
        {
            var metroContainers = new List<SplineContainer>();

            if (nuclei == null || nuclei.Length < 2 || streetGraph.Nodes.Count == 0)
            {
                return metroContainers;
            }

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

            int lineBudget = maxLines <= 0 ? int.MaxValue : maxLines;
            foreach (var (_, i, j) in allPairs)
            {
                if (metroContainers.Count >= lineBudget)
                {
                    break;
                }

                if (Find(i) == Find(j))
                {
                    continue;
                }

                parent[Find(i)] = Find(j);

                Vector2 directBearing = (nuclei[j].Centre - nuclei[i].Centre).normalized;

                List<RoadEdge> path = NucleusPathFinder.FindPath(
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

            var cleaned = new List<Vector3>(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 p = positions[i];
                if (!IsFinite(p))
                {
                    continue;
                }

                if (cleaned.Count == 0 || Vector3.Distance(cleaned[cleaned.Count - 1], p) > 0.5f)
                {
                    cleaned.Add(p);
                }
            }
            if (cleaned.Count < 2)
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
                    : TangentMode.Linear;

            foreach (var pos in cleaned)
            {
                spline.Add(new BezierKnot(
                    new float3(pos.x, pos.y, pos.z),
                    float3.zero, float3.zero), mode);
            }

            return container;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                   !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
                   !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }
    }
}
