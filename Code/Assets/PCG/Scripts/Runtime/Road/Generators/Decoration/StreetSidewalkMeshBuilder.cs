using Assets.Scripts.Runtime.City;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public static class StreetSidewalkMeshBuilder
    {
        internal static void BuildSidewalkStrips(
            SplineContainer container,
            CityManager manager,
            float halfWidth,
            NeighborhoodStyleSample style)
        {
            float length = container.Spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            int rings = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(
                RoadGenerationOffsets.SidewalkMinMeshResolution,
                manager.MeshResolution * RoadGenerationOffsets.SidewalkRingDensityScale)));
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

                float inner = Mathf.Max(RoadGenerationOffsets.MinPositive, halfWidth);
                float outer = inner + style.SidewalkWidth;
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

            BuildSidewalkTriangles(triangles, rings);

            var mesh = new Mesh { name = "SidewalkStrip" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            EnsureSidewalkNormalsPointUp(mesh);
            mesh.RecalculateBounds();

            var go = new GameObject("StreetSidewalk");
            go.transform.SetParent(container.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = style.SidewalkMaterial;
        }

        internal static void BuildBoulevardInteriorSidewalk(
            SplineContainer container,
            CityManager manager,
            NeighborhoodStyleSample style)
        {
            if (container == null || manager == null || container.Spline == null)
            {
                return;
            }

            float length = container.Spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            int rings = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(
                RoadGenerationOffsets.SidewalkMinMeshResolution,
                manager.MeshResolution * RoadGenerationOffsets.SidewalkRingDensityScale)));
            float yOffset = manager.SidewalkMeshVerticalOffset
                + style.BoulevardInteriorSidewalkVerticalOffset
                + RoadGenerationOffsets.BoulevardInteriorDecorationYOffsetDelta;
            float halfInteriorWidth = Mathf.Max(
                RoadGenerationOffsets.SidewalkMinHalfWidth,
                style.BoulevardInteriorSidewalkWidth * RoadGenerationOffsets.SidewalkCenterOffsetRatio);

            var vertices = new Vector3[rings * 2];
            var uvs = new Vector2[rings * 2];
            var triangles = new int[(rings - 1) * 2 * 3];

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

                int b = i * 2;
                vertices[b + 0] = p - right * halfInteriorWidth;
                vertices[b + 1] = p + right * halfInteriorWidth;

                float v = t * length;
                uvs[b + 0] = new Vector2(0f, v);
                uvs[b + 1] = new Vector2(1f, v);
            }

            int ti = 0;
            for (int i = 0; i < rings - 1; i++)
            {
                int cur = i * 2;
                int nxt = cur + 2;
                triangles[ti++] = cur + 0; triangles[ti++] = cur + 1; triangles[ti++] = nxt + 0;
                triangles[ti++] = cur + 1; triangles[ti++] = nxt + 1; triangles[ti++] = nxt + 0;
            }

            var mesh = new Mesh { name = "SidewalkStrip_BoulevardInterior" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            EnsureSidewalkNormalsPointUp(mesh);
            mesh.RecalculateBounds();

            var go = new GameObject("StreetSidewalk_BoulevardInterior");
            go.transform.SetParent(container.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = style.BoulevardInteriorSidewalkMaterial;
        }

        private static void EnsureSidewalkNormalsPointUp(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }
            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length == 0)
            {
                return;
            }
            float avgY = 0f;
            for (int i = 0; i < normals.Length; i++)
            {
                avgY += normals[i].y;
            }
            if (avgY >= 0f)
            {
                return;
            }
            int[] tris = mesh.triangles;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                (tris[i], tris[i + 1]) = (tris[i + 1], tris[i]);
            }
            mesh.triangles = tris;
            mesh.RecalculateNormals();
        }

        private static void BuildSidewalkTriangles(int[] triangles, int rings)
        {
            int ti = 0;
            for (int i = 0; i < rings - 1; i++)
            {
                int cur = i * 4;
                int nxt = cur + 4;
                triangles[ti++] = cur + 0; triangles[ti++] = cur + 1; triangles[ti++] = nxt + 0;
                triangles[ti++] = cur + 1; triangles[ti++] = nxt + 1; triangles[ti++] = nxt + 0;
                triangles[ti++] = cur + 2; triangles[ti++] = nxt + 2; triangles[ti++] = cur + 3;
                triangles[ti++] = cur + 3; triangles[ti++] = nxt + 2; triangles[ti++] = nxt + 3;
            }
        }
    }
}
