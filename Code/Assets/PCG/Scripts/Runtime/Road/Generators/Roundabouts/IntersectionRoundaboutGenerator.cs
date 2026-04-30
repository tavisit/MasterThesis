using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;

using UnityEngine;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class IntersectionRoundaboutGenerator
    {
        public static void Spawn(
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
            var adjacency = new Dictionary<RoadNode, List<RoadEdge>>();
            foreach (var node in graph.Nodes)
            {
                degree[node] = 0;
                adjacency[node] = new List<RoadEdge>();
            }

            foreach (var edge in graph.Edges)
            {
                if (edge.Type != RoadType.Street)
                {
                    continue;
                }

                degree[edge.From]++;
                degree[edge.To]++;
                adjacency[edge.From].Add(edge);
                adjacency[edge.To].Add(edge);
            }

            foreach (var node in graph.Nodes)
            {
                if (!degree.TryGetValue(node, out int d) || d < 2)
                {
                    continue;
                }

                if (!adjacency.TryGetValue(node, out List<RoadEdge> edges) || edges == null || edges.Count == 0)
                {
                    continue;
                }

                if (d == 2 && !IsAngledBend(node, edges))
                {
                    continue;
                }

                float maxHalfWidth = 0f;
                for (int i = 0; i < edges.Count; i++)
                {
                    maxHalfWidth = Mathf.Max(
                        maxHalfWidth,
                        HalfWidthForStreetEdge(
                            edges[i],
                            roadSettings,
                            boulevardPriorityEdgeKeys,
                            boulevardWidthMultiplier,
                            manager.Nuclei));
                }

                float radius = Mathf.Max(
                    RoadGenerationOffsets.RoundaboutMinRadius,
                    maxHalfWidth * RoadGenerationOffsets.RoundaboutWidthMultiplier);
                radius *= RoadGenerationOffsets.RoundaboutRadiusScale;
                Vector3 centerWorld = node.Position;
                centerWorld.y = node.Position.y + manager.RoadMeshVerticalOffset + RoadGenerationOffsets.IntersectionRoundaboutRoadYOffset;
                Mesh mesh = RoundaboutMeshUtility.BuildDiscMesh(
                    parent,
                    centerWorld,
                    radius,
                    segments: RoadGenerationOffsets.RoundaboutDiscSegments,
                    meshName: "IntersectionRoundabout");
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
                    yOffset: manager.SidewalkMeshVerticalOffset + RoadGenerationOffsets.IntersectionRoundaboutSidewalkYOffsetDelta,
                    meshName: "IntersectionRoundabout_Sidewalk");

                var go = new GameObject("IntersectionRoundabout");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();

                Material mat = NeighborhoodStyleEvaluator.Evaluate(centerWorld, manager.Nuclei).RoadMaterial;
                if (mat == null)
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

                mr.sharedMaterial = mat;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_LaneCount", 0f);
                mr.SetPropertyBlock(block);
                trackGenerated.Add(go);

                if (sidewalkMesh != null)
                {
                    var sw = new GameObject("IntersectionRoundabout_Sidewalk");
                    sw.transform.SetParent(parent, false);
                    sw.transform.localPosition = Vector3.zero;
                    sw.transform.localRotation = Quaternion.identity;
                    sw.AddComponent<MeshFilter>().sharedMesh = sidewalkMesh;
                    var swRenderer = sw.AddComponent<MeshRenderer>();
                    swRenderer.sharedMaterial = NeighborhoodStyleEvaluator.Evaluate(centerWorld, manager.Nuclei).SidewalkMaterial;
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

        private static bool IsAngledBend(RoadNode node, List<RoadEdge> edges)
        {
            if (node == null || edges == null || edges.Count != 2)
            {
                return false;
            }

            Vector3 d0 = (GetOtherNode(edges[0], node).Position - node.Position);
            Vector3 d1 = (GetOtherNode(edges[1], node).Position - node.Position);
            d0.y = 0f;
            d1.y = 0f;
            if (d0.sqrMagnitude < 1e-6f || d1.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            d0.Normalize();
            d1.Normalize();
            float angle = Vector3.Angle(d0, d1);
            // Straight continuations do not need a smoothing roundabout.
            return angle < 165f;
        }

        private static RoadNode GetOtherNode(RoadEdge edge, RoadNode node)
        {
            return edge.From == node ? edge.To : edge.From;
        }

    }
}
