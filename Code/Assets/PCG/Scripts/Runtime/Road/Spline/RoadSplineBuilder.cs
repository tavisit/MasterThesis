using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Spline
{
    public static class RoadSplineBuilder
    {
        private const float OrganicTangentFraction = 0.20f;

        public static List<SplineContainer> BuildSplines(
            RoadGraph graph,
            Transform parent,
            RoadSettings roadSettings,
            UrbanMorphology morphology = UrbanMorphology.Grid,
            int seed = 0,
            float junctionEndInset = 0f)
        {
            var containers = new List<SplineContainer>();
            var chains = graph.ExtractChains();
            HashSet<RoadNode> junctionNodes = junctionEndInset > 0f
                ? BuildJunctionNodes(graph)
                : null;

            foreach (var chain in chains)
            {
                if (chain.Count < 2)
                {
                    continue;
                }

                var go = new GameObject($"RoadSpline_{chain[0].Type}");
                go.transform.SetParent(parent, worldPositionStays: false);

                var container = go.AddComponent<SplineContainer>();
                var spline = container.Spline;
                spline.Clear();

                bool isOrganic = morphology == UrbanMorphology.Organic;

                var positions = BuildPositions(chain, isOrganic, seed);

                if (junctionNodes != null && junctionEndInset > 0f)
                {
                    positions = ApplyJunctionEndInset(positions, chain, junctionNodes, junctionEndInset);
                }

                if (positions.Count < 2)
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                TangentMode mode = morphology == UrbanMorphology.Organic
                ? TangentMode.AutoSmooth
                : TangentMode.Linear;

                for (int i = 0; i < positions.Count; i++)
                {
                    Vector3 pos = positions[i];

                    if (mode == TangentMode.Broken)
                    {
                        float3 inTan = float3.zero;
                        float3 outTan = float3.zero;

                        if (i > 0)
                        {
                            Vector3 prev = positions[i - 1];
                            float segLen = Vector3.Distance(prev, pos);
                            Vector3 dir = (pos - prev).normalized;
                            float handle = segLen * OrganicTangentFraction;
                            inTan = new float3(-dir.x, -dir.y, -dir.z) * handle;
                        }

                        if (i < positions.Count - 1)
                        {
                            Vector3 next = positions[i + 1];
                            float segLen = Vector3.Distance(pos, next);
                            Vector3 dir = (next - pos).normalized;
                            float handle = segLen * OrganicTangentFraction;
                            outTan = new float3(dir.x, dir.y, dir.z) * handle;
                        }

                        spline.Add(new BezierKnot(
                            new float3(pos.x, pos.y, pos.z),
                            inTan, outTan), mode);
                    }
                    else
                    {
                        spline.Add(new BezierKnot(
                            new float3(pos.x, pos.y, pos.z),
                            float3.zero, float3.zero), mode);
                    }
                }

                containers.Add(container);
            }

            return containers;
        }

        private static HashSet<RoadNode> BuildJunctionNodes(RoadGraph graph)
        {
            var degree = new Dictionary<RoadNode, int>();
            foreach (var edge in graph.Edges)
            {
                degree.TryGetValue(edge.From, out int df);
                degree[edge.From] = df + 1;
                degree.TryGetValue(edge.To, out int dt);
                degree[edge.To] = dt + 1;
            }

            var junctions = new HashSet<RoadNode>();
            foreach (var kvp in degree)
            {
                if (kvp.Value > 2)
                {
                    junctions.Add(kvp.Key);
                }
            }

            return junctions;
        }

        private static List<Vector3> ApplyJunctionEndInset(
            List<Vector3> positions,
            List<RoadNode> chain,
            HashSet<RoadNode> junctionNodes,
            float inset)
        {
            if (positions.Count < 2 || inset <= 0f || chain.Count < 2)
            {
                return positions;
            }

            var result = new List<Vector3>(positions);

            if (junctionNodes.Contains(chain[0]))
            {
                Vector3 graphDir = chain[1].Position - chain[0].Position;
                graphDir.y = 0f;
                float edgeLen = graphDir.magnitude;
                if (edgeLen > 1e-4f)
                {
                    graphDir /= edgeLen;
                    float travel = Mathf.Min(inset, edgeLen * 0.48f);
                    Vector3 p = chain[0].Position + graphDir * travel;
                    p.y = result[0].y;
                    result[0] = p;
                }
            }

            if (junctionNodes.Contains(chain[^1]))
            {
                Vector3 inward = chain[^2].Position - chain[^1].Position;
                inward.y = 0f;
                float edgeLen = inward.magnitude;
                if (edgeLen > 1e-4f)
                {
                    inward /= edgeLen;
                    float travel = Mathf.Min(inset, edgeLen * 0.48f);
                    int last = result.Count - 1;
                    Vector3 p = chain[^1].Position + inward * travel;
                    p.y = result[last].y;
                    result[last] = p;
                }
            }

            return result;
        }

        private static List<Vector3> BuildPositions(
            List<RoadNode> chain,
            bool isOrganic,
            int seed)
        {
            if (!isOrganic)
            {
                var flat = new List<Vector3>();
                for (int i = 0; i < chain.Count; i++)
                {
                    flat.Add(chain[i].Position);
                }

                return flat;
            }

            var rng = new System.Random(seed + chain[0].Id);
            var positions = new List<Vector3> { chain[0].Position };

            for (int i = 0; i < chain.Count - 1; i++)
            {
                Vector3 a = chain[i].Position;
                Vector3 b = chain[i + 1].Position;
                Vector3 dir = b - a;
                dir.y = 0f;
                float segLen = dir.magnitude;

                if (segLen > 0.5f)
                {
                    Vector3 perp = new Vector3(-dir.z, 0f, dir.x).normalized;
                    float maxOffset = segLen * 0.10f;
                    float offset = (float)(rng.NextDouble() * 2.0 - 1.0) * maxOffset;
                    Vector3 mid = (a + b) * 0.5f;
                    mid += perp * offset;
                    mid.y = (a.y + b.y) * 0.5f;
                    positions.Add(mid);
                }

                positions.Add(b);
            }

            return positions;
        }
    }
}
