using UnityEngine;

namespace Assets.Scripts.Runtime.Road.Generators
{
    internal readonly struct RoadSegmentData
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
}
