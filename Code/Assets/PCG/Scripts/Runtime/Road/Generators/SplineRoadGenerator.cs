using System.Collections.Generic;
using System.Linq;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;
using Assets.Scripts.Runtime.Spline;
using Assets.Scripts.Runtime.Voronoi;
using Assets.Scripts.Runtime.WFC;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    public sealed class SplineRoadGenerator
    {
        private readonly CityManager _manager;
        private readonly Transform _root;
        private readonly RoadSettings _roadSettings;
        private readonly RoadOverlayGenerator _overlay;

        private readonly List<GameObject> _generated = new();
        private readonly HashSet<Vector2Int> _placedStationCells = new();

        public SplineRoadGenerator(CityManager manager)
        {
            _manager = manager;
            _root = manager.transform;
            _roadSettings = manager.RoadSettings;
            _overlay = manager.GetComponent<RoadOverlayGenerator>();
        }

        public void Generate()
        {
            _placedStationCells.Clear();
            Clear();
            _overlay?.ResetPlacedCells();

            int rows = _manager.Rows;
            int columns = _manager.Columns;
            float cellSize = _manager.CellSize;

            WFCSolver streetSolver = _manager.StreetSolver;
            VoronoiWFCSolver voronoiSolver = _manager.VoronoiStreetSolver;
            bool isOrganic = _manager.Morphology == UrbanMorphology.Organic;

            RoadGraph streetGraph = null;

            if (_manager.GenerateStreets &&
                (streetSolver != null || voronoiSolver != null))
            {
                streetGraph = isOrganic && voronoiSolver != null
                    ? VoronoiRoadGraphExtractor.Extract(
                        voronoiSolver, _manager.TerrainAdapter, RoadType.Street)
                    : RoadGraphExtractor.Extract(
                        streetSolver, rows, columns, cellSize,
                        _manager.TerrainAdapter, RoadType.Street, RoadSockets.Road);

                ProcessGraph(streetGraph, RoadType.Street, _manager.StreetMaterial);
            }

            if (_manager.GenerateMetro &&
                _manager.Nuclei != null && _manager.Nuclei.Length >= 2 &&
                streetGraph != null)
            {
                List<SplineContainer> metroContainers = MetroGenerator.Generate(
                    streetGraph,
                    _manager.Nuclei,
                    _root,
                    _manager.Morphology,
                    _manager.MetroBearingPenalty);

                foreach (var container in metroContainers)
                {
                    container.gameObject.name = "RoadSpline_Metro";
                    var extruder = container.gameObject.AddComponent<RoadMeshExtruder>();
                    extruder.RoadType = RoadType.Metro;
                    extruder.Resolution = _manager.MeshResolution;
                    extruder.RoadMaterial = _manager.MetroMaterial;
                    extruder.Rebuild();
                    _generated.Add(container.gameObject);
                    ProcessOverlay(container, RoadType.Metro);
                    PlaceMetroStations(container);
                }

                Debug.Log($"[SplineRoadGenerator] Metro: {metroContainers.Count} lines generated.");
            }
            else if (_manager.GenerateMetro)
            {
                Debug.LogWarning("[SplineRoadGenerator] Metro skipped: requires streets " +
                                 "and at least 2 nuclei to route through.");
            }

            Debug.Log($"[SplineRoadGenerator] Done. Total objects: {_generated.Count}");
        }

        public void Clear()
        {
            foreach (var go in _generated)
            {
                if (go == null)
                {
                    continue;
                }
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(go);
                }
                else
#endif
                    Object.Destroy(go);
            }
            _generated.Clear();
        }


        private void ProcessGraph(RoadGraph graph, RoadType type, Material material)
        {
            Debug.Log($"[SplineRoadGenerator] {type} graph: {graph.Nodes.Count} nodes, " +
                      $"{graph.Edges.Count} edges.");

            float hw = RoadMeshExtruder.GetHalfWidth(type, _roadSettings);
            PruneParallelDuplicateEdges(graph, hw * 3f, 15f);
            PruneAcuteEdges(graph, 20f);

            int originalEdgeCount = graph.Edges.Count;

            List<RoadConnection> connectorEdges = RoadGraphConnector.ConnectComponents(
                graph,
                _manager.BridgeHeightThreshold,
                _manager.TunnelHeightThreshold,
                _manager.TerrainAdapter,
                _manager.ConnectionsPerComponent);

            Debug.Log($"[SplineRoadGenerator] {type}: added {connectorEdges.Count} connector edges.");

            var originalGraph = graph.SubgraphUpToEdge(originalEdgeCount);
            List<SplineContainer> containers = RoadSplineBuilder.BuildSplines(
                originalGraph, _root, _roadSettings, _manager.Morphology, graph.GetHashCode());
            SpawnSplines(containers, type, material);

            foreach (var conn in connectorEdges)
            {
                var connGraph = new RoadGraph();
                connGraph.AddEdge(
                    connGraph.AddNode(conn.From.Position, type),
                    connGraph.AddNode(conn.To.Position, type),
                    type);
                SpawnSplines(
                    RoadSplineBuilder.BuildSplines(connGraph, _root, _roadSettings),
                    type, material);
            }

            SpawnJunctionCaps(graph, type, material);

            Debug.Log($"[SplineRoadGenerator] {type}: {containers.Count} splines generated.");
        }


        private void SpawnJunctionCaps(RoadGraph graph, RoadType type, Material material)
        {
            float hw = RoadMeshExtruder.GetHalfWidth(type, _roadSettings);
            float capRadius = hw * 1.6f;
            int segments = 16;

            var capsParent = new GameObject($"JunctionCaps_{type}");
            capsParent.transform.SetParent(_root, false);
            _generated.Add(capsParent);

            var degree = new Dictionary<RoadNode, int>();
            foreach (var edge in graph.Edges)
            {
                degree.TryGetValue(edge.From, out int df); degree[edge.From] = df + 1;
                degree.TryGetValue(edge.To, out int dt); degree[edge.To] = dt + 1;
            }

            foreach (var kvp in degree)
            {
                if (kvp.Value < 3)
                {
                    continue;
                }

                RoadNode node = kvp.Key;
                float coneHeight = hw * 0.2f;

                var verts = new List<Vector3> { new Vector3(0f, coneHeight, 0f) };
                var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
                var tris = new List<int>();

                for (int s = 0; s <= segments; s++)
                {
                    float angle = 2f * Mathf.PI * s / segments;
                    float x = Mathf.Cos(angle) * capRadius;
                    float z = Mathf.Sin(angle) * capRadius;
                    verts.Add(new Vector3(x, 0f, z));
                    uvs.Add(new Vector2(x / (capRadius * 2f) + 0.5f,
                                        z / (capRadius * 2f) + 0.5f));
                }

                for (int s = 0; s < segments; s++)
                {
                    tris.Add(0); tris.Add(s + 2); tris.Add(s + 1);
                }

                var mesh = new Mesh { name = "JunctionCap" };
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.SetUVs(0, uvs);
                mesh.RecalculateNormals();

                var go = new GameObject("JunctionCap");
                go.transform.SetParent(capsParent.transform, false);
                go.transform.position = node.Position;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }


        private static void PruneParallelDuplicateEdges(
            RoadGraph graph, float maxMidpointDistance, float maxAngleDegrees)
        {
            var edges = graph.Edges.ToList();
            var toRemove = new HashSet<RoadEdge>();

            for (int i = 0; i < edges.Count; i++)
            {
                if (toRemove.Contains(edges[i]))
                {
                    continue;
                }

                Vector3 midI = (edges[i].From.Position + edges[i].To.Position) * 0.5f;
                Vector3 dirI = (edges[i].To.Position - edges[i].From.Position).normalized;
                float lenI = Vector3.Distance(edges[i].From.Position, edges[i].To.Position);

                for (int j = i + 1; j < edges.Count; j++)
                {
                    if (toRemove.Contains(edges[j]))
                    {
                        continue;
                    }

                    Vector3 midJ = (edges[j].From.Position + edges[j].To.Position) * 0.5f;
                    Vector3 dirJ = (edges[j].To.Position - edges[j].From.Position).normalized;
                    float lenJ = Vector3.Distance(edges[j].From.Position, edges[j].To.Position);

                    if (Vector3.Distance(midI, midJ) > maxMidpointDistance)
                    {
                        continue;
                    }

                    float angle = Mathf.Min(
                        Vector3.Angle(dirI, dirJ),
                        Vector3.Angle(dirI, -dirJ));

                    if (angle < maxAngleDegrees)
                    {
                        toRemove.Add(lenI < lenJ ? edges[i] : edges[j]);
                    }
                }
            }

            if (toRemove.Count > 0)
            {
                Debug.Log($"[SplineRoadGenerator] Removed {toRemove.Count} parallel duplicate edges.");
                graph.RetainEdges(e => !toRemove.Contains(e));
            }
        }

        private static void PruneAcuteEdges(RoadGraph graph, float minAngleDegrees)
        {
            var adj = new Dictionary<RoadNode, List<RoadEdge>>();
            foreach (var node in graph.Nodes)
            {
                adj[node] = new List<RoadEdge>();
            }

            foreach (var edge in graph.Edges)
            {
                adj[edge.From].Add(edge);
                adj[edge.To].Add(edge);
            }

            var toRemove = new HashSet<RoadEdge>();

            foreach (var node in graph.Nodes)
            {
                var edges = adj[node];
                if (edges.Count < 3)
                {
                    continue;
                }

                var dirs = new (RoadEdge edge, Vector3 dir, float len)[edges.Count];
                for (int i = 0; i < edges.Count; i++)
                {
                    var e = edges[i];
                    var nb = e.From == node ? e.To : e.From;
                    dirs[i] = (e,
                        (nb.Position - node.Position).normalized,
                        Vector3.Distance(e.From.Position, e.To.Position));
                }

                for (int i = 0; i < dirs.Length; i++)
                {
                    for (int j = i + 1; j < dirs.Length; j++)
                    {
                        if (Vector3.Angle(dirs[i].dir, dirs[j].dir) < minAngleDegrees)
                        {
                            toRemove.Add(dirs[i].len < dirs[j].len
                                ? dirs[i].edge : dirs[j].edge);
                        }
                    }
                }
            }

            if (toRemove.Count > 0)
            {
                Debug.Log($"[SplineRoadGenerator] Pruned {toRemove.Count} acute-angle edges.");
                graph.RetainEdges(e => !toRemove.Contains(e));
            }
        }

        private void SpawnSplines(
            List<SplineContainer> containers,
            RoadType type,
            Material material)
        {
            foreach (var container in containers)
            {
                container.gameObject.name = $"RoadSpline_{type}";
                var extruder = container.gameObject.AddComponent<RoadMeshExtruder>();
                extruder.RoadType = type;
                extruder.Resolution = _manager.MeshResolution;
                extruder.RoadMaterial = material;
                extruder.Rebuild();
                _generated.Add(container.gameObject);
                ProcessOverlay(container, type);
            }
        }

        private void ProcessOverlay(SplineContainer container, RoadType type)
        {
            _overlay?.ProcessSpline(container, type);
        }


        private void PlaceMetroStations(SplineContainer container)
        {
            var spline = container.Spline;
            float length = spline.GetLength();
            if (length <= 0f)
            {
                return;
            }

            float interval = _manager.MetroStationInterval;
            int count = Mathf.Max(1, Mathf.FloorToInt(length / interval));
            float hw = RoadMeshExtruder.GetHalfWidth(RoadType.Metro, _roadSettings);
            float platformW = hw * 2f;
            float platformL = Mathf.Clamp(interval * 0.1f, 4f, 18f);

            var stationsParent = new GameObject("MetroStations");
            stationsParent.transform.SetParent(container.transform, false);

            foreach (float tt in new[] { 0f, 1f })
            {
                TryPlaceStation(container, spline, tt, platformW, platformL,
                                stationsParent, forced: true);
            }

            for (int s = 1; s <= count; s++)
            {
                float t = (float)s / (count + 1);
                TryPlaceStation(container, spline, t, platformW, platformL,
                                stationsParent, forced: false);
            }
        }

        private void TryPlaceStation(
            SplineContainer container,
            UnityEngine.Splines.Spline spline,
            float t,
            float platformW,
            float platformL,
            GameObject stationsParent,
            bool forced)
        {
            spline.Evaluate(t, out var pos3, out var tan3, out _);

            Vector3 railPos = container.transform.TransformPoint(pos3);
            Vector3 tangent = container.transform.TransformDirection(
                ((Vector3)tan3).normalized);

            float terrainH = _manager.TerrainAdapter != null
                ? _manager.TerrainAdapter.SampleHeight(railPos.x, railPos.z)
                : railPos.y - 6f;

            float railH = railPos.y;
            float aboveTerrain = railH - terrainH;

            if (!forced)
            {
                if (terrainH > railH + 1f)
                {
                    return;
                }

                if (aboveTerrain > 15f)
                {
                    return;
                }

                var cell = new Vector2Int(
                    Mathf.RoundToInt(railPos.x / (_manager.MetroStationInterval * 0.5f)),
                    Mathf.RoundToInt(railPos.z / (_manager.MetroStationInterval * 0.5f)));
                if (_placedStationCells.Contains(cell))
                {
                    return;
                }

                _placedStationCells.Add(cell);
            }

            float pillarH = Mathf.Max(0.1f, aboveTerrain +
                            RoadMeshExtruder.GetHalfWidth(RoadType.Metro, _roadSettings));

            var verts = new List<Vector3>
            {
                new Vector3(-platformW * 0.5f, 0f,      -platformL * 0.5f),
                new Vector3( platformW * 0.5f, 0f,      -platformL * 0.5f),
                new Vector3( platformW * 0.5f, 0f,       platformL * 0.5f),
                new Vector3(-platformW * 0.5f, 0f,       platformL * 0.5f),
                new Vector3(-platformW * 0.5f, pillarH, -platformL * 0.5f),
                new Vector3( platformW * 0.5f, pillarH, -platformL * 0.5f),
                new Vector3( platformW * 0.5f, pillarH,  platformL * 0.5f),
                new Vector3(-platformW * 0.5f, pillarH,  platformL * 0.5f),
            };

            var tris = new List<int>
            {
                0, 1, 2,  0, 2, 3,
                4, 6, 5,  4, 7, 6,
                0, 5, 1,  0, 4, 5,
                2, 7, 3,  2, 6, 7,
                3, 4, 0,  3, 7, 4,
                1, 6, 2,  1, 5, 6,
            };

            var mesh = new Mesh { name = "MetroStation" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(forced ? "MetroStation_Terminal" : "MetroStation");
            go.transform.SetParent(stationsParent.transform, false);
            go.transform.position = new Vector3(railPos.x, terrainH, railPos.z);

            if (tangent.sqrMagnitude > 0.001f)
            {
                go.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            }

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _manager.MetroStationMaterial;
        }
    }
}
