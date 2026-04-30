using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

namespace Assets.Scripts.Runtime.Road.Generators
{
    internal static class StreetRoadOverlapUtility
    {
        internal static bool[] EvaluateRoadOverlap(
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
    }
}
