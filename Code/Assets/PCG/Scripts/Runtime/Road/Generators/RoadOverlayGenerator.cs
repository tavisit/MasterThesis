using System.Collections.Generic;

using Assets.Scripts.Runtime.Adapters;
using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;

using UnityEngine;
using UnityEngine.Splines;

public sealed class RoadOverlayGenerator : MonoBehaviour
{
    [Header("Tunnel")]
    [SerializeField] private bool _generateTunnels = true;
    [SerializeField] private float _tunnelHeightThreshold = 3f;
    [SerializeField] private Material _tunnelMaterial;
    [SerializeField] private float _tunnelSampleInterval = 5f;

    [Header("Tunnel arch shape")]
    [SerializeField] private int _archSegments = 16;
    [SerializeField] private float _archThickness = 0.6f;
    [SerializeField] private float _archWidthMultiplier = 1.35f;
    [SerializeField] private float _metroArchWidthMultiplier = 1.8f;
    [SerializeField] private float _boulevardArchWidthMultiplier = 1.1f;
    [SerializeField] private float _tunnelEntryExtension = 10f;

    [Header("Bridge")]
    [SerializeField] private bool _generateBridges = true;
    [SerializeField] private float _bridgeHeightThreshold = 1f;
    [SerializeField] private Material _pillarMaterial;
    [SerializeField] private float _pillarInterval = 20f;

    [Header("Bridge pillar shape")]
    [SerializeField] private int _pillarSides = 6;
    [SerializeField] private float _pillarRadius = 0.5f;

    [SerializeField] private TerrainAdapter _terrain;
    [SerializeField] private RoadSettings _roadSettings;
    private readonly HashSet<Vector2Int> _placedArchCells = new();
    private CityManager _cityManager;

    private void Awake()
    {
        _cityManager = GetComponent<CityManager>() ?? FindFirstObjectByType<CityManager>();
    }

    public void ResetPlacedCells() => _placedArchCells.Clear();

    public void ProcessSpline(SplineContainer container, RoadType roadType)
    {
        var spline = container.Spline;
        float length = spline.GetLength();
        if (length <= 0f)
        {
            return;
        }

        float hw = RoadMeshExtruder.GetHalfWidth(roadType, _roadSettings);
        if (roadType == RoadType.Street && container != null && container.gameObject.name == "RoadSpline_Boulevard")
        {
            float boulevardWidth = _cityManager != null ? _cityManager.BoulevardWidthMultiplier : 1f;
            hw *= Mathf.Max(1f, boulevardWidth * _boulevardArchWidthMultiplier);
        }
        else if (roadType == RoadType.Metro)
        {
            hw *= Mathf.Max(1f, _metroArchWidthMultiplier);
        }

        if (_generateTunnels)
        {
            PlaceTunnelArches(container, spline, length, hw * Mathf.Max(1f, _archWidthMultiplier));
        }

        if (_generateBridges)
        {
            PlaceBridgePillars(container, spline, length, hw);
        }
    }

    private void PlaceTunnelArches(SplineContainer container, Spline spline, float length, float hw)
    {
        int samples = Mathf.Max(4, Mathf.CeilToInt(length / _tunnelSampleInterval));

        var positions = new Vector3[samples];
        var tangents = new Vector3[samples];
        var inTunnel = new bool[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            spline.Evaluate(t, out var pos3, out var tan3, out _);
            positions[i] = pos3;
            tangents[i] = ((Vector3)tan3).normalized;

            float terrainH = _terrain != null
                ? _terrain.SampleHeight(positions[i].x, positions[i].z)
                : positions[i].y;

            inTunnel[i] = terrainH - positions[i].y > _tunnelHeightThreshold;
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 1; i < samples - 1; i++)
            {
                if (!inTunnel[i] && inTunnel[i - 1] && inTunnel[i + 1])
                {
                    inTunnel[i] = true;
                    changed = true;
                }
            }
        }
        var entryPoints = new List<int>();
        var exitPoints = new List<int>();

        for (int i = 0; i < samples; i++)
        {
            bool prev = i > 0 && inTunnel[i - 1];
            bool curr = inTunnel[i];
            bool next = i < samples - 1 && inTunnel[i + 1];

            if (curr && !prev)
            {
                entryPoints.Add(i);
            }

            if (curr && !next)
            {
                exitPoints.Add(i);
            }
        }

        foreach (int entry in entryPoints)
        {
            float rem = _tunnelEntryExtension;
            for (int k = entry - 1; k >= 0 && rem > 0f; k--)
            {
                inTunnel[k] = true;
                rem -= k > 0 ? Vector3.Distance(positions[k - 1], positions[k]) : 0f;
            }
        }

        foreach (int exit in exitPoints)
        {
            float rem = _tunnelEntryExtension;
            for (int k = exit + 1; k < samples && rem > 0f; k++)
            {
                inTunnel[k] = true;
                rem -= k < samples - 1 ? Vector3.Distance(positions[k], positions[k + 1]) : 0f;
            }
        }

        for (int i = 0; i < samples; i++)
        {
            if (!inTunnel[i])
            {
                continue;
            }

            var cell = new Vector2Int(
                Mathf.RoundToInt(positions[i].x / _tunnelSampleInterval),
                Mathf.RoundToInt(positions[i].z / _tunnelSampleInterval));

            if (_placedArchCells.Contains(cell))
            {
                continue;
            }

            _placedArchCells.Add(cell);

            float prevDist = i > 0
                ? Vector3.Distance(positions[i - 1], positions[i])
                : Vector3.Distance(positions[i], positions[Mathf.Min(i + 1, samples - 1)]);
            float nextDist = i < samples - 1
                ? Vector3.Distance(positions[i], positions[i + 1])
                : prevDist;
            float depth = (prevDist + nextDist) * 0.5f;

            float thickness = Mathf.Max(_archThickness, hw * 0.22f);
            Mesh archMesh = BuildArchMesh(hw, _archSegments, thickness, depth);

            var go = new GameObject("TunnelArch");
            go.transform.SetParent(container.transform, false);
            go.transform.position = positions[i];
            go.transform.rotation = Quaternion.LookRotation(tangents[i], Vector3.up);

            go.AddComponent<MeshFilter>().sharedMesh = archMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _tunnelMaterial;
        }
    }

    private Mesh BuildArchMesh(float hw, int segments, float thickness, float depth)
    {
        float innerR = Mathf.Max(0.1f, hw - thickness * 0.5f);
        float outerR = hw + thickness * 0.5f;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        for (int face = 0; face < 2; face++)
        {
            float z = face == 0 ? 0f : depth;
            for (int s = 0; s <= segments; s++)
            {
                float angle = Mathf.PI * s / segments;
                float cx = Mathf.Cos(angle);
                float cy = Mathf.Sin(angle);

                verts.Add(new Vector3(cx * innerR, cy * innerR, z));
                verts.Add(new Vector3(cx * outerR, cy * outerR, z));
                uvs.Add(new Vector2((float)s / segments, face == 0 ? 0f : 1f));
                uvs.Add(new Vector2((float)s / segments, face == 0 ? 0f : 1f));
            }
        }

        int stride = (segments + 1) * 2;

        for (int s = 0; s < segments; s++)
        {
            int i0 = s * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
            tris.Add(i0); tris.Add(i2); tris.Add(i1);
            tris.Add(i1); tris.Add(i2); tris.Add(i3);
        }

        for (int s = 0; s < segments; s++)
        {
            int i0 = stride + s * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
            tris.Add(i0); tris.Add(i1); tris.Add(i2);
            tris.Add(i1); tris.Add(i3); tris.Add(i2);
        }

        for (int s = 0; s < segments; s++)
        {
            int fi = s * 2, bi = stride + s * 2;
            int fo = s * 2 + 1, bo = stride + s * 2 + 1;

            tris.Add(fi); tris.Add(fi + 2); tris.Add(bi);
            tris.Add(fi + 2); tris.Add(bi + 2); tris.Add(bi);

            tris.Add(fo); tris.Add(bo); tris.Add(fo + 2);
            tris.Add(bo); tris.Add(bo + 2); tris.Add(fo + 2);
        }

        tris.Add(0); tris.Add(stride); tris.Add(1);
        tris.Add(1); tris.Add(stride); tris.Add(stride + 1);

        int lastF = segments * 2, lastB = stride + segments * 2;
        tris.Add(lastF + 1); tris.Add(lastF); tris.Add(lastB + 1);
        tris.Add(lastF); tris.Add(lastB); tris.Add(lastB + 1);

        var mesh = new Mesh { name = "TunnelArch" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void PlaceBridgePillars(SplineContainer container, Spline spline, float length, float hw)
    {
        int samples = Mathf.Max(2, Mathf.CeilToInt(length / _pillarInterval));

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            spline.Evaluate(t, out var pos3, out _, out _);

            Vector3 pos = pos3;
            float terrainH = _terrain != null ? _terrain.SampleHeight(pos.x, pos.z) : pos.y;
            float pillarH = pos.y - terrainH;

            if (pillarH <= _bridgeHeightThreshold)
            {
                continue;
            }

            Mesh pillarMesh = BuildPillarMesh(_pillarSides, _pillarRadius, pillarH);

            var go = new GameObject("BridgePillar");
            go.transform.SetParent(container.transform, false);
            go.transform.position = new Vector3(pos.x, terrainH, pos.z);
            go.transform.rotation = Quaternion.identity;

            go.AddComponent<MeshFilter>().sharedMesh = pillarMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _pillarMaterial;
        }
    }

    private static Mesh BuildPillarMesh(int sides, float radius, float height)
    {
        float baseR = radius * 1.2f;
        float topR = radius;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        for (int s = 0; s <= sides; s++)
        {
            float angle = 2f * Mathf.PI * s / sides;
            verts.Add(new Vector3(Mathf.Cos(angle) * baseR, 0f, Mathf.Sin(angle) * baseR));
            uvs.Add(new Vector2((float)s / sides, 0f));
        }

        for (int s = 0; s <= sides; s++)
        {
            float angle = 2f * Mathf.PI * s / sides;
            verts.Add(new Vector3(Mathf.Cos(angle) * topR, height, Mathf.Sin(angle) * topR));
            uvs.Add(new Vector2((float)s / sides, 1f));
        }

        for (int s = 0; s < sides; s++)
        {
            int b0 = s, b1 = s + 1;
            int t0 = sides + 1 + s, t1 = sides + 1 + s + 1;
            tris.Add(b0); tris.Add(t0); tris.Add(b1);
            tris.Add(b1); tris.Add(t0); tris.Add(t1);
        }

        int bottomCentre = verts.Count;
        verts.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0f));
        for (int s = 0; s < sides; s++)
        {
            tris.Add(bottomCentre); tris.Add(s + 1); tris.Add(s);
        }

        int topCentre = verts.Count;
        verts.Add(new Vector3(0f, height, 0f));
        uvs.Add(new Vector2(0.5f, 1f));
        int topOffset = sides + 1;
        for (int s = 0; s < sides; s++)
        {
            tris.Add(topCentre); tris.Add(topOffset + s); tris.Add(topOffset + s + 1);
        }

        var mesh = new Mesh { name = "BridgePillar" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
