using System.Collections.Generic;

using Assets.Scripts.Runtime.City;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class StreetLightPlacer
    {
        public static void PlaceLightPosts(
            SplineContainer container,
            float halfWidth,
            NeighborhoodStyleSample style,
            List<RoadSegmentData> roadSegments)
        {
            GameObject prefab = style.LightPostPrefab;
            if (prefab == null)
            {
                return;
            }

            float length = container.Spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            float interval = style.LightPostInterval;
            float offset = halfWidth + style.SidewalkWidth;
            var candidates = new List<(Vector3 localPos, Vector3 tangent, Vector3 up, Vector3 worldPos, bool flipForward)>();

            for (float d = interval * RoadGenerationOffsets.PlacementFirstSampleRatio; d < length; d += interval)
            {
                float t = d / length;
                container.Spline.Evaluate(t, out var pos3, out var tan3, out var up3);
                Vector3 p = (Vector3)pos3;
                Vector3 tangent = ((Vector3)tan3).normalized;
                Vector3 up = ((Vector3)up3).normalized;
                Vector3 right = Vector3.Cross(up, tangent).normalized;
                if (right.sqrMagnitude < 0.001f)
                {
                    right = Vector3.right;
                }

                Vector3 safeForward = GetSafeForward(tangent, Vector3.forward);
                Vector3 leftPos = p - right * offset;
                Vector3 rightPos = p + right * offset;
                candidates.Add((leftPos, safeForward, up, container.transform.TransformPoint(leftPos), flipForward: false));
                candidates.Add((rightPos, safeForward, up, container.transform.TransformPoint(rightPos), flipForward: true));
            }

            bool[] blocked = StreetRoadOverlapUtility.EvaluateRoadOverlap(
                candidates.ConvertAll(c => c.worldPos),
                roadSegments,
                runParallel: true);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (blocked[i] || IsWorldPointOnIntersectionRoundabout(container.transform.parent, candidates[i].worldPos))
                {
                    continue;
                }

                var c = candidates[i];
                SpawnLightPostPrefab(container.transform, c.worldPos, c.tangent, c.up, prefab, c.flipForward);
            }
        }

        public static void PlaceBoulevardInteriorLightPosts(
            SplineContainer container,
            NeighborhoodStyleSample style)
        {
            GameObject prefab = style.BoulevardInteriorLightPostPrefab;
            if (prefab == null || container == null || container.Spline == null)
            {
                return;
            }

            float length = container.Spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            float interval = style.BoulevardInteriorLightPostInterval;
            for (float d = interval * RoadGenerationOffsets.PlacementFirstSampleRatio; d < length; d += interval)
            {
                float t = d / length;
                container.Spline.Evaluate(t, out var pos3, out var tan3, out var up3);
                Vector3 p = (Vector3)pos3 + (Vector3)up3
                    * (style.BoulevardInteriorSidewalkVerticalOffset + RoadGenerationOffsets.BoulevardInteriorDecorationYOffsetDelta);
                Vector3 tangent = ((Vector3)tan3).normalized;
                Vector3 up = ((Vector3)up3).normalized;

                Vector3 safeForward = GetSafeForward(tangent, Vector3.forward);
                Vector3 worldPos = container.transform.TransformPoint(p);
                if (IsWorldPointOnIntersectionRoundabout(container.transform.parent, worldPos))
                {
                    continue;
                }

                SpawnLightPostPrefab(container.transform, worldPos, safeForward, up, prefab);
            }
        }

        private static void SpawnLightPostPrefab(
            Transform parent,
            Vector3 worldPos,
            Vector3 tangent,
            Vector3 up,
            GameObject prefab,
            bool flipForward = false)
        {
#if UNITY_EDITOR
            GameObject instance = Application.isPlaying
                ? Object.Instantiate(prefab, parent)
                : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
#else
            GameObject instance = Object.Instantiate(prefab, parent);
#endif
            if (instance == null)
            {
                return;
            }

            instance.name = $"StreetLight_{prefab.name}";
            Vector3 safeForward = GetSafeForward(tangent, Vector3.forward);
            if (flipForward)
            {
                safeForward = -safeForward;
            }
            Quaternion baseRotation = Quaternion.LookRotation(safeForward, up);
            instance.transform.SetPositionAndRotation(
                worldPos,
                baseRotation * Quaternion.Euler(-90f, 0f, 0f));
        }

        /// <summary>
        /// True when <paramref name="worldPos"/> lies within intersection roundabout renderer bounds (XZ disc).
        /// </summary>
        public static bool IsWorldPointOnIntersectionRoundabout(Transform root, Vector3 worldPos)
        {
            if (root == null)
            {
                return false;
            }

            foreach (Transform child in root)
            {
                if (child == null || child.name != "IntersectionRoundabout")
                {
                    continue;
                }
                Renderer r = child.GetComponent<Renderer>();
                if (r == null)
                {
                    continue;
                }

                Bounds b = r.bounds;
                if (worldPos.y < b.min.y - 1f || worldPos.y > b.max.y + 2f)
                {
                    continue;
                }

                Vector2 p = new Vector2(worldPos.x, worldPos.z);
                Vector2 c = new Vector2(b.center.x, b.center.z);
                float radius = Mathf.Max(b.extents.x, b.extents.z) + 0.15f;
                if ((p - c).sqrMagnitude <= radius * radius)
                {
                    return true;
                }
            }
            return false;
        }

        private static Vector3 GetSafeForward(Vector3 candidate, Vector3 fallback)
        {
            if (candidate.sqrMagnitude > 1e-6f &&
                !float.IsNaN(candidate.x) && !float.IsNaN(candidate.y) && !float.IsNaN(candidate.z) &&
                !float.IsInfinity(candidate.x) && !float.IsInfinity(candidate.y) && !float.IsInfinity(candidate.z))
            {
                return candidate.normalized;
            }

            if (fallback.sqrMagnitude > 1e-6f)
            {
                return fallback.normalized;
            }

            return Vector3.forward;
        }
    }
}
