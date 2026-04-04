using System.Collections.Generic;

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
    public sealed class SplineRoadGenerator : MonoBehaviour
    {
        private CityManager _manager;

        public WFCSolver MetroSolver { get; private set; }

        private readonly List<GameObject> _generated = new();

        private void Awake()
        {
            _manager = GetComponent<CityManager>();
        }

        private CityManager Manager => _manager != null ? _manager : (_manager = GetComponent<CityManager>());

        [ContextMenu("Generate Road Splines")]
        public void Generate()
        {
            Clear();

            if (Manager == null)
            {
                Debug.LogError("[SplineRoadGenerator] No CityManager found on this GameObject.");
                return;
            }

            int rows = Manager.Rows;
            int columns = Manager.Columns;
            float cellSize = Manager.CellSize;

            WFCSolver streetSolver = Manager.StreetSolver;
            VoronoiWFCSolver voronoiSolver = Manager.VoronoiStreetSolver;
            bool isOrganic = Manager.Morphology == UrbanMorphology.Organic;

            if ((streetSolver != null || voronoiSolver != null) && (Manager.GenerateStreets || Manager.GenerateBoulevards))
            {
                RoadGraph fullGraph = isOrganic && voronoiSolver != null
                    ? VoronoiRoadGraphExtractor.Extract(voronoiSolver, Manager.TerrainAdapter, RoadType.Street, yOffset: 1.0f)
                    : RoadGraphExtractor.Extract(streetSolver, rows, columns, cellSize,
                        Manager.TerrainAdapter, RoadType.Street, RoadSockets.Road, yOffset: 1.0f);

                if (Manager.GenerateBoulevards && Manager.Nuclei != null && Manager.Nuclei.Length >= 2)
                {
                    var (blvdContainers, remainingStreets) = BoulevardGenerator.Generate(
                        fullGraph, Manager.Nuclei, transform,
                        Manager.Morphology, Manager.MaxBoulevards,
                        Manager.TunnelHeightThreshold,
                        Manager.BoulevardBearingPenalty);

                    foreach (var container in blvdContainers)
                    {
                        container.gameObject.name = "RoadSpline_Boulevard";
                        var extruder = container.gameObject.AddComponent<RoadMeshExtruder>();
                        extruder.RoadType = RoadType.Boulevard;
                        extruder.Resolution = Manager.MeshResolution;
                        extruder.RoadMaterial = Manager.BoulevardMaterial;
                        extruder.Rebuild();
                        _generated.Add(container.gameObject);
                    }

                    Debug.Log($"[SplineRoadGenerator] Boulevard: {blvdContainers.Count} splines generated.");

                    if (Manager.GenerateStreets)
                    {
                        ProcessGraph(fullGraph, RoadType.Street, Manager.StreetMaterial, forceFullConnectivity: false);
                    }
                }
                else
                {
                    if (Manager.GenerateStreets)
                    {
                        ProcessGraph(fullGraph, RoadType.Street, Manager.StreetMaterial, forceFullConnectivity: false);
                    }
                }
            }

            if (Manager.GenerateMetro)
            {
                MetroSolver = new WFCSolver(
                    RoadTileSetFactory.CreateMetro(), rows, columns, Manager.MetroSeed, maxBacktracks: 1000);

                WFCSolver solverForMetroCheck = Manager.StreetSolver;
                if (solverForMetroCheck != null && !isOrganic)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < columns; c++)
                        {
                            TileDefinition tile = solverForMetroCheck.GetCollapsedTile(r, c);
                            bool hasRoad = false;
                            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
                            {
                                if (tile.GetSocket(dir) == RoadSockets.Road)
                                {
                                    hasRoad = true;
                                    break;
                                }
                            }

                            if (hasRoad)
                            {
                                MetroSolver.ApplyConstraint(r, c, new[] { "metro_empty" });
                            }
                        }
                    }
                }

                MetroSolver.Solve();

                RoadGraph metroGraph = RoadGraphExtractor.Extract(
                    MetroSolver, rows, columns, cellSize,
                    Manager.TerrainAdapter, RoadType.Metro, RoadSockets.Metro, yOffset: 1.0f);
                ProcessGraph(metroGraph, RoadType.Metro, Manager.MetroMaterial, forceFullConnectivity: false);
            }

            Debug.Log($"[SplineRoadGenerator] Done. Total objects: {_generated.Count}");
        }

        private void ProcessGraph(
            RoadGraph graph,
            RoadType type,
            Material material,
            bool forceFullConnectivity)
        {
            Debug.Log($"[SplineRoadGenerator] {type} graph: {graph.Nodes.Count} nodes, {graph.Edges.Count} edges.");

            float bht = forceFullConnectivity ? float.MaxValue : Manager.BridgeHeightThreshold;
            float tht = forceFullConnectivity ? float.MaxValue : Manager.TunnelHeightThreshold;
            var normalGraph = new RoadGraph();
            var bridgeGraph = new RoadGraph();
            var tunnelConns = new List<RoadConnection>();
            var normalMap = new Dictionary<RoadNode, RoadNode>();
            var bridgeMap = new Dictionary<RoadNode, RoadNode>();

            RoadNode NormalNode(RoadNode n)
            {
                if (!normalMap.TryGetValue(n, out var m))
                {
                    m = normalMap[n] = normalGraph.AddNode(n.Position, n.Type);
                }

                return m;
            }

            RoadNode BridgeNode(RoadNode n)
            {
                if (!bridgeMap.TryGetValue(n, out var m))
                {
                    m = bridgeMap[n] = bridgeGraph.AddNode(n.Position, n.Type);
                }

                return m;
            }

            var chains = graph.ExtractChains();

            foreach (var chain in chains)
            {
                if (chain.Count < 2) continue;

                ConnectionType chainType = ClassifyChain(chain, bht, tht, Manager.TerrainAdapter);

                switch (chainType)
                {
                    case ConnectionType.Tunnel:
                        var fromNode = new RoadNode(chain[0].Id, chain[0].Position, type);
                        var toNode = new RoadNode(chain[chain.Count - 1].Id, chain[chain.Count - 1].Position, type);
                        tunnelConns.Add(new RoadConnection(fromNode, toNode, ConnectionType.Tunnel));
                        break;

                    case ConnectionType.Bridge:
                        for (int i = 0; i < chain.Count - 1; i++)
                            bridgeGraph.AddEdge(BridgeNode(chain[i]), BridgeNode(chain[i + 1]), type);
                        break;

                    default:
                        for (int i = 0; i < chain.Count - 1; i++)
                            normalGraph.AddEdge(NormalNode(chain[i]), NormalNode(chain[i + 1]), type);
                        break;
                }
            }

            List<SplineContainer> normalContainers = RoadSplineBuilder.BuildSplines(
                normalGraph, transform, Manager.Morphology, graph.GetHashCode());
            SpawnSplines(normalContainers, type, material);

            if (bridgeGraph.Edges.Count > 0)
            {
                Material bm = Manager.BridgeMaterial != null ? Manager.BridgeMaterial : material;
                List<SplineContainer> bridgeContainers = RoadSplineBuilder.BuildSplines(
                    bridgeGraph, transform, Manager.Morphology, graph.GetHashCode() + 1);
                SpawnSplines(bridgeContainers, type, bm);
                Debug.Log($"[SplineRoadGenerator] {type}: {bridgeContainers.Count} bridge splines.");
            }
            foreach (var conn in tunnelConns)
            {
                Material tm = Manager.TunnelMaterial != null ? Manager.TunnelMaterial : material;
                var tunnelContainer = TunnelSplineBuilder.BuildTunnelSpline(conn, transform);
                tunnelContainer.gameObject.name = "TunnelSpline";
                var extruder = tunnelContainer.gameObject.AddComponent<RoadMeshExtruder>();
                extruder.RoadType = type;
                extruder.Resolution = Manager.MeshResolution;
                extruder.RoadMaterial = tm;
                extruder.Rebuild();
                _generated.Add(tunnelContainer.gameObject);

                Vector3 connDir = (conn.To.Position - conn.From.Position).normalized;
                float hw = RoadMeshExtruder.GetHalfWidth(type);
                float tunnelLen = Vector3.Distance(conn.From.Position, conn.To.Position);
                var hood = TunnelSplineBuilder.BuildPortalHood(
                    conn.From.Position, connDir, hw * 2f, tunnelLen, tm, transform);
                _generated.Add(hood);
            }

            Debug.Log($"[SplineRoadGenerator] {type}: {tunnelConns.Count} tunnel splines.");
            List<RoadConnection> connectorEdges = RoadGraphConnector.ConnectComponents(
                graph,
                bridgeHeightThreshold: bht,
                tunnelHeightThreshold: tht,
                terrain: Manager.TerrainAdapter,
                connectionsPerComponent: Manager.ConnectionsPerComponent
                );

            Debug.Log($"[SplineRoadGenerator] {type}: added {connectorEdges.Count} connector edges.");

            foreach (var conn in connectorEdges)
            {
                if (conn.ConnectionType == ConnectionType.Tunnel)
                {
                    Material tm = Manager.TunnelMaterial != null ? Manager.TunnelMaterial : material;
                    var tunnelContainer = TunnelSplineBuilder.BuildTunnelSpline(conn, transform);
                    tunnelContainer.gameObject.name = "TunnelSpline";
                    var extruder = tunnelContainer.gameObject.AddComponent<RoadMeshExtruder>();
                    extruder.RoadType = type;
                    extruder.Resolution = Manager.MeshResolution;
                    extruder.RoadMaterial = tm;
                    extruder.Rebuild();
                    _generated.Add(tunnelContainer.gameObject);

                    Vector3 connDir = (conn.To.Position - conn.From.Position).normalized;
                    float hw = RoadMeshExtruder.GetHalfWidth(type);
                    float tunnelLen = Vector3.Distance(conn.From.Position, conn.To.Position);
                    var hood = TunnelSplineBuilder.BuildPortalHood(
                        conn.From.Position, connDir, hw * 2f, tunnelLen, tm, transform);
                    _generated.Add(hood);
                }
                else
                {
                    var connGraph = new RoadGraph();
                    connGraph.AddEdge(
                        connGraph.AddNode(conn.From.Position, type),
                        connGraph.AddNode(conn.To.Position, type),
                        type);
                    Material connMaterial = conn.ConnectionType == ConnectionType.Bridge
                        ? (Manager.BridgeMaterial != null ? Manager.BridgeMaterial : material)
                        : material;
                    SpawnSplines(RoadSplineBuilder.BuildSplines(connGraph, transform), type, connMaterial);
                }
            }
        }

        private void SpawnSplines(List<SplineContainer> containers, RoadType type, Material material)
        {
            foreach (var container in containers)
            {
                container.gameObject.name = $"RoadSpline_{type}";
                var extruder = container.gameObject.AddComponent<RoadMeshExtruder>();
                extruder.RoadType = type;
                extruder.Resolution = Manager.MeshResolution;
                extruder.RoadMaterial = material;
                extruder.Rebuild();
                _generated.Add(container.gameObject);
            }
        }

        private static ConnectionType ClassifyChain(List<RoadNode> chain,
            float bridgeT,
            float tunnelT,
            Adapters.TerrainAdapter terrain)
        {
            Vector3 start = chain[0].Position;
            Vector3 end = chain[chain.Count - 1].Position;
            float totalLen = 0f;
            var cumDist = new float[chain.Count];
            for (int i = 1; i < chain.Count; i++)
            {
                totalLen += Vector3.Distance(chain[i - 1].Position, chain[i].Position);
                cumDist[i] = totalLen;
            }

            float maxDelta = 0f;
            float minDelta = 0f;

            for (int i = 1; i < chain.Count - 1; i++)
            {
                float t = totalLen > 0f ? cumDist[i] / totalLen : 0f;
                float roadY = Mathf.Lerp(start.y, end.y, t);
                float terrainH = terrain != null
                    ? terrain.SampleHeight(chain[i].Position.x, chain[i].Position.z)
                    : chain[i].Position.y;

                float delta = terrainH - roadY;
                if (delta > maxDelta) maxDelta = delta;
                if (delta < minDelta) minDelta = delta;
            }

            if (maxDelta > tunnelT) return ConnectionType.Tunnel;
            if (minDelta < -bridgeT) return ConnectionType.Bridge;
            return ConnectionType.Road;
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            foreach (var go in _generated)
            {
                if (go == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(go);
                else
#endif
                    Destroy(go);
            }
            _generated.Clear();
        }
    }
}
