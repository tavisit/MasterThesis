using UnityEngine;

namespace Assets.Scripts.Runtime.Graph
{
    public static class RoadGraphKeyUtility
    {
        public static string ToEdgeKey(Vector3 a, Vector3 b)
        {
            string pa = ToPointKey(a);
            string pb = ToPointKey(b);
            return string.CompareOrdinal(pa, pb) <= 0 ? pa + "|" + pb : pb + "|" + pa;
        }

        public static string ToPointKey(Vector3 p)
        {
            int x = Mathf.RoundToInt(p.x * 10f);
            int y = Mathf.RoundToInt(p.y * 10f);
            int z = Mathf.RoundToInt(p.z * 10f);
            return x + "," + y + "," + z;
        }
    }
}
