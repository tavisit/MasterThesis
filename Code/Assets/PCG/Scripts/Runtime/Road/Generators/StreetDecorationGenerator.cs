using System.Collections.Generic;
using System.Threading.Tasks;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class StreetDecorationGenerator
    {
        private readonly struct RoadSegmentData
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly float HalfSpan;

            public RoadSegmentData(Vector3 a, Vector3 b, float halfSpan)
            {
                A = a;
                B = b;
                HalfSpan = halfSpan;
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
            float kerbWidth = roadSettings != null ? roadSettings.GetKerbWidth(roadType) : 0.4f;

            if (includeSidewalks && manager.GenerateSidewalks)
            {
                BuildSidewalkStrips(container, manager, halfWidth, kerbWidth);
            }

            List<RoadSegmentData> roadSegments = null;
            if (manager.AvoidRoadOverlapForDecor)
            {
                roadSegments = CollectRoadSegments(container.transform.parent, manager, roadSettings);
            }

            if (manager.GenerateLightPosts)
            {
                PlaceLightPosts(container, manager, halfWidth, kerbWidth, roadSegments);
            }

            if (manager.GenerateSidewalkProps)
            {
                PlaceSidewalkProps(container, manager, halfWidth, kerbWidth, roadSegments);
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

                float widthMul = child.name == "RoadSpline_Boulevard" ? manager.BoulevardWidthMultiplier : 1f;
                float hw = RoadMeshExtruder.GetHalfWidth(RoadType.Street, roadSettings) * Mathf.Max(0.01f, widthMul);
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
            if (root == null || manager == null || !manager.GenerateStreetDecor || !manager.GenerateSidewalks)
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
                float widthMultiplier = sc.gameObject.name == "RoadSpline_Boulevard" ? manager.BoulevardWidthMultiplier : 1f;
                float halfWidth = RoadMeshExtruder.GetHalfWidth(RoadType.Street, roadSettings) * Mathf.Max(0.01f, widthMultiplier);
                float kerbWidth = roadSettings != null ? roadSettings.GetKerbWidth(RoadType.Street) : 0.4f;
                BuildSidewalkStrips(sc, manager, halfWidth, kerbWidth);
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

        private static void BuildSidewalkStrips(
            SplineContainer container,
            CityManager manager,
            float halfWidth,
            float kerbWidth)
        {
            float length = container.Spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            int rings = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(1f, manager.MeshResolution * 0.75f)));
            float width = Mathf.Max(0.2f, manager.SidewalkWidth);
            float yOffset = manager.SidewalkMeshVerticalOffset;

            var vertices = new Vector3[rings * 4];
            var uvs = new Vector2[rings * 4];
            var triangles = new int[(rings - 1) * 4 * 3];

            for (int i = 0; i < rings; i++)
            {
                float t = (float)i / (rings - 1);
                container.Spline.Evaluate(t, out var pos3, out var tan3, out var up3);

                Vector3 p = (Vector3)pos3 + (Vector3)up3 * yOffset;
                Vector3 tangent = ((Vector3)tan3).normalized;
                Vector3 up = ((Vector3)up3).normalized;
                Vector3 right = Vector3.Cross(up, tangent).normalized;
                if (right.sqrMagnitude < 0.001f)
                {
                    right = Vector3.right;
                }

                float inner = halfWidth + kerbWidth;
                float outer = inner + width;
                int b = i * 4;

                vertices[b + 0] = p - right * inner;
                vertices[b + 1] = p - right * outer;
                vertices[b + 2] = p + right * inner;
                vertices[b + 3] = p + right * outer;

                float v = t * length;
                uvs[b + 0] = new Vector2(0f, v);
                uvs[b + 1] = new Vector2(1f, v);
                uvs[b + 2] = new Vector2(0f, v);
                uvs[b + 3] = new Vector2(1f, v);
            }

            int ti = 0;
            for (int i = 0; i < rings - 1; i++)
            {
                int cur = i * 4;
                int nxt = cur + 4;
                triangles[ti++] = cur + 0; triangles[ti++] = nxt + 0; triangles[ti++] = cur + 1;
                triangles[ti++] = cur + 1; triangles[ti++] = nxt + 0; triangles[ti++] = nxt + 1;
                triangles[ti++] = cur + 2; triangles[ti++] = cur + 3; triangles[ti++] = nxt + 2;
                triangles[ti++] = cur + 3; triangles[ti++] = nxt + 3; triangles[ti++] = nxt + 2;
            }

            var mesh = new Mesh { name = "SidewalkStrip" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("StreetSidewalk");
            go.transform.SetParent(container.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = manager.SidewalkMaterial != null ? manager.SidewalkMaterial : manager.StreetMaterial;
        }

        private static void PlaceLightPosts(
            SplineContainer container,
            CityManager manager,
            float halfWidth,
            float kerbWidth,
            List<RoadSegmentData> roadSegments)
        {
            float length = container.Spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            float interval = Mathf.Max(8f, manager.LightPostInterval);
            float sidewalkWidth = Mathf.Max(0.2f, manager.SidewalkWidth);
            float offset = halfWidth + kerbWidth + sidewalkWidth * 0.5f;
            int index = 0;
            var candidates = new List<(Vector3 localPos, Vector3 tangent, Vector3 up, Vector3 worldPos)>();

            for (float d = interval * 0.5f; d < length; d += interval)
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

                float side = index % 2 == 0 ? -1f : 1f;
                Vector3 basePos = p + right * side * offset;
                Vector3 safeForward = GetSafeForward(tangent, right * -side);
                candidates.Add((basePos, safeForward, up, container.transform.TransformPoint(basePos)));
                index++;
            }

            bool[] blocked = EvaluateRoadOverlap(
                candidates.ConvertAll(c => c.worldPos),
                roadSegments,
                manager.ParallelizeDecorChecks);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (blocked[i])
                {
                    continue;
                }

                var c = candidates[i];
                SpawnLampPost(container.transform, c.localPos, c.tangent, c.up);
            }
        }

        private static void SpawnLampPost(
            Transform parent,
            Vector3 basePos,
            Vector3 tangent,
            Vector3 up)
        {
            var manager = parent.GetComponentInParent<CityManager>();
            GameObject prefab = manager != null ? manager.LightPostPrefab : null;
            if (prefab != null)
            {
                SpawnLightPostPrefab(parent, basePos, tangent, up, prefab);
                return;
            }

            const float fallbackHeight = 5.5f;
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "StreetLightPole";
            pole.transform.SetParent(parent, false);
            pole.transform.position = basePos + up * (fallbackHeight * 0.5f);
            pole.transform.rotation = Quaternion.LookRotation(GetSafeForward(tangent, Vector3.forward), up);
            pole.transform.localScale = new Vector3(0.12f, fallbackHeight * 0.5f, 0.12f);
            var collider = pole.GetComponent<Collider>();
            if (collider != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(collider);
                }
                else
#endif
                {
                    Object.Destroy(collider);
                }
            }

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "StreetLightHead";
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = basePos + up * (fallbackHeight + 0.2f);
            lamp.transform.localScale = Vector3.one * 0.28f;
            var lampCollider = lamp.GetComponent<Collider>();
            if (lampCollider != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(lampCollider);
                }
                else
#endif
                {
                    Object.Destroy(lampCollider);
                }
            }
        }

        private static void SpawnLightPostPrefab(
            Transform parent,
            Vector3 basePos,
            Vector3 tangent,
            Vector3 up,
            GameObject prefab)
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
            instance.transform.SetPositionAndRotation(
                basePos,
                Quaternion.LookRotation(safeForward, up));
        }

        private static void PlaceSidewalkProps(
            SplineContainer container,
            CityManager manager,
            float halfWidth,
            float kerbWidth,
            List<RoadSegmentData> roadSegments)
        {
            var prefabs = manager.SidewalkPropPrefabs;
            if (prefabs == null || prefabs.Count == 0)
            {
                return;
            }

            float length = container.Spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            float interval = Mathf.Max(10f, manager.SidewalkPropInterval);
            float spawnChance = Mathf.Clamp01(manager.SidewalkPropSpawnChance);
            float sideOffset = halfWidth + kerbWidth + Mathf.Max(0.2f, manager.SidewalkWidth) * 0.6f;
            int seed = container.gameObject.GetInstanceID() ^ Mathf.RoundToInt(length * 10f);
            var rng = new System.Random(seed);
            var candidates = new List<(Vector3 worldPos, Quaternion worldRot, GameObject prefab)>();

            for (float d = interval * 0.5f; d < length; d += interval)
            {
                if (rng.NextDouble() > spawnChance)
                {
                    continue;
                }

                float t = d / length;
                container.Spline.Evaluate(t, out var pos3, out var tan3, out var up3);
                Vector3 p = (Vector3)pos3 + (Vector3)up3 * manager.SidewalkVerticalOffset;
                Vector3 tangent = ((Vector3)tan3).normalized;
                Vector3 up = ((Vector3)up3).normalized;
                Vector3 right = Vector3.Cross(up, tangent).normalized;
                if (right.sqrMagnitude < 0.001f)
                {
                    right = Vector3.right;
                }

                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 localPos = p + right * side * sideOffset;

                int pick = rng.Next(prefabs.Count);
                GameObject prefab = prefabs[pick];
                if (prefab == null)
                {
                    continue;
                }

                Vector3 worldPos = container.transform.TransformPoint(localPos);
                Vector3 safeForward = GetSafeForward(tangent * -side, right);
                // Prefabs expect forward along sidewalk;
                Quaternion worldRot = Quaternion.LookRotation(safeForward, Vector3.up)
                    * Quaternion.Euler(0f, -90f, 0f);
                candidates.Add((worldPos, worldRot, prefab));
            }

            bool[] blocked = EvaluateRoadOverlap(
                candidates.ConvertAll(c => c.worldPos),
                roadSegments,
                manager.ParallelizeDecorChecks);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (blocked[i])
                {
                    continue;
                }

                var c = candidates[i];
#if UNITY_EDITOR
                GameObject instance = Application.isPlaying
                    ? Object.Instantiate(c.prefab, c.worldPos, c.worldRot, container.transform)
                    : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(c.prefab, container.transform);
                if (!Application.isPlaying && instance != null)
                {
                    instance.transform.SetPositionAndRotation(c.worldPos, c.worldRot);
                }
#else
                GameObject instance = Object.Instantiate(c.prefab, c.worldPos, c.worldRot, container.transform);
#endif
                if (instance != null)
                {
                    instance.name = $"StreetProp_{c.prefab.name}";
                }
            }
        }

        private static bool[] EvaluateRoadOverlap(
            List<Vector3> candidates,
            List<RoadSegmentData> roadSegments,
            bool runParallel)
        {
            var blocked = new bool[candidates?.Count ?? 0];
            if (candidates == null || candidates.Count == 0 ||
                roadSegments == null || roadSegments.Count == 0)
            {
                return blocked;
            }

            void EvalAt(int i)
            {
                blocked[i] = IsPointOverAnyRoadSegment(candidates[i], roadSegments);
            }

            if (runParallel && candidates.Count >= 32)
            {
                Parallel.For(0, candidates.Count, EvalAt);
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    EvalAt(i);
                }
            }

            return blocked;
        }

        private static bool IsPointOverAnyRoadSegment(Vector3 p, List<RoadSegmentData> segments)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (IsPointOverRoadSegment(p, segments[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointOverRoadSegment(Vector3 p, RoadSegmentData seg)
        {
            Vector2 a = new Vector2(seg.A.x, seg.A.z);
            Vector2 b = new Vector2(seg.B.x, seg.B.z);
            Vector2 q = new Vector2(p.x, p.z);
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq <= 1e-6f)
            {
                return false;
            }

            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / lenSq);
            Vector2 nearest = a + ab * t;
            float xzDist = Vector2.Distance(q, nearest);
            if (xzDist > seg.HalfSpan)
            {
                return false;
            }

            float yOnSeg = Mathf.Lerp(seg.A.y, seg.B.y, t);
            return Mathf.Abs(p.y - yOnSeg) <= 1.0f;
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
