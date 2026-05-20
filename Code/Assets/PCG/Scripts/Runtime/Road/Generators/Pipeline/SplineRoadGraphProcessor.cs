using System;
using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;
using Assets.Scripts.Runtime.Spline;

using UnityEngine;
using UnityEngine.Splines;

namespace Assets.Scripts.Runtime.Road.Generators
{
    internal sealed class SplineRoadGraphProcessor
    {
        private readonly CityManager _manager;
        private readonly Transform _root;
        private readonly RoadSettings _roadSettings;
        private readonly List<GameObject> _generated;

        internal SplineRoadGraphProcessor(
            CityManager manager,
            Transform root,
            RoadSettings roadSettings,
            List<GameObject> generated)
        {
            _manager = manager;
            _root = root;
            _roadSettings = roadSettings;
            _generated = generated;
        }

        internal void ProcessGraph(
            RoadGraph graph,
            RoadType type,
            Material material,
            Action<List<SplineContainer>, RoadType, Material, bool> spawnSplines,
            Func<SplineContainer, float, Vector3> getWorldPointOnSpline,
            Action<SplineContainer, RoadType> processOverlay,
            Action postCullOverlappingStreetSplines,
            Action removePropsOnRoundabouts,
            Action<SplineContainer, float> enqueueBoulevardStreetDecorOrNull = null)
        {
            _manager.ReportGenerationProgress("Optimizing road graph", 0.84f);

            HashSet<string> boulevardPriorityEdgeKeys = null;
            if (type == RoadType.Street &&
                _manager.GenerateBoulevard &&
                _manager.Nuclei != null &&
                _manager.Nuclei.Length >= 2)
            {
                boulevardPriorityEdgeKeys = BoulevardGenerator.BuildPriorityEdgeKeys(
                    graph,
                    _manager.Nuclei,
                    _manager.MetroBearingPenalty,
                    _manager.BoulevardLineCount);
            }

            SplineRoadGenerator.PruneAcuteEdgesOnlyAtHighDegreeIntersections(
                graph,
                _manager.MinRoadIntersectionAngleDegrees,
                minDegreeToPrune: 5,
                neverPruneEdgeKeys: boulevardPriorityEdgeKeys);

            HashSet<RoadNode> boulevardProtectedPathNodes = null;
            if (type == RoadType.Street &&
                _manager.GenerateBoulevard &&
                _manager.Nuclei != null &&
                _manager.Nuclei.Length >= 2)
            {
                boulevardProtectedPathNodes = BoulevardGenerator.CollectBoulevardPathNodes(
                    graph,
                    _manager.Nuclei,
                    _manager.MetroBearingPenalty,
                    _manager.BoulevardLineCount);
            }

            int originalEdgeCount = graph.Edges.Count;

            List<RoadConnection> connectorEdges = RoadGraphConnector.ConnectComponents(
                graph,
                _manager.BridgeHeightThreshold,
                _manager.TunnelHeightThreshold,
                _manager.TerrainAdapter,
                _manager.ConnectionsPerComponent,
                boulevardProtectedPathNodes);

            _manager.ReportGenerationProgress("Extruding street meshes", 0.885f);
            var originalGraph = graph.SubgraphUpToEdge(originalEdgeCount);
            if (type == RoadType.Street &&
                _manager.GenerateBoulevard &&
                _manager.Nuclei != null &&
                _manager.Nuclei.Length >= 2 &&
                boulevardPriorityEdgeKeys != null &&
                boulevardPriorityEdgeKeys.Count > 0)
            {
                originalGraph.RetainEdges(e =>
                    !boulevardPriorityEdgeKeys.Contains(RoadGraphKeyUtility.ToEdgeKey(e.From.Position, e.To.Position)));
            }

            List<SplineContainer> containers = RoadSplineBuilder.BuildSplines(
                originalGraph,
                _root,
                _roadSettings,
                _manager.EffectiveSplineMorphology,
                graph.GetHashCode());
            _manager.ReportGenerationProgress("Building road splines", 0.892f);
            spawnSplines(containers, type, material, false);

            foreach (var conn in connectorEdges)
            {
                var connGraph = new RoadGraph();
                connGraph.AddEdge(
                    connGraph.AddNode(conn.From.Position, type),
                    connGraph.AddNode(conn.To.Position, type),
                    type);
                if (boulevardPriorityEdgeKeys != null &&
                    boulevardPriorityEdgeKeys.Contains(RoadGraphKeyUtility.ToEdgeKey(conn.From.Position, conn.To.Position)))
                {
                    continue;
                }
                spawnSplines(
                    RoadSplineBuilder.BuildSplines(connGraph, _root, _roadSettings),
                    type, material, true);
            }

            if (type == RoadType.Street &&
                _manager.GenerateBoulevard &&
                _manager.Nuclei != null &&
                _manager.Nuclei.Length >= 2)
            {
                _manager.ReportGenerationProgress("Generating boulevards between nuclei", 0.90f);
                List<SplineContainer> boulevardContainers = BoulevardGenerator.Generate(
                    graph,
                    _manager.Nuclei,
                    _root,
                    _roadSettings,
                    _manager.EffectiveSplineMorphology,
                    _manager.MetroBearingPenalty,
                    _manager.BoulevardLineCount);

                for (int i = 0; i < boulevardContainers.Count; i++)
                {
                    _manager.ReportGenerationProgress(
                        "Placing boulevard props",
                        Mathf.Lerp(0.905f, 0.915f, boulevardContainers.Count <= 1 ? 1f : (float)i / (boulevardContainers.Count - 1)));
                    var container = boulevardContainers[i];
                    container.gameObject.name = "RoadSpline_Boulevard";
                    var extruder = container.gameObject.AddComponent<RoadMeshExtruder>();
                    extruder.RoadType = RoadType.Street;
                    extruder.RoadSettings = _roadSettings;
                    extruder.Resolution = _manager.MeshResolution;
                    NeighborhoodStyleSample boulevardStyle = NeighborhoodStyleEvaluator.Evaluate(
                        getWorldPointOnSpline(container, 0.5f),
                        _manager.Nuclei);
                    extruder.RoadMaterial = boulevardStyle.BoulevardMaterial;
                    extruder.WidthMultiplier = _manager.BoulevardWidthMultiplier;
                    extruder.MeshVerticalOffset = _manager.RoadMeshVerticalOffset;
                    extruder.LaneCount = 4;
                    extruder.Rebuild();
                    if (enqueueBoulevardStreetDecorOrNull != null)
                    {
                        enqueueBoulevardStreetDecorOrNull(
                            container,
                            _manager.BoulevardWidthMultiplier);
                    }
                    else
                    {
                        StreetDecorationGenerator.AddDecorations(
                            container,
                            RoadType.Street,
                            _manager,
                            _roadSettings,
                            _manager.BoulevardWidthMultiplier,
                            includeSidewalks: false);
                    }

                    _generated.Add(container.gameObject);
                    processOverlay(container, RoadType.Street);
                }
            }

            if (type == RoadType.Street)
            {
                _manager.ReportGenerationProgress("Culling overlapping street splines", 0.918f);
                postCullOverlappingStreetSplines();
                _manager.ReportGenerationProgress("Smoothing street intersections", 0.9185f);
                IntersectionRoundaboutGenerator.Spawn(
                    graph,
                    _root,
                    _manager,
                    _roadSettings,
                    boulevardPriorityEdgeKeys,
                    _manager.BoulevardWidthMultiplier,
                    _generated);
                _manager.ReportGenerationProgress("Adding dead-end roundabout caps", 0.919f);
                DeadEndRoundaboutGenerator.SpawnCaps(
                    graph,
                    _root,
                    _manager,
                    _roadSettings,
                    boulevardPriorityEdgeKeys,
                    _manager.BoulevardWidthMultiplier,
                    _generated);
                removePropsOnRoundabouts();
            }
        }
    }
}
