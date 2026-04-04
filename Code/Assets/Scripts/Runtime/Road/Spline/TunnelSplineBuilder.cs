using System.Collections.Generic;
using Assets.Scripts.Runtime.Graph;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Spline
{
    public static class TunnelSplineBuilder
    {
        private const float _depthFraction = 0.18f;

        public static SplineContainer BuildTunnelSpline(
            RoadConnection conn,
            Transform parent,
            float maxDepth = 10f)
        {
            var go = new GameObject("TunnelSpline");
            go.transform.SetParent(parent, false);

            var container = go.AddComponent<SplineContainer>();
            var spline = container.Spline;
            spline.Clear();

            Vector3 a = conn.From.Position;
            Vector3 b = conn.To.Position;
            float dist = Vector3.Distance(a, b);

            float arcDepth = Mathf.Min(dist * _depthFraction, maxDepth);
            Vector3 mid = (a + b) * 0.5f;
            mid.y = Mathf.Min(a.y, b.y) - arcDepth;

            Vector3 dir = (b - a).normalized;
            float tangentLen = dist * 0.25f;
            var entryUnder = Vector3.Lerp(a, mid, 0.45f);
            var exitUnder = Vector3.Lerp(b, mid, 0.45f);

            AddKnot(spline, a, dir * tangentLen);
            AddKnot(spline, entryUnder, dir * tangentLen);
            AddKnot(spline, exitUnder, dir * tangentLen);
            AddKnot(spline, b, dir * tangentLen);

            return container;
        }

        private static void AddKnot(UnityEngine.Splines.Spline spline, Vector3 pos, Vector3 tangent)
        {
            spline.Add(new BezierKnot(
                new float3(pos.x, pos.y, pos.z),
                new float3(-tangent.x, -tangent.y, -tangent.z),
                new float3(tangent.x, tangent.y, tangent.z)
            ), TangentMode.Broken);
        }
        public static GameObject BuildPortalHood(
            Vector3 position,
            Vector3 forward,
            float roadWidth,
            float hoodDepth,
            Material material,
            Transform parent)
        {
            var go = new GameObject("TunnelHood");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            filter.sharedMesh = BuildHalfPipeMesh(roadWidth * 0.5f + 0.6f, hoodDepth);
            return go;
        }

        private static Mesh BuildHalfPipeMesh(float radius, float depth)
        {
            int arcSegs = 16;
            int depthSegs = 6;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int d = 0; d <= depthSegs; d++)
            {
                float z = (float)d / depthSegs * depth;
                float v = (float)d / depthSegs;

                for (int a = 0; a <= arcSegs; a++)
                {
                    float angle = Mathf.PI * a / arcSegs;
                    float x = Mathf.Cos(angle) * radius;
                    float y = Mathf.Sin(angle) * radius;
                    float u = (float)a / arcSegs;

                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(u, v));
                }
            }

            int stride = arcSegs + 1;
            for (int d = 0; d < depthSegs; d++)
            {
                for (int a = 0; a < arcSegs; a++)
                {
                    int bl = d * stride + a;
                    int br = bl + 1;
                    int tl = bl + stride;
                    int tr = tl + 1;

                    tris.Add(bl); tris.Add(br); tris.Add(tl);
                    tris.Add(br); tris.Add(tr); tris.Add(tl);
                    tris.Add(tl); tris.Add(br); tris.Add(bl);
                    tris.Add(tl); tris.Add(tr); tris.Add(br);
                }
            }

            int capBase = verts.Count;
            Vector3 centre = Vector3.zero;
            verts.Add(centre);
            uvs.Add(new Vector2(0.5f, 0f));
            for (int a = 0; a <= arcSegs; a++)
            {
                float angle = Mathf.PI * a / arcSegs;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                uvs.Add(new Vector2((float)a / arcSegs, 0f));
            }
            for (int a = 0; a < arcSegs; a++)
            {
                tris.Add(capBase);
                tris.Add(capBase + a + 2);
                tris.Add(capBase + a + 1);
            }

            verts.Add(new Vector3(-radius, 0f, 0f));
            verts.Add(new Vector3(radius, 0f, 0f));
            verts.Add(new Vector3(-radius, 0f, depth));
            verts.Add(new Vector3(radius, 0f, depth));
            int fb = verts.Count - 4;
            uvs.Add(Vector2.zero); uvs.Add(Vector2.right);
            uvs.Add(Vector2.up); uvs.Add(Vector2.one);
            tris.Add(fb); tris.Add(fb + 1); tris.Add(fb + 2);
            tris.Add(fb + 1); tris.Add(fb + 3); tris.Add(fb + 2);

            var mesh = new Mesh { name = "TunnelHood" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
