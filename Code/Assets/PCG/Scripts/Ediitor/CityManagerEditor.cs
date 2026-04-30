#if UNITY_EDITOR
using Assets.Scripts.Runtime.City;

using UnityEditor;

using UnityEngine;

namespace Assets.Scripts.Editor
{
    [CustomEditor(typeof(CityManager))]
    public sealed class CityManagerEditor : UnityEditor.Editor
    {
        private bool _showAdvanced;

        public override void OnInspectorGUI()
        {
            var manager = (CityManager)target;
            serializedObject.Update();

            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Voronoi Spatial Hybrid presets set mode and main sliders, then generate. " +
                "Core fields are below; technical tuning is under Advanced.",
                MessageType.None);

            if (GUILayout.Button("Grid at nuclei -> Voronoi outside", GUILayout.Height(28)))
            {
                ApplyVoronoiSpatialPreset(manager, CityManagerEditorPresets.GridCoreVoronoiOutside);
            }

            if (GUILayout.Button("Voronoi at nuclei -> Grid outside", GUILayout.Height(28)))
            {
                ApplyVoronoiSpatialPreset(manager, CityManagerEditorPresets.VoronoiCoreGridOutside);
            }

            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate City", GUILayout.Height(30)))
            {
                manager.Generate();
            }

            if (GUILayout.Button("Clear City", GUILayout.Height(30)))
            {
                manager.Clear();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            DrawCoreSections();

            EditorGUILayout.Space(8);
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced parameters", true);
            if (_showAdvanced)
            {
                DrawAdvancedSections();
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
        }

        private void DrawCoreSections()
        {
            EditorGUILayout.LabelField("City Layout", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_rows"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_columns"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_cellSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_nuclei"), includeChildren: true);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Networks", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_generateStreets"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_generateBoulevard"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_boulevardLineCount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_boulevardWidthMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_generateMetro"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_metroLineCount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_metroStationInterval"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_metroMaterial"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_metroStationMaterial"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_generateStreetDecor"));
        }

        private void DrawAdvancedSections()
        {
            EditorGUILayout.LabelField("Generation Mode", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_generationMode"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Morphology", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_morphology"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_morphologyBlend"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Voronoi Spatial Hybrid", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_spatialGradient"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_spatialInfluence"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_nucleusFalloffWorld"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_gridTopologyInfluence"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Solver and Voronoi", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_seed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxBacktracks"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_voronoiResolution"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_voronoiCellSize"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Routing and Terrain", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_connectionsPerComponent"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_metroBearingPenalty"));
            SerializedProperty terrainAdapterProp = serializedObject.FindProperty("_terrainAdapter");
            EditorGUILayout.PropertyField(terrainAdapterProp);
            DrawTerrainAdapterControls(terrainAdapterProp);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_bridgeHeightThreshold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_tunnelHeightThreshold"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Mesh", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_meshResolution"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_minRoadIntersectionAngleDegrees"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_roadSettings"));
        }

        private static void DrawTerrainAdapterControls(SerializedProperty terrainAdapterProp)
        {
            if (terrainAdapterProp == null || terrainAdapterProp.objectReferenceValue == null)
            {
                return;
            }

            var terrainSO = new SerializedObject(terrainAdapterProp.objectReferenceValue);
            SerializedProperty enforceSlope = terrainSO.FindProperty("_enforceSlopeConstraint");
            SerializedProperty maxSlope = terrainSO.FindProperty("_maxRoadSlopeDegrees");
            SerializedProperty seaLevel = terrainSO.FindProperty("_seaLevel");

            if (enforceSlope == null || maxSlope == null || seaLevel == null)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(enforceSlope, new GUIContent("Enforce Slope Constraint"));
            EditorGUILayout.PropertyField(maxSlope, new GUIContent("Max Road Slope Degrees"));
            EditorGUILayout.PropertyField(seaLevel, new GUIContent("Sea Level"));
            EditorGUI.indentLevel--;

            EditorGUILayout.HelpBox(
                "Terrain slope guidance: 20-30 realistic, 30-45 permissive, >45 mostly unconstrained.",
                MessageType.None);

            terrainSO.ApplyModifiedProperties();
        }

        private static void ApplyVoronoiSpatialPreset(
            CityManager manager,
            CityManagerEditorPresets.VoronoiSpatialConfiguration preset)
        {
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("_generationMode").enumValueIndex = (int)CityGenerationMode.VoronoiSpatialHybrid;
            so.FindProperty("_morphologyBlend").floatValue = preset.MorphologyBlend;
            so.FindProperty("_spatialInfluence").floatValue = preset.SpatialInfluence;
            so.FindProperty("_gridTopologyInfluence").floatValue = preset.GridTopologyInfluence;
            so.FindProperty("_nucleusFalloffWorld").floatValue = preset.NucleusFalloffWorld;
            so.FindProperty("_spatialGradient").enumValueIndex = (int)preset.Gradient;
            so.ApplyModifiedProperties();
            manager.Generate();
        }

        private void OnSceneGUI()
        {
            var manager = (CityManager)target;
            if (manager == null)
            {
                return;
            }

            DrawGrid(manager);
            DrawNuclei(manager);
        }

        private static void DrawGrid(CityManager manager)
        {
            Handles.color = new Color(0.4f, 0.8f, 1f, 0.2f);
            int rows = manager.Rows;
            int columns = manager.Columns;
            float cellSize = manager.CellSize;
            Vector3 origin = manager.transform.position;

            for (int r = 0; r <= rows; r++)
            {
                Vector3 start = origin + new Vector3(0, 0, r * cellSize);
                Vector3 end = origin + new Vector3(columns * cellSize, 0, r * cellSize);
                Handles.DrawLine(start, end);
            }

            for (int c = 0; c <= columns; c++)
            {
                Vector3 start = origin + new Vector3(c * cellSize, 0, 0);
                Vector3 end = origin + new Vector3(c * cellSize, 0, rows * cellSize);
                Handles.DrawLine(start, end);
            }
        }

        private static void DrawNuclei(CityManager manager)
        {
            var nuclei = manager.Nuclei;
            if (nuclei == null)
            {
                return;
            }

            foreach (var nucleus in nuclei)
            {
                Vector3 centre = new Vector3(nucleus.Centre.x, manager.transform.position.y, nucleus.Centre.y);

                Handles.color = new Color(1f, 0.5f, 0f, 0.9f);
                Handles.SphereHandleCap(0, centre, Quaternion.identity, nucleus.Radius * 0.06f, EventType.Repaint);

                Handles.color = new Color(1f, 0.5f, 0f, 0.25f);
                Handles.DrawSolidDisc(centre, Vector3.up, nucleus.Radius);

                Handles.color = new Color(1f, 0.6f, 0f, 0.9f);
                Handles.DrawWireDisc(centre, Vector3.up, nucleus.Radius);

                Handles.Label(centre + Vector3.up * 4f,
                    $"Nucleus\nR={nucleus.Radius:F0}  S={nucleus.Strength:F1}",
                    EditorStyles.boldLabel);
            }
        }
    }
}
#endif
