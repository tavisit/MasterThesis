#if UNITY_EDITOR
using Assets.Scripts.Runtime.City;

using UnityEditor;

using UnityEngine;

namespace Assets.Scripts.Editor
{
    [CustomEditor(typeof(CityManager))]
    public sealed class CityManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            var manager = (CityManager)target;

            if (GUILayout.Button("Generate City"))
            {
                manager.Generate();
            }

            if (GUILayout.Button("Clear City"))
            {
                manager.Clear();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Result", manager.LastResult.ToString());
            EditorGUILayout.LabelField("Collapse Count", manager.CollapseCount.ToString());
            EditorGUILayout.LabelField("Backtrack Count", manager.BacktrackCount.ToString());
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
            DrawBoulevardPreview(manager);
        }

        private void DrawGrid(CityManager manager)
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

        private void DrawNuclei(CityManager manager)
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

        private void DrawBoulevardPreview(CityManager manager)
        {
            var nuclei = manager.Nuclei;
            if (nuclei == null || nuclei.Length < 2)
            {
                return;
            }

            Handles.color = new Color(1f, 0.9f, 0f, 0.8f);
            var connected = new System.Collections.Generic.HashSet<(int, int)>();
            int k = manager.MaxBoulevards;

            for (int i = 0; i < nuclei.Length; i++)
            {
                var distances = new System.Collections.Generic.List<(float d, int j)>();
                for (int j = 0; j < nuclei.Length; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    distances.Add((Vector2.Distance(nuclei[i].Centre, nuclei[j].Centre), j));
                }
                distances.Sort((a, b) => a.d.CompareTo(b.d));

                int count = 0;
                foreach (var (_, j) in distances)
                {
                    if (count >= k)
                    {
                        break;
                    }

                    int a = Mathf.Min(i, j), b = Mathf.Max(i, j);
                    if (connected.Contains((a, b)))
                    {
                        continue;
                    }

                    connected.Add((a, b));
                    count++;

                    float y = manager.transform.position.y;
                    Vector3 from = new Vector3(nuclei[i].Centre.x, y, nuclei[i].Centre.y);
                    Vector3 to = new Vector3(nuclei[j].Centre.x, y, nuclei[j].Centre.y);
                    Handles.DrawLine(from, to, 3f);
                }
            }
        }
    }
}
#endif
