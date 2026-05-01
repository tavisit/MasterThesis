using Assets.Scripts.Runtime.Road.Generators;

using NUnit.Framework;

using UnityEngine;

public class RoundaboutMeshUtilityTests
{
    [Test]
    public void BuildDiscMesh_InvalidParameters_ReturnsNull()
    {
        var parent = new GameObject("RoundaboutTestParent").transform;
        try
        {
            Assert.IsNull(RoundaboutMeshUtility.BuildDiscMesh(null, Vector3.zero, 5f, 8, "X"));
            Assert.IsNull(RoundaboutMeshUtility.BuildDiscMesh(parent, Vector3.zero, 5f, 4, "X"));
            Assert.IsNull(RoundaboutMeshUtility.BuildDiscMesh(parent, Vector3.zero, 0.01f, 8, "X"));
        }
        finally
        {
            Object.DestroyImmediate(parent.gameObject);
        }
    }

    [Test]
    public void BuildDiscMesh_ValidParameters_ProducesExpectedTopology()
    {
        var go = new GameObject("RoundaboutTestParent");
        Transform parent = go.transform;
        try
        {
            Mesh mesh = RoundaboutMeshUtility.BuildDiscMesh(
                parent,
                new Vector3(10f, 2f, -5f),
                radius: 5f,
                segments: 8,
                meshName: "TestDisc");

            Assert.IsNotNull(mesh);
            Assert.AreEqual("TestDisc", mesh.name);
            Assert.AreEqual(9, mesh.vertexCount);
            Assert.AreEqual(8 * 3, mesh.triangles.Length);
            var uvs = new System.Collections.Generic.List<Vector2>();
            mesh.GetUVs(0, uvs);
            Assert.AreEqual(9, uvs.Count);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BuildSidewalkRingMesh_ValidParameters_ProducesExpectedTopology()
    {
        var go = new GameObject("RoundaboutRingTestParent");
        Transform parent = go.transform;
        try
        {
            Mesh mesh = RoundaboutMeshUtility.BuildSidewalkRingMesh(
                parent,
                Vector3.zero,
                innerRadius: 2f,
                outerRadius: 4f,
                segments: 6,
                yOffset: 0.02f,
                meshName: "TestRing");

            Assert.IsNotNull(mesh);
            Assert.AreEqual(14, mesh.vertexCount);
            Assert.AreEqual(6 * 6, mesh.triangles.Length);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void EnsureUpwardNormals_NullMesh_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => RoundaboutMeshUtility.EnsureUpwardNormals(null));
    }
}
