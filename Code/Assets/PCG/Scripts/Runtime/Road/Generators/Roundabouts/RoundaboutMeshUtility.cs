using System.Collections.Generic;

using UnityEngine;

namespace Assets.Scripts.Runtime.Road.Generators
{
    internal static class RoundaboutMeshUtility
    {
        internal static Mesh BuildDiscMesh(
            Transform parent,
            Vector3 centerWorld,
            float radius,
            int segments,
            string meshName)
        {
            if (parent == null || segments < 6 || radius <= 0.05f)
            {
                return null;
            }

            Matrix4x4 worldToLocal = parent.worldToLocalMatrix;
            var vertices = new List<Vector3>(segments + 1);
            var triangles = new List<int>(segments * 3);
            var uvs = new List<Vector2>(segments + 1);

            Vector3 centerLocal = worldToLocal.MultiplyPoint3x4(centerWorld);
            vertices.Add(centerLocal);
            uvs.Add(new Vector2(
                centerWorld.x * RoadGenerationOffsets.RoundaboutRoadUvWorldTiling,
                centerWorld.z * RoadGenerationOffsets.RoundaboutRoadUvWorldTiling));

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 ringWorld = centerWorld + dir * radius;
                ringWorld.y -= RoadGenerationOffsets.RoundaboutEdgeYDrop;
                Vector3 ringLocal = worldToLocal.MultiplyPoint3x4(ringWorld);
                vertices.Add(ringLocal);
                uvs.Add(new Vector2(
                    ringWorld.x * RoadGenerationOffsets.RoundaboutRoadUvWorldTiling,
                    ringWorld.z * RoadGenerationOffsets.RoundaboutRoadUvWorldTiling));
            }

            for (int i = 0; i < segments; i++)
            {
                int b = 1 + i;
                int c = 1 + ((i + 1) % segments);
                triangles.Add(0);
                triangles.Add(b);
                triangles.Add(c);
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            EnsureUpwardNormals(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Mesh BuildSidewalkRingMesh(
            Transform parent,
            Vector3 centerWorld,
            float innerRadius,
            float outerRadius,
            int segments,
            float yOffset,
            string meshName)
        {
            if (parent == null || segments < 6 || outerRadius <= innerRadius + 0.05f)
            {
                return null;
            }

            Matrix4x4 worldToLocal = parent.worldToLocalMatrix;
            var vertices = new List<Vector3>((segments + 1) * 2);
            var triangles = new List<int>(segments * 6);
            var uvs = new List<Vector2>((segments + 1) * 2);

            Vector3 centerRaised = centerWorld + Vector3.up * yOffset;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 inner = centerRaised + dir * innerRadius;
                Vector3 outer = centerRaised + dir * outerRadius;
                vertices.Add(worldToLocal.MultiplyPoint3x4(inner));
                vertices.Add(worldToLocal.MultiplyPoint3x4(outer));
                uvs.Add(new Vector2(
                    inner.x * RoadGenerationOffsets.RoundaboutSidewalkUvWorldTiling,
                    inner.z * RoadGenerationOffsets.RoundaboutSidewalkUvWorldTiling));
                uvs.Add(new Vector2(
                    outer.x * RoadGenerationOffsets.RoundaboutSidewalkUvWorldTiling,
                    outer.z * RoadGenerationOffsets.RoundaboutSidewalkUvWorldTiling));
            }

            for (int i = 0; i < segments; i++)
            {
                int cur = i * 2;
                int nxt = cur + 2;
                triangles.Add(cur + 0); triangles.Add(cur + 1); triangles.Add(nxt + 0);
                triangles.Add(cur + 1); triangles.Add(nxt + 1); triangles.Add(nxt + 0);
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            EnsureUpwardNormals(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static void EnsureUpwardNormals(Mesh mesh)
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
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }

            mesh.triangles = tris;
            mesh.RecalculateNormals();
        }
    }
}
