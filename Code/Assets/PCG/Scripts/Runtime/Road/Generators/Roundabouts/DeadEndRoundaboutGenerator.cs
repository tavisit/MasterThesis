using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class DeadEndRoundaboutGenerator
    {
        public static void SpawnCaps(
            RoadGraph graph,
            Transform parent,
            CityManager manager,
            RoadSettings roadSettings,
            HashSet<string> boulevardPriorityEdgeKeys,
            float boulevardWidthMultiplier,
            List<GameObject> trackGenerated)
        {
            if (graph == null || parent == null || manager == null || roadSettings == null || trackGenerated == null)
            {
                return;
            }

            var degree = new Dictionary<RoadNode, int>();
            var adjacency = new Dictionary<RoadNode, RoadEdge>();
            foreach (var node in graph.Nodes)
            {
                degree[node] = 0;
            }

            foreach (var edge in graph.Edges)
            {
                if (edge.Type != RoadType.Street)
                {
                    continue;
                }

                if (boulevardPriorityEdgeKeys != null &&
                    boulevardPriorityEdgeKeys.Contains(
                        RoadGraphKeyUtility.ToEdgeKey(edge.From.Position, edge.To.Position)))
                {
                    continue;
                }

                degree[edge.From]++;
                degree[edge.To]++;
                adjacency[edge.From] = edge;
                adjacency[edge.To] = edge;
            }

            foreach (var node in graph.Nodes)
            {
                if (!degree.TryGetValue(node, out int d) || d != 1 || !adjacency.TryGetValue(node, out RoadEdge edge))
                {
                    continue;
                }

                RoadNode other = edge.From == node ? edge.To : edge.From;
                Vector3 inward = other.Position - node.Position;
                inward.y = 0f;
                if (inward.sqrMagnitude < 1e-6f)
                {
                    continue;
                }

                inward.Normalize();
                Vector3 outward = -inward;

                float halfWidth = HalfWidthForStreetEdge(
                    edge,
                    roadSettings,
                    boulevardPriorityEdgeKeys,
                    boulevardWidthMultiplier,
                    manager.Nuclei);
                float radius = Mathf.Max(
                    RoadGenerationOffsets.RoundaboutMinRadius,
                    halfWidth * RoadGenerationOffsets.RoundaboutWidthMultiplier);
                radius *= RoadGenerationOffsets.RoundaboutRadiusScale;
                // Keep the cap centered near the dead-end node even if
                // local spline curvature drifts from the graph edge direction.
                Vector3 centerWorld = node.Position + outward * (radius * RoadGenerationOffsets.DeadEndRoundaboutCenterOutwardFactor);
                centerWorld.y = node.Position.y + manager.RoadMeshVerticalOffset + RoadGenerationOffsets.DeadEndRoundaboutRoadYOffset;

                Mesh mesh = RoundaboutMeshUtility.BuildDiscMesh(
                    parent,
                    centerWorld,
                    radius,
                    segments: RoadGenerationOffsets.RoundaboutDiscSegments,
                    meshName: "RoadStubRoundabout");
                if (mesh == null)
                {
                    continue;
                }
                Mesh sidewalkMesh = RoundaboutMeshUtility.BuildSidewalkRingMesh(
                    parent,
                    centerWorld,
                    innerRadius: radius,
                    outerRadius: radius + RoadGenerationOffsets.RoundaboutSidewalkWidth,
                    segments: RoadGenerationOffsets.RoundaboutSidewalkSegments,
                    yOffset: manager.SidewalkMeshVerticalOffset + RoadGenerationOffsets.DeadEndRoundaboutSidewalkYOffsetDelta,
                    meshName: "RoadStubRoundabout_Sidewalk");

                var go = new GameObject("RoadStubRoundabout");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();

                Material roadMaterial = ResolveStreetMaterial(parent, node.Position, centerWorld, manager.Nuclei);
                if (roadMaterial == null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        Object.DestroyImmediate(go);
                    }
                    else
#endif
                    {
                        Object.Destroy(go);
                    }

                    continue;
                }

                mr.sharedMaterial = roadMaterial;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_LaneCount", 0f);
                mr.SetPropertyBlock(block);
                trackGenerated.Add(go);

                if (sidewalkMesh != null)
                {
                    var sw = new GameObject("RoadStubRoundabout_Sidewalk");
                    sw.transform.SetParent(parent, false);
                    sw.transform.localPosition = Vector3.zero;
                    sw.transform.localRotation = Quaternion.identity;
                    sw.AddComponent<MeshFilter>().sharedMesh = sidewalkMesh;
                    var swRenderer = sw.AddComponent<MeshRenderer>();
                    Material swMat = ResolveSidewalkMaterial(parent, node.Position, centerWorld, manager.Nuclei);
                    swRenderer.sharedMaterial = swMat;
                    trackGenerated.Add(sw);
                }
            }
        }

        private static float HalfWidthForStreetEdge(
            RoadEdge edge,
            RoadSettings roadSettings,
            HashSet<string> boulevardPriorityEdgeKeys,
            float boulevardWidthMultiplier,
            CityNucleus[] nuclei)
        {
            Vector3 mid = (edge.From.Position + edge.To.Position) * 0.5f;
            float widthMultiplier = 1f;
            if (nuclei != null && nuclei.Length > 0)
            {
                widthMultiplier = Mathf.Max(0.01f, NeighborhoodStyleEvaluator.Evaluate(mid, nuclei).RoadWidthMultiplier);
            }

            float halfWidth = RoadMeshExtruder.GetHalfWidth(RoadType.Street, roadSettings) * widthMultiplier;
            if (boulevardPriorityEdgeKeys != null &&
                boulevardPriorityEdgeKeys.Contains(RoadGraphKeyUtility.ToEdgeKey(edge.From.Position, edge.To.Position)))
            {
                halfWidth *= Mathf.Max(0.01f, boulevardWidthMultiplier);
            }

            return halfWidth;
        }

        private static Material ResolveStreetMaterial(
            Transform root,
            Vector3 nodePos,
            Vector3 fallbackPos,
            CityNucleus[] nuclei)
        {
            Material best = null;
            float bestScore = float.MaxValue;
            if (root != null)
            {
                foreach (Transform child in root)
                {
                    if (child == null || child.name != "RoadSpline_Street")
                    {
                        continue;
                    }

                    var sc = child.GetComponent<UnityEngine.Splines.SplineContainer>();
                    var mr = child.GetComponent<MeshRenderer>();
                    if (sc == null || sc.Spline == null || mr == null || mr.sharedMaterial == null)
                    {
                        continue;
                    }

                    Vector3 p0 = GetWorldPointOnSpline(sc, 0f);
                    Vector3 p1 = GetWorldPointOnSpline(sc, 1f);
                    float endpointScore = Mathf.Min(
                        (p0 - nodePos).sqrMagnitude,
                        (p1 - nodePos).sqrMagnitude);
                    if (endpointScore < bestScore)
                    {
                        bestScore = endpointScore;
                        best = mr.sharedMaterial;
                    }
                }
            }

            if (best != null && bestScore <= 25f)
            {
                return best;
            }

            return NeighborhoodStyleEvaluator.Evaluate(fallbackPos, nuclei).RoadMaterial;
        }

        private static Material ResolveSidewalkMaterial(
            Transform root,
            Vector3 nodePos,
            Vector3 fallbackPos,
            CityNucleus[] nuclei)
        {
            Material best = null;
            float bestScore = float.MaxValue;
            if (root != null)
            {
                foreach (Transform child in root)
                {
                    if (child == null || child.name != "RoadSpline_Street")
                    {
                        continue;
                    }

                    Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        Renderer r = renderers[i];
                        if (r == null || r.sharedMaterial == null ||
                            (r.gameObject.name != "StreetSidewalk" && r.gameObject.name != "StreetSidewalk_BoulevardInterior"))
                        {
                            continue;
                        }

                        Bounds b = r.bounds;
                        Vector3 c = b.center;
                        c.y = nodePos.y;
                        float score = (c - nodePos).sqrMagnitude;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = r.sharedMaterial;
                        }
                    }
                }
            }

            if (best != null)
            {
                return best;
            }

            return NeighborhoodStyleEvaluator.Evaluate(fallbackPos, nuclei).SidewalkMaterial;
        }

        private static Vector3 GetWorldPointOnSpline(UnityEngine.Splines.SplineContainer container, float t)
        {
            container.Spline.Evaluate(t, out var pos, out _, out _);
            return container.transform.TransformPoint((Vector3)pos);
        }
    }
}
