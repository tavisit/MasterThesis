using System.Reflection;

using Assets.Scripts.Runtime.Road.Generators;

using NUnit.Framework;

public class StreetDecorationGeneratorTests
{
    [Test]
    public void BuildSidewalkTriangles_UsesUpwardFacingWinding()
    {
        var triangles = new int[(2 - 1) * 4 * 3];
        MethodInfo method = typeof(StreetSidewalkMeshBuilder).GetMethod(
            "BuildSidewalkTriangles",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "Expected StreetSidewalkMeshBuilder.BuildSidewalkTriangles to exist.");

        method.Invoke(null, new object[] { triangles, 2 });

        int[] expected = { 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7 };
        CollectionAssert.AreEqual(
            expected,
            triangles,
            "Sidewalk triangles should be wound so normals face upward.");
    }
}
