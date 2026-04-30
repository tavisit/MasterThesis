using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    internal sealed class MetroEntranceLocator
    {
        private readonly CityManager _manager;
        private readonly Transform _root;
        private readonly RoadSettings _roadSettings;
        private readonly HashSet<Vector2Int> _placedEntranceCells;

        internal MetroEntranceLocator(
            CityManager manager,
            Transform root,
            RoadSettings roadSettings,
            HashSet<Vector2Int> placedEntranceCells)
        {
            _manager = manager;
            _root = root;
            _roadSettings = roadSettings;
            _placedEntranceCells = placedEntranceCells;
        }

        internal bool IsPointUnderStreet(Vector3 worldPoint)
        {
            if (!TryGetNearestStreetInfo(worldPoint, out float distanceToCenter, out float halfWidth, out _))
            {
                return false;
            }

            return distanceToCenter <= halfWidth + RoadGenerationOffsets.StreetUnderPadding;
        }

        internal bool TryFindValidSidewalkSpot(
            Vector3 railPos,
            Vector3 right,
            float sideSign,
            float initialOffset,
            float sidewalkWidth,
            out Vector3 spot)
        {
            spot = default;
            float step = Mathf.Max(
                RoadGenerationOffsets.MetroEntranceProbeMinStep,
                sidewalkWidth * RoadGenerationOffsets.MetroEntranceProbeStepSidewalkFactor);
            float maxProbe = Mathf.Max(
                RoadGenerationOffsets.MetroEntranceProbeMinDistance,
                sidewalkWidth * RoadGenerationOffsets.MetroEntranceProbeMaxDistanceSidewalkFactor);
            int tries = Mathf.Max(RoadGenerationOffsets.MetroEntranceProbeMinTries, Mathf.CeilToInt(maxProbe / step));
            float sidewalkTolerance = Mathf.Max(
                RoadGenerationOffsets.MetroEntranceSidewalkToleranceMin,
                sidewalkWidth * RoadGenerationOffsets.MetroEntranceSidewalkToleranceFactor);

            for (int i = 0; i < tries; i++)
            {
                float offset = initialOffset + i * step;
                Vector3 candidate = railPos + right * (sideSign * offset);
                float groundY = _manager.TerrainAdapter != null
                    ? _manager.TerrainAdapter.SampleHeight(candidate.x, candidate.z)
                    : candidate.y;
                candidate.y = groundY;

                if (!IsPointOnAnySidewalk(candidate, sidewalkTolerance))
                {
                    continue;
                }

                if (!TryGetNearestStreetInfo(candidate, out float distToStreetCenter, out float streetHalfWidth, out _))
                {
                    continue;
                }

                float minSidewalkOffset = streetHalfWidth + Mathf.Max(
                    RoadGenerationOffsets.MetroEntranceMinSidewalkInset,
                    sidewalkWidth * RoadGenerationOffsets.MetroEntranceMinSidewalkInsetFactor);
                float maxSidewalkOffset = streetHalfWidth + Mathf.Max(
                    RoadGenerationOffsets.MetroEntranceMaxSidewalkInset,
                    sidewalkWidth * RoadGenerationOffsets.MetroEntranceMaxSidewalkInsetFactor);
                if (distToStreetCenter < minSidewalkOffset || distToStreetCenter > maxSidewalkOffset)
                {
                    continue;
                }

                if (IsPointInsideStreetCorridor(candidate))
                {
                    continue;
                }

                if (IsEntranceSlotTaken(candidate))
                {
                    continue;
                }

                if (IsEntranceAreaOccupied(candidate))
                {
                    continue;
                }

                spot = candidate;
                ReserveEntranceSlot(candidate);
                return true;
            }

            return false;
        }

        internal bool TryGetNearestStreetTangent(Vector3 worldPoint, out Vector3 tangent)
        {
            if (TryGetNearestStreetInfo(worldPoint, out _, out _, out tangent))
            {
                return true;
            }

            tangent = Vector3.forward;
            return false;
        }

        private bool TryGetNearestStreetInfo(
            Vector3 worldPoint,
            out float nearestDistanceToCenter,
            out float nearestStreetHalfWidth,
            out Vector3 nearestStreetTangent)
        {
            nearestDistanceToCenter = float.MaxValue;
            nearestStreetHalfWidth = 0f;
            nearestStreetTangent = Vector3.forward;
            if (_root == null)
            {
                return false;
            }

            bool found = false;
            foreach (Transform child in _root)
            {
                if (child == null ||
                    !(child.name == "RoadSpline_Street" || child.name == "RoadSpline_Boulevard"))
                {
                    continue;
                }

                var sc = child.GetComponent<SplineContainer>();
                if (sc == null || sc.Spline == null)
                {
                    continue;
                }

                float length = sc.Spline.GetLength();
                if (length <= 0.1f)
                {
                    continue;
                }

                float widthMultiplier = child.name == "RoadSpline_Boulevard"
                    ? Mathf.Max(0.01f, _manager.BoulevardWidthMultiplier)
                    : Mathf.Max(0.01f, NeighborhoodStyleEvaluator.Evaluate(worldPoint, _manager.Nuclei).RoadWidthMultiplier);
                float halfWidth = RoadMeshExtruder.GetHalfWidth(RoadType.Street, _roadSettings) * widthMultiplier;

                int samples = Mathf.Clamp(Mathf.CeilToInt(length / 4f), 16, 96);
                sc.Spline.Evaluate(0f, out var prevPos3, out _, out _);
                Vector3 prev = sc.transform.TransformPoint((Vector3)prevPos3);
                for (int i = 1; i < samples; i++)
                {
                    float t = i / (float)(samples - 1);
                    sc.Spline.Evaluate(t, out var pos3, out _, out _);
                    Vector3 cur = sc.transform.TransformPoint((Vector3)pos3);

                    float dist = DistancePointToSegmentXZ(worldPoint, prev, cur);
                    if (dist < nearestDistanceToCenter)
                    {
                        Vector3 seg = cur - prev;
                        seg.y = 0f;
                        if (seg.sqrMagnitude > 1e-6f)
                        {
                            nearestDistanceToCenter = dist;
                            nearestStreetHalfWidth = halfWidth;
                            nearestStreetTangent = seg.normalized;
                            found = true;
                        }
                    }

                    prev = cur;
                }
            }

            return found;
        }

        private bool IsPointInsideStreetCorridor(Vector3 worldPoint)
        {
            if (!TryGetNearestStreetInfo(worldPoint, out float distanceToCenter, out float halfWidth, out _))
            {
                return false;
            }

            return distanceToCenter <= halfWidth + RoadGenerationOffsets.StreetCorridorPadding;
        }

        private static float DistancePointToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 ap = new Vector2(p.x - a.x, p.z - a.z);
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f)
            {
                return ap.magnitude;
            }

            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / lenSq);
            Vector2 q = new Vector2(a.x, a.z) + ab * t;
            return Vector2.Distance(new Vector2(p.x, p.z), q);
        }

        private bool IsEntranceSlotTaken(Vector3 worldPos)
        {
            Vector2Int key = new Vector2Int(
                Mathf.RoundToInt(worldPos.x / RoadGenerationOffsets.EntranceSlotGridSize),
                Mathf.RoundToInt(worldPos.z / RoadGenerationOffsets.EntranceSlotGridSize));
            return _placedEntranceCells.Contains(key);
        }

        private void ReserveEntranceSlot(Vector3 worldPos)
        {
            Vector2Int key = new Vector2Int(
                Mathf.RoundToInt(worldPos.x / RoadGenerationOffsets.EntranceSlotGridSize),
                Mathf.RoundToInt(worldPos.z / RoadGenerationOffsets.EntranceSlotGridSize));
            _placedEntranceCells.Add(key);
        }

        private bool IsPointOnAnySidewalk(Vector3 worldPoint, float toleranceXZ)
        {
            if (_root == null)
            {
                return false;
            }

            foreach (Transform child in _root)
            {
                if (child == null ||
                    !(child.name == "RoadSpline_Street" || child.name == "RoadSpline_Boulevard"))
                {
                    continue;
                }

                Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || r.gameObject == null)
                    {
                        continue;
                    }

                    string n = r.gameObject.name;
                    if (!(n == "StreetSidewalk" || n == "StreetSidewalk_BoulevardInterior"))
                    {
                        continue;
                    }

                    Bounds b = r.bounds;
                    float cx = Mathf.Clamp(worldPoint.x, b.min.x, b.max.x);
                    float cz = Mathf.Clamp(worldPoint.z, b.min.z, b.max.z);
                    float dx = worldPoint.x - cx;
                    float dz = worldPoint.z - cz;
                    float distXZ = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distXZ > toleranceXZ)
                    {
                        continue;
                    }

                    if (worldPoint.y < b.min.y + RoadGenerationOffsets.SidewalkPointMinYDelta ||
                        worldPoint.y > b.max.y + RoadGenerationOffsets.SidewalkPointMaxYDelta)
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        private bool IsEntranceAreaOccupied(Vector3 worldPos)
        {
            const float radius = RoadGenerationOffsets.EntranceOccupancyRadius;
            const float halfHeight = RoadGenerationOffsets.EntranceOccupancyHalfHeight;
            Vector3 boxCenter = worldPos + Vector3.up * halfHeight;
            Vector3 halfExtents = new Vector3(radius, halfHeight, radius);

            Collider[] colliders = Physics.OverlapBox(
                boxCenter,
                halfExtents,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || c.transform == null)
                {
                    continue;
                }

                if (!IsBlockingObjectName(c.transform.gameObject.name))
                {
                    continue;
                }

                return true;
            }

            if (_root == null)
            {
                return false;
            }

            Bounds probe = new Bounds(boxCenter, halfExtents * 2f);
            foreach (Transform child in _root)
            {
                if (child == null)
                {
                    continue;
                }

                Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || r.gameObject == null)
                    {
                        continue;
                    }

                    if (!IsBlockingObjectName(r.gameObject.name))
                    {
                        continue;
                    }

                    if (probe.Intersects(r.bounds))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsBlockingObjectName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return
                name.StartsWith("StreetProp_", System.StringComparison.Ordinal) ||
                name.StartsWith("StreetLight_", System.StringComparison.Ordinal) ||
                name.StartsWith("MetroEntrance_", System.StringComparison.Ordinal) ||
                name.StartsWith("MetroStation_Terminal", System.StringComparison.Ordinal) ||
                name.StartsWith("MetroStation", System.StringComparison.Ordinal);
        }
    }
}
