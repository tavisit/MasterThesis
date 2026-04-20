using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.MeshRelated
{
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteAlways]
    public sealed class RoadMeshExtruder : MonoBehaviour
    {
        [SerializeField] private RoadType _type = RoadType.Street;
        [SerializeField] private int _resolution = 1;
        [SerializeField] private Material _roadMaterial;
        [SerializeField] private RoadSettings _roadSettings;
        [SerializeField] private float _widthMultiplier = 1f;
        [SerializeField] private float _meshVerticalOffset = 0f;
        [SerializeField] private int _laneCount = 2;

        public RoadType RoadType { get => _type; set => _type = value; }
        public float WidthMultiplier { get => _widthMultiplier; set => _widthMultiplier = value; }
        public float MeshVerticalOffset { get => _meshVerticalOffset; set => _meshVerticalOffset = value; }
        public int LaneCount { get => _laneCount; set => _laneCount = Mathf.Max(1, value); }
        public int Resolution { get => _resolution; set => _resolution = value; }
        public RoadSettings RoadSettings
        {
            get => _roadSettings;
            set => _roadSettings = value;
        }
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

        public static float GetHalfWidth(RoadType t, RoadSettings settings)
        {
            if (settings != null)
            {
                return settings.GetHalfWidth(t);
            }

            return 4.0f;
        }

        private SplineContainer _container;
        private MeshFilter _filter;
        private MeshRenderer _renderer;

        private void Awake()
        {
            _container = GetComponent<SplineContainer>();
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();

            if (_roadSettings == null)
            {
                var cityManager = FindFirstObjectByType<CityManager>();
                if (cityManager != null)
                {
                    _roadSettings = cityManager.RoadSettings;
                }
            }

            if (_roadMaterial != null)
            {
                _renderer.sharedMaterial = _roadMaterial;
            }
        }

        private void Start() => Rebuild();

        public void Rebuild()
        {
            if (_roadSettings == null)
            {
                _roadSettings = FindFirstObjectByType<CityManager>()?.RoadSettings;
            }

            if (_roadSettings == null)
            {
                Debug.LogError($"[RoadMeshExtruder] RoadSettings not assigned on " +
                               $"{gameObject.name}. Aborting rebuild.");
                return;
            }

            if (_container == null || _container.Spline == null)
            {
                return;
            }

            var spline = _container.Spline;
            float hw = _roadSettings.GetHalfWidth(_type) * Mathf.Max(0.01f, _widthMultiplier);
            float kerbH = _roadSettings.GetKerbHeight(_type);
            float length = spline.GetLength();

            if (length <= 0f)
            {
                return;
            }

            int rings = Mathf.Max(2, Mathf.CeilToInt(length / _resolution));
            int vPerRing = 3;
            int quadsPerSeg = 2;

            var verts = new Vector3[rings * vPerRing];
            var uvs = new Vector2[rings * vPerRing];
            var tris = new int[(rings - 1) * quadsPerSeg * 6];
            Vector3 lastTang = Vector3.forward;
            Vector3 lastUp = Vector3.up;

            for (int i = 0; i < rings; i++)
            {
                float t = (float)i / (rings - 1);
                spline.Evaluate(t, out var pos, out var tangent, out var upVec);

                Vector3 p = (Vector3)pos + (Vector3)upVec * _meshVerticalOffset;
                Vector3 rawTang = (Vector3)tangent;
                Vector3 rawUp = (Vector3)upVec;
                if (!IsFinite(p))
                {
                    Debug.LogWarning($"[RoadMeshExtruder] Invalid spline sample on {gameObject.name}. Rebuild skipped.");
                    return;
                }

                Vector3 tang = rawTang.sqrMagnitude > 1e-6f && IsFinite(rawTang)
                    ? rawTang.normalized
                    : lastTang;
                Vector3 up = rawUp.sqrMagnitude > 1e-6f && IsFinite(rawUp)
                    ? rawUp.normalized
                    : lastUp;
                if (Vector3.Dot(up, tang) > 0.995f || Vector3.Dot(up, tang) < -0.995f)
                {
                    up = lastUp;
                }
                Vector3 right = Vector3.Cross(up, tang).normalized;

                if (right.sqrMagnitude < 0.01f)
                {
                    Vector3 worldRight = Vector3.right;
                    if (Mathf.Abs(Vector3.Dot(tang, worldRight)) > 0.99f)
                    {
                        worldRight = Vector3.forward;
                    }

                    right = Vector3.Cross(up, worldRight).normalized;
                }
                if (!IsFinite(right) || right.sqrMagnitude < 1e-6f)
                {
                    right = Vector3.right;
                }

                lastTang = tang;
                lastUp = up;

                int b = i * vPerRing;
                float v = t * length;
                float camber = kerbH * 0.35f;

                verts[b + 0] = p - right * hw;
                verts[b + 1] = p + up * camber;
                verts[b + 2] = p + right * hw;
                uvs[b + 0] = new Vector2(0f, v);
                uvs[b + 1] = new Vector2(0.5f, v);
                uvs[b + 2] = new Vector2(1f, v);
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
            if (!IsFinite(mesh.bounds.min) || !IsFinite(mesh.bounds.max))
            {
                Debug.LogWarning($"[RoadMeshExtruder] Skipped invalid mesh bounds on {gameObject.name}.");
                return;
            }
            _filter.sharedMesh = mesh;
            ApplyLaneShaderProperties();
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                   !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
                   !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }

        private void ApplyLaneShaderProperties()
        {
            if (_renderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);
            block.SetFloat("_LaneCount", Mathf.Max(1, _laneCount));
            _renderer.SetPropertyBlock(block);
        }

#if UNITY_EDITOR
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += Rebuild;
#endif
    }
}
