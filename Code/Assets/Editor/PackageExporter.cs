#if UNITY_EDITOR
using System.IO;

using UnityEditor;

public static class PackageExporter
{
    public static void Export()
    {
        string[] assetPaths = { "Assets/PCG/Scripts",
            "Assets/PCG/Materials",
            "Assets/PCG/Scenes",
            "Assets/PCG/README.md"
        };

        Directory.CreateDirectory("Builds");

        AssetDatabase.ExportPackage(assetPaths, "Builds/ProceduralCityGenerator.unitypackage",
            ExportPackageOptions.Recurse);

        UnityEngine.Debug.Log("[PackageExporter] Export complete.");
    }
}
#endif
