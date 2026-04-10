#if UNITY_EDITOR
using UnityEditor;
using System.IO;

public static class PackageExporter
{
    public static void Export()
    {
        string[] assetPaths =
        {
            "Assets/Scripts",
            "Assets/Materials",
            "Assets/Scenes/DemoScene.unity"
        };

        Directory.CreateDirectory("Builds");

        AssetDatabase.ExportPackage(
            assetPaths,
            "Builds/ProceduralCityGenerator.unitypackage",
            ExportPackageOptions.Recurse |
            ExportPackageOptions.IncludeDependencies);

        UnityEngine.Debug.Log("[PackageExporter] Export complete.");
    }
}
#endif
