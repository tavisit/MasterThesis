using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;
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
        private readonly MetroEntrancePlacer _metroEntrancePlacer;
        private readonly SplineRoadGraphProcessor _graphProcessor;

        private readonly List<GameObject> _generated = new();
        private readonly HashSet<Vector2Int> _placedStationCells = new();
        private readonly HashSet<Vector2Int> _placedEntranceCells = new();

        public SplineRoadGenerator(CityManager manager)
        {
            _manager = manager;
            _root = manager.transform;
            _roadSettings = manager.RoadSettings;
            _overlay = manager.GetComponent<RoadOverlayGenerator>();
            _metroEntrancePlacer = new MetroEntrancePlacer(_manager, _root, _roadSettings, _placedEntranceCells);
            _graphProcessor = new SplineRoadGraphProcessor(_manager, _root, _roadSettings, _generated);
        }

        public void Generate()
        {
            _manager.ReportGenerationProgress("Preparing spline generation", 0.76f);
            _placedStationCells.Clear();
            _placedEntranceCells.Clear();
            Clear();
            _manager.ReportGenerationProgress("Clearing previous generated objects", 0.775f);
            _overlay?.ResetPlacedCells();

            int rows = _manager.Rows;
            int columns = _manager.Columns;
            float cellSize = _manager.CellSize;

            WFCSolver streetSolver = _manager.StreetSolver;
            VoronoiWFCSolver voronoiSolver = _manager.VoronoiStreetSolver;
            bool useVoronoiStreetGraph = _manager.UsesVoronoiStreetGraph && voronoiSolver != null;

            RoadGraph streetGraph = null;

            if (_manager.GenerateStreets &&
                (streetSolver != null || voronoiSolver != null))
            {
                _manager.ReportGenerationProgress("Extracting street graph", 0.80f);
                streetGraph = useVoronoiStreetGraph
                    ? VoronoiRoadGraphExtractor.Extract(
                        voronoiSolver, _manager.TerrainAdapter, RoadType.Street)
                    : RoadGraphExtractor.Extract(
                        streetSolver, rows, columns, cellSize,
                        _manager.TerrainAdapter, RoadType.Street, SocketDefinitions.Road);

                _manager.SetStreetIntersections(
                    RoadIntersectionExtractor.Extract(streetGraph, _manager.MinRoadIntersectionAngleDegrees));
                ProcessGraph(streetGraph, RoadType.Street, null);
            }

            if (_manager.GenerateMetro &&
                _manager.Nuclei != null && _manager.Nuclei.Length >= 2 &&
                streetGraph != null)
            {
                _manager.ReportGenerationProgress("Routing metro lines between nuclei", 0.93f);
                List<SplineContainer> metroContainers = MetroGenerator.Generate(
                    streetGraph,
                    _manager.Nuclei,
                    _root,
                    _manager.TerrainAdapter,
                    _manager.EffectiveSplineMorphology,
                    _manager.MetroBearingPenalty,
                    _manager.MetroLineCount);

                for (int i = 0; i < metroContainers.Count; i++)
                {
                    _manager.ReportGenerationProgress(
                        "Generating metro meshes",
                        Mathf.Lerp(0.935f, 0.965f, metroContainers.Count <= 1 ? 1f : (float)i / (metroContainers.Count - 1)));
                    var container = metroContainers[i];
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

                _manager.ReportGenerationProgress("Placing metro stations", 0.972f);
                _manager.ReportGenerationProgress("Finalizing metro overlays", 0.98f);
                Debug.Log($"[SplineRoadGenerator] Metro: {metroContainers.Count} lines generated.");
            }
            else if (_manager.GenerateMetro)
            {
                Debug.LogWarning("[SplineRoadGenerator] Metro skipped: requires streets " +
                                 "and at least 2 nuclei to route through.");
            }

            _manager.ReportGenerationProgress("Rebuilding sidewalks", 0.915f);
            StreetDecorationGenerator.RebuildAllSidewalks(_root, _manager, _roadSettings);
            _manager.ReportGenerationProgress("Placing props", 0.925f);

            Debug.Log($"[SplineRoadGenerator] Done. Total objects: {_generated.Count}");
        }

        public void Clear()
        {
            foreach (var go in _generated)
            {
                DestroyGameObject(go);
            }
            _generated.Clear();
        }


        private void ProcessGraph(RoadGraph graph, RoadType type, Material material)
        {
            _graphProcessor.ProcessGraph(
                graph,
                type,
                material,
                (containers, rt, mat, forceDecor) => SpawnSplines(containers, rt, mat, forceDecor),
                RemoveOverlappingStreetSplineMesh,
                GetWorldPointOnSpline,
                ProcessOverlay,
                PostCullOverlappingStreetSplines,
                RemovePropsOnRoundabouts);
        }

        private void RemoveOverlappingStreetSplineMesh(SplineContainer boulevard)
        {
            if (boulevard == null || boulevard.Spline == null)
            {
                return;
            }

            var toRemove = new List<GameObject>();
            Vector3 bStart = GetWorldPointOnSpline(boulevard, 0f);
            Vector3 bEnd = GetWorldPointOnSpline(boulevard, 1f);
            Vector3 bMid = GetWorldPointOnSpline(boulevard, 0.5f);
            float bLength = boulevard.Spline.GetLength();

            foreach (var go in _generated)
            {
                if (go == null || go.name != "RoadSpline_Street")
                {
                    continue;
                }

                var street = go.GetComponent<SplineContainer>();
                if (street == null || street.Spline == null)
                {
                    continue;
                }

                float sLength = street.Spline.GetLength();
                if (Mathf.Abs(sLength - bLength) > 1.0f)
                {
                    continue;
                }

                Vector3 sStart = GetWorldPointOnSpline(street, 0f);
                Vector3 sEnd = GetWorldPointOnSpline(street, 1f);
                Vector3 sMid = GetWorldPointOnSpline(street, 0.5f);

                bool sameDirection =
                    Vector3.Distance(sStart, bStart) < 0.6f &&
                    Vector3.Distance(sEnd, bEnd) < 0.6f;
                bool reverseDirection =
                    Vector3.Distance(sStart, bEnd) < 0.6f &&
                    Vector3.Distance(sEnd, bStart) < 0.6f;

                if (!(sameDirection || reverseDirection))
                {
                    continue;
                }

                if (Vector3.Distance(sMid, bMid) < 0.8f)
                {
                    toRemove.Add(go);
                }
            }

            foreach (var go in toRemove)
            {
                DestroyGameObject(go);
                _generated.Remove(go);
            }
        }

        private static Vector3 GetWorldPointOnSpline(SplineContainer container, float t)
        {
            container.Spline.Evaluate(t, out var pos, out _, out _);
            return container.transform.TransformPoint((Vector3)pos);
        }

        private void PostCullOverlappingStreetSplines()
        {
            var candidates = new List<(GameObject go, SplineContainer sc, int priority)>();
            foreach (var go in _generated)
            {
                if (go == null)
                {
                    continue;
                }

                int prio = go.name switch
                {
                    "RoadSpline_Boulevard" => 2,
                    "RoadSpline_Street" => 1,
                    _ => 0
                };
                if (prio == 0)
                {
                    continue;
                }

                var sc = go.GetComponent<SplineContainer>();
                if (sc == null || sc.Spline == null || sc.Spline.Count < 2)
                {
                    continue;
                }

                candidates.Add((go, sc, prio));
            }

            if (candidates.Count < 2)
            {
                return;
            }

            var toDelete = new HashSet<GameObject>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (toDelete.Contains(candidates[i].go))
                {
                    continue;
                }

                for (int j = i + 1; j < candidates.Count; j++)
                {
                    if (toDelete.Contains(candidates[j].go))
                    {
                        continue;
                    }

                    if (!AreSplinesNearDuplicate(candidates[i].sc, candidates[j].sc))
                    {
                        continue;
                    }

                    if (candidates[i].priority > candidates[j].priority)
                    {
                        toDelete.Add(candidates[j].go);
                    }
                    else if (candidates[j].priority > candidates[i].priority)
                    {
                        toDelete.Add(candidates[i].go);
                    }
                    else
                    {
                        float li = candidates[i].sc.Spline.GetLength();
                        float lj = candidates[j].sc.Spline.GetLength();
                        toDelete.Add(li <= lj ? candidates[i].go : candidates[j].go);
                    }
                }
            }

            foreach (var go in toDelete)
            {
                DestroyGameObject(go);
                _generated.Remove(go);
            }

            if (toDelete.Count > 0)
            {
                Debug.Log($"[SplineRoadGenerator] Culled {toDelete.Count} overlapping street/boulevard splines.");
            }
        }

        private static bool AreSplinesNearDuplicate(SplineContainer a, SplineContainer b)
        {
            float lenA = a.Spline.GetLength();
            float lenB = b.Spline.GetLength();
            if (lenA <= 0.1f || lenB <= 0.1f)
            {
                return false;
            }

            float maxLen = Mathf.Max(lenA, lenB);
            float minLen = Mathf.Min(lenA, lenB);
            if (minLen / maxLen < 0.45f)
            {
                return false;
            }

            Vector3 a0 = GetWorldPointOnSpline(a, 0f);
            Vector3 a1 = GetWorldPointOnSpline(a, 1f);
            Vector3 b0 = GetWorldPointOnSpline(b, 0f);
            Vector3 b1 = GetWorldPointOnSpline(b, 1f);
            Vector3 dirA = (a1 - a0).normalized;
            Vector3 dirB = (b1 - b0).normalized;
            float align = Mathf.Max(Vector3.Dot(dirA, dirB), Vector3.Dot(dirA, -dirB));
            if (align < 0.86f)
            {
                return false;
            }

            const int samples = 31;
            var ptsA = SampleSplinePoints(a, samples);
            var ptsB = SampleSplinePoints(b, samples);
            if (ptsA.Count < 4 || ptsB.Count < 4)
            {
                return false;
            }

            int nearMatchesForward = 0;
            int nearMatchesReverse = 0;
            float sumForward = 0f;
            float sumReverse = 0f;
            for (int i = 0; i < ptsA.Count; i++)
            {
                Vector3 pa = ptsA[i];
                Vector3 pbF = ptsB[i];
                Vector3 pbR = ptsB[ptsB.Count - 1 - i];
                float dF = Vector3.Distance(pa, pbF);
                float dR = Vector3.Distance(pa, pbR);
                sumForward += dF;
                sumReverse += dR;
                if (dF < 1.6f)
                {
                    nearMatchesForward++;
                }
                if (dR < 1.6f)
                {
                    nearMatchesReverse++;
                }
            }

            int bestDirect = Mathf.Max(nearMatchesForward, nearMatchesReverse);
            float meanBest = Mathf.Min(sumForward, sumReverse) / ptsA.Count;
            if (bestDirect >= Mathf.CeilToInt(samples * 0.68f) && meanBest < 1.55f)
            {
                return true;
            }

            var shorter = lenA <= lenB ? ptsA : ptsB;
            var longer = lenA <= lenB ? ptsB : ptsA;

            int overlapHits = 0;
            for (int i = 0; i < shorter.Count; i++)
            {
                float d = MinDistanceToSamples(shorter[i], longer);
                if (d < 1.8f)
                {
                    overlapHits++;
                }
            }

            float overlapRatio = overlapHits / (float)shorter.Count;
            return overlapRatio >= 0.64f;
        }

        private static List<Vector3> SampleSplinePoints(SplineContainer s, int samples)
        {
            var list = new List<Vector3>(samples);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                list.Add(GetWorldPointOnSpline(s, t));
            }
            return list;
        }

        private static float MinDistanceToSamples(Vector3 p, List<Vector3> samples)
        {
            float best = float.MaxValue;
            for (int i = 0; i < samples.Count; i++)
            {
                float d = Vector3.Distance(p, samples[i]);
                if (d < best)
                {
                    best = d;
                }
            }
            return best;
        }

        private void RemovePropsOnRoundabouts()
        {
            List<GameObject> toDelete = RoundaboutPropCleanupUtility.FindPropsIntersectingRoundabouts(_root);
            for (int i = 0; i < toDelete.Count; i++)
            {
                DestroyGameObject(toDelete[i]);
            }
        }

        private static void DestroyGameObject(GameObject go)
        {
            if (go == null)
            {
                return;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(go);
                return;
            }
#endif
            Object.Destroy(go);
        }
        internal static void PruneAcuteEdgesOnlyAtHighDegreeIntersections(
            RoadGraph graph,
            float minAngleDegrees,
            int minDegreeToPrune)
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
                if (edges.Count < minDegreeToPrune)
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
                Debug.Log($"[SplineRoadGenerator] Pruned {toRemove.Count} acute-angle edges at intersections with degree >= {minDegreeToPrune}.");
                graph.RetainEdges(e => !toRemove.Contains(e));
            }
        }

        private void SpawnSplines(
            List<SplineContainer> containers,
            RoadType type,
            Material material,
            bool forceStreetDecor = false)
        {
            for (int i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                container.gameObject.name = $"RoadSpline_{type}";
                var extruder = container.gameObject.AddComponent<RoadMeshExtruder>();
                extruder.RoadType = type;
                extruder.Resolution = _manager.MeshResolution;
                Material resolvedMaterial = material;
                float widthMultiplier = 1f;
                if (type == RoadType.Street)
                {
                    Vector3 styleSamplePos = GetWorldPointOnSpline(container, 0.5f);
                    NeighborhoodStyleSample style = NeighborhoodStyleEvaluator.Evaluate(styleSamplePos, _manager.Nuclei);
                    widthMultiplier = Mathf.Max(0.01f, style.RoadWidthMultiplier);
                    resolvedMaterial = style.RoadMaterial;
                }

                extruder.RoadMaterial = resolvedMaterial;
                extruder.WidthMultiplier = widthMultiplier;
                extruder.LaneCount = type == RoadType.Street
                    ? (widthMultiplier < 1f ? 0 : 2)
                    : 1;
                if (type == RoadType.Street)
                {
                    extruder.MeshVerticalOffset = _manager.RoadMeshVerticalOffset;
                }
                extruder.Rebuild();
                if (_manager.GenerateStreetDecor &&
                    (forceStreetDecor || type == RoadType.Street))
                {
                    _manager.ReportGenerationProgress(
                        "Placing props",
                        Mathf.Lerp(0.895f, 0.905f, containers.Count <= 1 ? 1f : (float)i / (containers.Count - 1)));
                }
                StreetDecorationGenerator.AddDecorations(
                    container,
                    type,
                    _manager,
                    _roadSettings,
                    widthMultiplier,
                    forceStreetDecor,
                    includeSidewalks: false);
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
            float belowTerrain = terrainH - railH;

            if (!_metroEntrancePlacer.IsPointUnderStreet(railPos))
            {
                return;
            }

            if (!forced)
            {
                if (belowTerrain < 2.5f)
                {
                    return;
                }

                if (belowTerrain > 35f)
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

            var go = new GameObject(forced ? "MetroStation_Terminal" : "MetroStation");
            go.transform.SetParent(stationsParent.transform, false);
            go.transform.position = railPos;

            if (tangent.sqrMagnitude > 0.001f)
            {
                go.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            }

            BuildStationSlices(
                go.transform,
                Mathf.Max(2.5f, platformW * 0.85f),
                platformW,
                platformL,
                _manager.MetroStationMaterial);
            _metroEntrancePlacer.PlaceStationEntrances(railPos, tangent, stationsParent.transform);
        }

        private static void BuildStationSlices(
            Transform parent,
            float totalHeight,
            float baseWidth,
            float baseLength,
            Material material)
        {
            int sliceCount = Mathf.Max(3, Mathf.CeilToInt(totalHeight / 0.45f));
            float sliceHeight = totalHeight / sliceCount;

            for (int i = 0; i < sliceCount; i++)
            {
                float t = sliceCount == 1 ? 1f : i / (float)(sliceCount - 1);
                float widthScale = Mathf.Lerp(1.0f, 0.88f, t);
                float lengthScale = Mathf.Lerp(1.0f, 0.92f, t);

                GameObject slice = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slice.name = "MetroStationSlice";
                slice.transform.SetParent(parent, false);
                slice.transform.localPosition = new Vector3(
                    0f,
                    sliceHeight * (i + 0.5f),
                    0f);
                slice.transform.localScale = new Vector3(
                    baseWidth * widthScale,
                    sliceHeight * 1.001f,
                    baseLength * lengthScale);

                var renderer = slice.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }

                var collider = slice.GetComponent<Collider>();
                if (collider != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        Object.DestroyImmediate(collider);
                    }
                    else
#endif
                    {
                        Object.Destroy(collider);
                    }
                }
            }
        }
    }
}
