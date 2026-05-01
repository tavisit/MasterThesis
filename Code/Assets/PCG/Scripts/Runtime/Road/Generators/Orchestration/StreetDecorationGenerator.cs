using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class StreetDecorationGenerator
    {
        private readonly struct DecorStyleContext
        {
            public readonly NeighborhoodStyleSample Style;

            public DecorStyleContext(NeighborhoodStyleSample style)
            {
                Style = style;
            }
        }

        public static void AddDecorations(
            SplineContainer container,
            RoadType roadType,
            CityManager manager,
            RoadSettings roadSettings,
            float widthMultiplier,
            bool forceStreetDecor = false,
            bool includeSidewalks = true)
        {
            if (container == null || container.Spline == null || manager == null)
            {
                return;
            }

            if (!manager.GenerateStreetDecor || (!forceStreetDecor && roadType != RoadType.Street))
            {
                return;
            }

            float halfWidth = RoadMeshExtruder.GetHalfWidth(roadType, roadSettings) * Mathf.Max(0.01f, widthMultiplier);
            DecorStyleContext styleCtx = BuildStyleContext(container, manager);

            if (includeSidewalks)
            {
                StreetSidewalkMeshBuilder.BuildSidewalkStrips(container, manager, halfWidth, styleCtx.Style);
            }

            List<RoadSegmentData> roadSegments = CollectRoadSegments(
                container.transform.parent, manager, roadSettings);

            if (styleCtx.Style.LightPostPrefab != null)
            {
                StreetLightPlacer.PlaceLightPosts(container, halfWidth, styleCtx.Style, roadSegments);
            }

            if (ResolvePropPrefabs(styleCtx.Style) is { Count: > 0 })
            {
                PlaceSidewalkProps(container, halfWidth, styleCtx, roadSegments);
            }

            if (IsBoulevard(container))
            {
                StreetSidewalkMeshBuilder.BuildBoulevardInteriorSidewalk(container, manager, styleCtx.Style);
                StreetLightPlacer.PlaceBoulevardInteriorLightPosts(container, styleCtx.Style);
                PlaceBoulevardInteriorProps(container, styleCtx);
            }
        }

        private static List<RoadSegmentData> CollectRoadSegments(Transform root, CityManager manager, RoadSettings roadSettings)
        {
            var result = new List<RoadSegmentData>(256);
            if (root == null || manager == null)
            {
                return result;
            }

            foreach (Transform child in root)
            {
                if (child == null || !(child.name == "RoadSpline_Street" || child.name == "RoadSpline_Boulevard"))
                {
                    continue;
                }

                var sc = child.GetComponent<SplineContainer>();
                if (sc == null || sc.Spline == null)
                {
                    continue;
                }

                float len = sc.Spline.GetLength();
                if (len <= 0f)
                {
                    continue;
                }

                float widthMul = ResolveStreetWidthMultiplierForSpline(sc, manager);
                float hw = RoadMeshExtruder.GetHalfWidth(RoadType.Street, roadSettings) * widthMul;
                int sampleCount = Mathf.Clamp(Mathf.CeilToInt(len / 30f), 6, 24);

                sc.Spline.Evaluate(0f, out var prevPos3, out _, out _);
                Vector3 prev = sc.transform.TransformPoint((Vector3)prevPos3);
                for (int i = 1; i < sampleCount; i++)
                {
                    float t = i / (float)(sampleCount - 1);
                    sc.Spline.Evaluate(t, out var pos3, out _, out _);
                    Vector3 cur = sc.transform.TransformPoint((Vector3)pos3);
                    if ((cur - prev).sqrMagnitude > 0.01f)
                    {
                        result.Add(new RoadSegmentData(prev, cur, hw));
                    }
                    prev = cur;
                }
            }

            return result;
        }

        public static void RebuildAllSidewalks(Transform root, CityManager manager, RoadSettings roadSettings)
        {
            if (root == null || manager == null || !manager.GenerateStreetDecor)
            {
                return;
            }

            var targets = new List<SplineContainer>();
            foreach (Transform child in root)
            {
                if (child == null)
                {
                    continue;
                }

                if (!(child.name == "RoadSpline_Street" || child.name == "RoadSpline_Boulevard"))
                {
                    continue;
                }

                var sc = child.GetComponent<SplineContainer>();
                if (sc == null || sc.Spline == null || sc.Spline.Count < 2)
                {
                    continue;
                }

                targets.Add(sc);
            }

            foreach (var sc in targets)
            {
                ClearExistingSidewalkChildren(sc.transform);
            }

            foreach (var sc in targets)
            {
                float widthMul = ResolveStreetWidthMultiplierForSpline(sc, manager);
                float halfWidth = RoadMeshExtruder.GetHalfWidth(RoadType.Street, roadSettings) * widthMul;
                DecorStyleContext styleCtx = BuildStyleContext(sc, manager);
                StreetSidewalkMeshBuilder.BuildSidewalkStrips(sc, manager, halfWidth, styleCtx.Style);
                if (IsBoulevard(sc))
                {
                    StreetSidewalkMeshBuilder.BuildBoulevardInteriorSidewalk(sc, manager, styleCtx.Style);
                }
            }

        }

        private static void ClearExistingSidewalkChildren(Transform t)
        {
            var toDelete = new List<GameObject>();
            foreach (Transform c in t)
            {
                if (c != null && c.name.StartsWith("StreetSidewalk", System.StringComparison.Ordinal))
                {
                    toDelete.Add(c.gameObject);
                }
            }

            foreach (var go in toDelete)
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
            }
        }

        private static void PlaceSidewalkProps(
            SplineContainer container,
            float halfWidth,
            DecorStyleContext styleCtx,
            List<RoadSegmentData> roadSegments)
        {
            List<GameObject> prefabs = ResolvePropPrefabs(styleCtx.Style);
            if (prefabs == null || prefabs.Count == 0)
            {
                return;
            }

            float length = container.Spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            float interval = styleCtx.Style.SidewalkPropInterval;
            // Center props on the sidewalk strip.
            float sidewalkCenterOffset = halfWidth + styleCtx.Style.SidewalkWidth * RoadGenerationOffsets.SidewalkCenterOffsetRatio;
            int seed = container.gameObject.GetInstanceID() ^ Mathf.RoundToInt(length * 10f);
            var rng = new System.Random(seed);
            float spawnChance = Mathf.Clamp01(styleCtx.Style.SidewalkPropSpawnChance);
            var candidates = new List<(Vector3 worldPos, Quaternion worldRot, GameObject prefab, Vector3 targetCenter, Vector3 lateralAxis)>();
            float propYOffset = styleCtx.Style.SidewalkVerticalOffset;

            for (float d = interval * RoadGenerationOffsets.PlacementFirstSampleRatio; d < length;)
            {
                if (rng.NextDouble() > spawnChance)
                {
                    d += GetJitteredIntervalStep(interval, rng);
                    continue;
                }

                float t = d / length;
                container.Spline.Evaluate(t, out var pos3, out var tan3, out var up3);
                Vector3 p = (Vector3)pos3 + (Vector3)up3 * propYOffset;
                Vector3 tangent = ((Vector3)tan3).normalized;
                Vector3 up = ((Vector3)up3).normalized;
                Vector3 right = Vector3.Cross(up, tangent).normalized;
                if (right.sqrMagnitude < 0.001f)
                {
                    right = Vector3.right;
                }

                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 localPos = p + right * side * sidewalkCenterOffset;

                int pick = rng.Next(prefabs.Count);
                GameObject prefab = prefabs[pick];
                if (prefab == null)
                {
                    d += GetJitteredIntervalStep(interval, rng);
                    continue;
                }

                Vector3 worldPos = container.transform.TransformPoint(localPos);
                Vector3 safeForward = GetSafeForward(tangent, Vector3.forward);
                Vector3 yawForward = Vector3.ProjectOnPlane(safeForward, Vector3.up).normalized;
                if (yawForward.sqrMagnitude < 1e-6f)
                {
                    yawForward = Vector3.forward;
                }
                float propPitch = GetPropPitchDegrees(prefab);
                Quaternion worldRot = Quaternion.LookRotation(yawForward, Vector3.up)
                    * Quaternion.Euler(propPitch, 0f, 0f);
                Vector3 lateralAxis = (right * side).normalized;
                candidates.Add((worldPos, worldRot, prefab, worldPos, lateralAxis));
                d += GetJitteredIntervalStep(interval, rng);
            }

            bool[] blocked = StreetRoadOverlapUtility.EvaluateRoadOverlap(
                candidates.ConvertAll(c => c.worldPos),
                roadSegments,
                runParallel: true);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (blocked[i])
                {
                    continue;
                }

                var c = candidates[i];
                Vector3 spawnPos = c.worldPos;
                if (!TryResolveNonRoadPosition(container.transform.parent, c.worldPos, c.lateralAxis, out spawnPos))
                {
                    continue;
                }
#if UNITY_EDITOR
                GameObject instance = Application.isPlaying
                    ? Object.Instantiate(c.prefab, spawnPos, c.worldRot, container.transform)
                    : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(c.prefab, container.transform);
                if (!Application.isPlaying && instance != null)
                {
                    instance.transform.SetPositionAndRotation(spawnPos, c.worldRot);
                }
#else
                GameObject instance = Object.Instantiate(c.prefab, spawnPos, c.worldRot, container.transform);
#endif
                if (instance != null)
                {
                    AlignPropToSidewalkCenter(instance, spawnPos, c.lateralAxis);
                    instance.name = $"StreetProp_{c.prefab.name}";
                }
            }
        }

        private static void AlignPropToSidewalkCenter(GameObject instance, Vector3 targetCenter, Vector3 lateralAxis)
        {
            if (instance == null || lateralAxis.sqrMagnitude < 1e-6f)
            {
                return;
            }

            if (!TryGetWorldBoundsCenter(instance, out Vector3 boundsCenter))
            {
                return;
            }

            Vector3 axis = lateralAxis.normalized;
            float lateralOffset = Vector3.Dot(boundsCenter - targetCenter, axis);
            if (Mathf.Abs(lateralOffset) < RoadGenerationOffsets.LateralCenterSnapEpsilon)
            {
                return;
            }

            instance.transform.position -= axis * lateralOffset;
        }

        private static bool TryGetWorldBoundsCenter(GameObject go, out Vector3 center)
        {
            center = Vector3.zero;
            bool hasBounds = false;
            Bounds merged = default;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    merged = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    merged.Encapsulate(r.bounds);
                }
            }

            if (!hasBounds)
            {
                Collider[] colliders = go.GetComponentsInChildren<Collider>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider c = colliders[i];
                    if (c == null)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        merged = c.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        merged.Encapsulate(c.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            center = merged.center;
            return true;
        }

        private static void PlaceBoulevardInteriorProps(
            SplineContainer container,
            DecorStyleContext styleCtx)
        {
            List<GameObject> prefabs = ResolveBoulevardInteriorPropPrefabs(styleCtx.Style);
            if (prefabs == null || prefabs.Count == 0 || container == null || container.Spline == null)
            {
                return;
            }

            float length = container.Spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            float interval = styleCtx.Style.BoulevardInteriorPropInterval;
            int seed = container.gameObject.GetInstanceID() ^ 0x2E11A;
            var rng = new System.Random(seed);
            float spawnChance = Mathf.Clamp01(styleCtx.Style.BoulevardInteriorPropSpawnChance);
            float propYOffset = styleCtx.Style.BoulevardInteriorSidewalkVerticalOffset
                + RoadGenerationOffsets.BoulevardInteriorDecorationYOffsetDelta;

            for (float d = interval * RoadGenerationOffsets.PlacementFirstSampleRatio; d < length;)
            {
                if (rng.NextDouble() > spawnChance)
                {
                    d += GetJitteredIntervalStep(interval, rng);
                    continue;
                }

                float t = d / length;
                container.Spline.Evaluate(t, out var pos3, out var tan3, out var up3);
                Vector3 p = (Vector3)pos3 + (Vector3)up3 * propYOffset;
                Vector3 tangent = ((Vector3)tan3).normalized;
                Vector3 localPos = p;
                int pick = rng.Next(prefabs.Count);
                GameObject prefab = prefabs[pick];
                if (prefab == null)
                {
                    d += GetJitteredIntervalStep(interval, rng);
                    continue;
                }

                Vector3 worldPos = container.transform.TransformPoint(localPos);
                Vector3 safeForward = GetSafeForward(tangent, Vector3.forward);
                Vector3 yawForward = Vector3.ProjectOnPlane(safeForward, Vector3.up).normalized;
                if (yawForward.sqrMagnitude < 1e-6f)
                {
                    yawForward = Vector3.forward;
                }
                float propPitch = GetPropPitchDegrees(prefab);
                Quaternion worldRot = Quaternion.LookRotation(yawForward, Vector3.up)
                    * Quaternion.Euler(propPitch, 0f, 0f);
                if (IsWorldPointOnRoundaboutOrStubSurface(container.transform.parent, worldPos))
                {
                    d += GetJitteredIntervalStep(interval, rng);
                    continue;
                }

#if UNITY_EDITOR
                GameObject instance = Application.isPlaying
                    ? Object.Instantiate(prefab, worldPos, worldRot, container.transform)
                    : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, container.transform);
                if (!Application.isPlaying && instance != null)
                {
                    instance.transform.SetPositionAndRotation(worldPos, worldRot);
                }
#else
                GameObject instance = Object.Instantiate(prefab, worldPos, worldRot, container.transform);
#endif
                if (instance != null)
                {
                    instance.name = $"StreetProp_BoulevardInterior_{prefab.name}";
                }

                d += GetJitteredIntervalStep(interval, rng);
            }
        }

        private static float GetJitteredIntervalStep(float baseInterval, System.Random rng)
        {
            if (rng == null)
            {
                return baseInterval;
            }

            float jitterRatio = Mathf.Clamp01(RoadGenerationOffsets.PropIntervalJitterRatio);
            float minStep = baseInterval * (1f - jitterRatio);
            float maxStep = baseInterval * (1f + jitterRatio);
            float t = (float)rng.NextDouble();
            return Mathf.Lerp(minStep, maxStep, t);
        }

        private static bool TryResolveNonRoadPosition(
            Transform root,
            Vector3 startPos,
            Vector3 lateralAxis,
            out Vector3 resolvedPos)
        {
            resolvedPos = startPos;
            if (!IsWorldPointOnRoundaboutOrStubSurface(root, startPos))
            {
                return true;
            }

            if (lateralAxis.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            Vector3 axis = lateralAxis.normalized;
            const int maxSteps = 8;
            const float stepSize = 0.35f;
            for (int i = 1; i <= maxSteps; i++)
            {
                Vector3 candidate = startPos + axis * (i * stepSize);
                if (!IsWorldPointOnRoundaboutOrStubSurface(root, candidate))
                {
                    resolvedPos = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when <paramref name="worldPos"/> lies on generated intersection or dead-end stub roundabout meshes
        /// (used to skip boulevard interior props on those surfaces).
        /// </summary>
        public static bool IsWorldPointOnRoundaboutOrStubSurface(Transform root, Vector3 worldPos)
        {
            if (root == null)
            {
                return false;
            }

            foreach (Transform child in root)
            {
                if (child == null)
                {
                    continue;
                }

                bool isRoadFamily =
                    child.name == "IntersectionRoundabout" ||
                    child.name == "RoadStubRoundabout";
                if (!isRoadFamily)
                {
                    continue;
                }

                var meshFilters = child.GetComponentsInChildren<MeshFilter>();
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    MeshFilter mf = meshFilters[i];
                    if (mf == null || mf.sharedMesh == null)
                    {
                        continue;
                    }

                    Transform t = mf.transform;
                    if (t == null)
                    {
                        continue;
                    }

                    Bounds wb = mf.GetComponent<Renderer>()?.bounds ?? new Bounds(t.position, Vector3.zero);
                    if (worldPos.y < wb.min.y - 1f || worldPos.y > wb.max.y + 2f)
                    {
                        continue;
                    }

                    Vector3 c = wb.center;
                    float r = Mathf.Max(wb.extents.x, wb.extents.z);
                    Vector2 p = new Vector2(worldPos.x, worldPos.z);
                    Vector2 cc = new Vector2(c.x, c.z);
                    if ((p - cc).sqrMagnitude <= (r + 0.15f) * (r + 0.15f))
                    {
                        return true;
                    }
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

        private static float GetPropPitchDegrees(GameObject prefab)
        {
            if (prefab == null)
            {
                return -90f;
            }

            string n = prefab.name ?? string.Empty;
            // Some vegetation assets are authored with different internal axis alignment.
            // Keep them upright while retaining lamp-post style for other props.
            if (n.Contains("Tree", System.StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Bush", System.StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Plant", System.StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Foliage", System.StringComparison.OrdinalIgnoreCase))
            {
                return 0f;
            }

            return -90f;
        }

        private static NeighborhoodStyleSample EvaluateStyleAtMid(SplineContainer container, CityManager manager)
        {
            if (container == null || container.Spline == null || manager == null)
            {
                return NeighborhoodStyleEvaluator.Evaluate(Vector3.zero, null);
            }

            container.Spline.Evaluate(0.5f, out var midPos, out _, out _);
            Vector3 midWorld = container.transform.TransformPoint((Vector3)midPos);
            return NeighborhoodStyleEvaluator.Evaluate(midWorld, manager.Nuclei);
        }

        /// <summary>
        /// Matches <see cref="SplineRoadGenerator.SpawnSplines"/>: boulevard uses city multiplier;
        /// street splines use blended <see cref="NeighborhoodStyleSample.RoadWidthMultiplier"/> at spline mid.
        /// </summary>
        private static float ResolveStreetWidthMultiplierForSpline(SplineContainer container, CityManager manager)
        {
            if (container == null || manager == null)
            {
                return 1f;
            }

            if (container.gameObject.name == "RoadSpline_Boulevard")
            {
                return Mathf.Max(0.01f, manager.BoulevardWidthMultiplier);
            }

            return Mathf.Max(0.01f, EvaluateStyleAtMid(container, manager).RoadWidthMultiplier);
        }

        private static DecorStyleContext BuildStyleContext(SplineContainer container, CityManager manager)
        {
            NeighborhoodStyleSample style = EvaluateStyleAtMid(container, manager);
            return new DecorStyleContext(style.ScaledBySidewalkRatio());
        }

        private static List<GameObject> ResolvePropPrefabs(NeighborhoodStyleSample style)
        {
            var profilePrefabs = style.SidewalkPropPrefabs;
            if (profilePrefabs != null && profilePrefabs.Count > 0)
            {
                return profilePrefabs;
            }

            return null;
        }

        private static List<GameObject> ResolveBoulevardInteriorPropPrefabs(NeighborhoodStyleSample style)
        {
            var profilePrefabs = style.BoulevardInteriorPropPrefabs;
            if (profilePrefabs != null && profilePrefabs.Count > 0)
            {
                return profilePrefabs;
            }

            return null;
        }

        private static bool IsBoulevard(SplineContainer container)
        {
            return container != null && container.gameObject.name == "RoadSpline_Boulevard";
        }

    }
}
