using Assets.Scripts.Runtime.Graph;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.MeshRelated
{
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class RoadMeshExtruder : MonoBehaviour
    {
        [SerializeField] private RoadType _type = RoadType.Street;
        [SerializeField] private int _resolution = 1;
        [SerializeField] private Material _roadMaterial;

        public RoadType RoadType { get => _type; set => _type = value; }
        public int Resolution { get => _resolution; set => _resolution = value; }
        public Material RoadMaterial
        {
            get => _roadMaterial;
            set
            {
                _roadMaterial = value; if (_renderer)
                {
                    _renderer.sharedMaterial = value;
                }
            }
        }

        public static readonly float[] HalfWidths = { 4.0f, 7.0f, 2.5f }; // Street, Boulevard, Metro

        public static float GetHalfWidth(RoadType t) => HalfWidths[Mathf.Clamp((int)t, 0, HalfWidths.Length - 1)];
        private static readonly float[] _kerbHeights = { 0.15f, 0.2f, 0.25f };
        private static readonly float[] _kerbWidths = { 0.4f, 0.6f, 0.3f };

        private SplineContainer _container;
        private MeshFilter _filter;
        private MeshRenderer _renderer;

        private void Awake()
        {
            _container = GetComponent<SplineContainer>();
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            if (_roadMaterial != null)
            {
                _renderer.sharedMaterial = _roadMaterial;
            }
        }

        private void Start() => Rebuild();

        public void Rebuild()
        {
            if (_container == null || _container.Spline == null)
            {
                return;
            }

            var spline = _container.Spline;
            int idx = Mathf.Clamp((int)_type, 0, HalfWidths.Length - 1);
            float hw = HalfWidths[idx];
            float kerbH = _kerbHeights[idx];
            float kerbW = _kerbWidths[idx];
            float length = spline.GetLength();

            if (length <= 0f)
            {
                return;
            }

            int rings = Mathf.Max(2, Mathf.CeilToInt(length / _resolution));
            int vPerRing = 7;
            int quadsPerSeg = 6;

            var verts = new Vector3[rings * vPerRing];
            var uvs = new Vector2[rings * vPerRing];
            var tris = new int[(rings - 1) * quadsPerSeg * 6];

            for (int i = 0; i < rings; i++)
            {
                float t = (float)i / (rings - 1);
                spline.Evaluate(t, out var pos, out var tangent, out var upVec);

                Vector3 p = pos;
                Vector3 right = Vector3.Cross(
                    ((Vector3)tangent).normalized,
                    ((Vector3)upVec).normalized
                ).normalized;
                Vector3 up = ((Vector3)upVec).normalized * kerbH;

                int b = i * vPerRing;
                float v = t * length;

                verts[b + 0] = p - right * (hw + kerbW);
                verts[b + 1] = p - right * hw + up;
                verts[b + 2] = p - right * hw;
                verts[b + 3] = p;
                verts[b + 4] = p + right * hw;
                verts[b + 5] = p + right * hw + up;
                verts[b + 6] = p + right * (hw + kerbW);

                float[] us = { 0f, 0.1f, 0.15f, 0.5f, 0.85f, 0.9f, 1f };
                for (int j = 0; j < vPerRing; j++)
                {
                    uvs[b + j] = new Vector2(us[j], v);
                }
            }

            int ti = 0;
            for (int i = 0; i < rings - 1; i++)
            {
                int cur = i * vPerRing;
                int nxt = cur + vPerRing;

                for (int q = 0; q < quadsPerSeg; q++)
                {
                    tris[ti++] = cur + q;
                    tris[ti++] = nxt + q;
                    tris[ti++] = cur + q + 1;
                    tris[ti++] = cur + q + 1;
                    tris[ti++] = nxt + q;
                    tris[ti++] = nxt + q + 1;
                }
            }

            var mesh = new Mesh { name = $"RoadMesh_{_type}" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _filter.sharedMesh = mesh;
        }

#if UNITY_EDITOR
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += Rebuild;
#endif
    }
}
