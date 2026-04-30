using System.Collections.Generic;
using System.Linq;

using Assets.Scripts.Runtime.Adapters;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.Road.Generators;
using Assets.Scripts.Runtime.Voronoi;
using Assets.Scripts.Runtime.WFC;

using UnityEngine;

namespace Assets.Scripts.Runtime.City
{
    public enum CityGenerationMode
    {
        SingleMorphology = 0,
        VoronoiSpatialHybrid = 1,
    }

    public sealed class CityManager : MonoBehaviour
    {
        [Header("Generation Mode")]
        [Tooltip("Single Morphology: grid or organic (Voronoi) WFC. Voronoi Spatial Hybrid: Voronoi + weighted tile families; blend near/far from nuclei.")]
        [SerializeField] private CityGenerationMode _generationMode = CityGenerationMode.SingleMorphology;

        [Header("Grid")]
        [SerializeField] private int _rows = 20;
        [SerializeField] private int _columns = 20;
        [SerializeField] private float _cellSize = 10f;

        [Header("Morphology")]
        [Tooltip("Single Morphology only. Organic uses Voronoi cells + WFC. Ignored when using Voronoi Spatial Hybrid.")]
        [SerializeField] private UrbanMorphology _morphology = UrbanMorphology.Grid;
        [Tooltip("Voronoi Spatial Hybrid: baseline grid vs organic tile bias; blended with nucleus distance when Spatial Influence is high.")]
        [SerializeField][Range(0f, 1f)] private float _morphologyBlend = 0.5f;

        [Header("Voronoi spatial hybrid")]
        [Tooltip("Only for Voronoi Spatial Hybrid mode. Organic near nuclei -> grid far, or the inverse.")]
        [SerializeField] private SpatialMorphologyGradient _spatialGradient = SpatialMorphologyGradient.OrganicNearNuclei_GridFar;
        [Tooltip("World units beyond nucleus edge over which tile-family bias fades to the far value.")]
        [SerializeField] private float _nucleusFalloffWorld = 80f;
        [Tooltip("0 = ignore nuclei (use Morphology Blend only everywhere). 1 = full nucleus distance gradient.")]
        [SerializeField][Range(0f, 1f)] private float _spatialInfluence = 1f;
        [Tooltip("Extra geometry blend for Voronoi Spatial Hybrid: 0 keeps pure Voronoi sites, 1 strongly snaps far-from-organic regions toward the grid.")]
        [SerializeField][Range(0f, 1f)] private float _gridTopologyInfluence = 0.65f;

        [Header("Solver")]
        [SerializeField] private int _seed = 0;
        [SerializeField] private int _maxBacktracks = 1000;

        [Header("Nuclei")]
        [SerializeField] private CityNucleus[] _nuclei;

        [Header("Voronoi (Single Morphology + Organic only)")]
        [SerializeField] private int _voronoiResolution = 256;
        [SerializeField] private float _voronoiCellSize = 40f;

        [Header("Rendering")]
        [SerializeField] private Material _metroMaterial;
        [SerializeField] private Material _metroStationMaterial;
        [SerializeField] private bool _generateStreetDecor = true;

        [Header("Street")]
        [SerializeField] private bool _generateStreets = true;
        [SerializeField] private int _connectionsPerComponent = 2;

        [Header("Boulevard")]
        [Tooltip("Surface boulevards routed between nuclei on a minimum spanning tree (about N-1 lines for N centers).")]
        [SerializeField] private bool _generateBoulevard = true;
        [SerializeField] private int _boulevardLineCount = 1;
        [Tooltip("Road mesh width vs normal streets (1 = same half-width as RoadSettings). Use 2 for a double-width boulevard.")]
        [SerializeField] private float _boulevardWidthMultiplier = 2f;

        [Header("Metro")]
        [SerializeField] private bool _generateMetro = true;
        [SerializeField] private int _metroLineCount = 1;
        [SerializeField][Range(0f, 2f)] private float _metroBearingPenalty = 0.6f;
        [SerializeField] private float _metroStationInterval = 80f;

        [Header("Terrain")]
        [SerializeField] private TerrainAdapter _terrainAdapter;

        [Header("Mesh")]
        [SerializeField] private int _meshResolution = 20;
        [SerializeField][Range(5f, 85f)] private float _minRoadIntersectionAngleDegrees = 32f;
        [SerializeField] private float _bridgeHeightThreshold = 1f;
        [SerializeField] private float _tunnelHeightThreshold = 1f;
        [SerializeField] private RoadSettings _roadSettings;

        [Header("Debug")]
        [SerializeField] private bool _showStreetIntersections = true;
        [SerializeField] private float _intersectionGizmoRadius = 1.2f;
        [SerializeField] private float _intersectionDirectionGizmoLength = 5f;

        private int VoronoiSiteCount
        {
            get
            {
                float worldW = _columns * _cellSize;
                float worldH = _rows * _cellSize;
                int countW = Mathf.Max(1, Mathf.RoundToInt(worldW / _voronoiCellSize));
                int countH = Mathf.Max(1, Mathf.RoundToInt(worldH / _voronoiCellSize));
                return countW * countH;
            }
        }

        public CityGenerationMode GenerationMode => _generationMode;
        public RoadSettings RoadSettings => _roadSettings;
        public float MetroStationInterval => _metroStationInterval;
        public Material MetroStationMaterial => _metroStationMaterial;
        public int Rows => _rows;
        public int Columns => _columns;
        public float CellSize => _cellSize;
        public UrbanMorphology Morphology => _morphology;
        public float MorphologyBlend => _morphologyBlend;
        public SpatialMorphologyGradient SpatialGradient => _spatialGradient;
        public float NucleusFalloffWorld => _nucleusFalloffWorld;
        public float SpatialInfluence => _spatialInfluence;
        public float GridTopologyInfluence => _gridTopologyInfluence;

        public UrbanMorphology EffectiveSplineMorphology =>
            _generationMode == CityGenerationMode.VoronoiSpatialHybrid
                ? (_spatialGradient == SpatialMorphologyGradient.OrganicNearNuclei_GridFar
                    ? UrbanMorphology.Grid
                    : (_morphologyBlend < 0.5f ? UrbanMorphology.Grid : UrbanMorphology.Organic))
                : _morphology;

        public bool UsesVoronoiStreetGraph =>
            _generationMode == CityGenerationMode.VoronoiSpatialHybrid ||
            (_generationMode == CityGenerationMode.SingleMorphology && _morphology == UrbanMorphology.Organic);
        public int Seed => _seed;
        public int MaxBacktracks => _maxBacktracks;
        public CityNucleus[] Nuclei => _nuclei;
        public int VoronoiResolution => _voronoiResolution;
        public bool GenerateStreets => _generateStreets;
        public bool GenerateMetro => _generateMetro;
        public int MetroLineCount => _metroLineCount;
        public float MetroBearingPenalty => _metroBearingPenalty;
        public int ConnectionsPerComponent => _connectionsPerComponent;
        public bool GenerateBoulevard => _generateBoulevard;
        public int BoulevardLineCount => _boulevardLineCount;
        public float BoulevardWidthMultiplier => _boulevardWidthMultiplier;
        public int MeshResolution => _meshResolution;
        public float MinRoadIntersectionAngleDegrees => _minRoadIntersectionAngleDegrees;
        public float BridgeHeightThreshold => _bridgeHeightThreshold;
        public float TunnelHeightThreshold => _tunnelHeightThreshold;
        public float RoadMeshVerticalOffset => RoadGenerationOffsets.RoadMeshVerticalOffset;
        public float SidewalkMeshVerticalOffset => RoadGenerationOffsets.SidewalkMeshVerticalOffset;
        public Material MetroMaterial => _metroMaterial;
        public TerrainAdapter TerrainAdapter => _terrainAdapter;
        public bool GenerateStreetDecor => _generateStreetDecor;
        public string GenerationStage => _generationStage;
        public float GenerationProgress => _generationProgress;
        public IReadOnlyList<RoadIntersectionInfo> StreetIntersections => _streetIntersections;

        public WFCSolver StreetSolver { get; private set; }
        public VoronoiWFCSolver VoronoiStreetSolver { get; private set; }
        public SolveResult LastResult { get; private set; }
        public int CollapseCount =>
            StreetSolver?.CollapseCount ?? VoronoiStreetSolver?.CollapseCount ?? 0;
        public int BacktrackCount =>
            StreetSolver?.BacktrackCount ?? VoronoiStreetSolver?.BacktrackCount ?? 0;

        private SplineRoadGenerator _splineGenerator;
        private string _generationStage = "Idle";
        private float _generationProgress;
        private List<RoadIntersectionInfo> _streetIntersections = new();

        private SplineRoadGenerator SplineGenerator =>
            _splineGenerator ??= new SplineRoadGenerator(this);

        [ContextMenu("Generate City")]
        public void Generate()
        {
            SetGenerationProgress("Clearing previous city", 0.03f);
            ClearGenerated();

            try
            {
                if (_generationMode == CityGenerationMode.VoronoiSpatialHybrid)
                {
                    SetGenerationProgress("Solving Voronoi spatial hybrid WFC", 0.18f);
                    SolveVoronoiSpatialHybrid();
                }
                else
                {
                    SetGenerationProgress("Solving street WFC", 0.18f);
                    SolveStreets();
                }

                if (LastResult == SolveResult.Success)
                {
                    SetGenerationProgress("Building road and metro meshes", 0.72f);
                    SplineGenerator.Generate();
                    SetGenerationProgress("Generation complete", 1f);
                }
                else
                {
                    SetGenerationProgress("Generation failed", 1f);
                }
            }
            finally
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.ClearProgressBar();
                }
#endif
            }
        }

        [ContextMenu("Clear City")]
        public void Clear()
        {
            ClearGenerated();
            SplineGenerator.Clear();
        }

        public void SolveStreets()
        {
            TileSet tileSet = RoadTileSetFactory.CreateStreet(_morphology);

            if (_morphology == UrbanMorphology.Organic)
            {
                Debug.Log("[CityManager] Running Voronoi WFC for organic morphology.");
                SolveOrganicVoronoi(tileSet);
            }
            else
            {
                Debug.Log("[CityManager] Running grid WFC for grid morphology.");
                SolveGridSingle(tileSet);
            }
        }

        private void SolveVoronoiSpatialHybrid()
        {
            float worldW = _columns * _cellSize;
            float worldH = _rows * _cellSize;

            float gridBias = 1.0f - _morphologyBlend;
            SetGenerationProgress("Preparing hybrid tile set", 0.24f);
            TileSet hybridTileSet = HybridTileSetFactory.CreateHybridStreet(gridBias);

            SetGenerationProgress("Generating Voronoi cells", 0.30f);
            Vector2[] sites = CityVoronoiSiteGenerator.GenerateSites(
                VoronoiSiteCount,
                worldW,
                worldH,
                applySpatialGridSnap: true,
                GetVoronoiSiteGeneratorConfig());
            var cells = VoronoiGenerator.Generate(sites, worldW, worldH, _voronoiResolution);

            SetGenerationProgress("Computing nucleus spatial bias", 0.38f);
            float[] organicBias = SpatialMorphologyBias.ComputeOrganicBiasPerCell(
                cells,
                _nuclei,
                _spatialGradient,
                _nucleusFalloffWorld,
                _morphologyBlend,
                _spatialInfluence);

            StreetSolver = null;
            VoronoiStreetSolver = new VoronoiWFCSolver(
                hybridTileSet,
                cells,
                _seed,
                _maxBacktracks,
                organicBiasPerCell: organicBias);

            Debug.Log("[CityManager] Voronoi spatial hybrid | " +
                      $"cells: {cells.Count} | gradient: {_spatialGradient} | falloff: {_nucleusFalloffWorld:F0}");

            if (_nuclei != null && _nuclei.Length > 0)
            {
                NucleusConstraintApplier.ApplyVoronoi(VoronoiStreetSolver, _nuclei);
            }

            if (_terrainAdapter != null)
            {
                _terrainAdapter.ApplyTerrainConstraintsVoronoi(VoronoiStreetSolver);
            }

            SetGenerationProgress("Running Voronoi WFC collapse", 0.56f);
            var result = VoronoiStreetSolver.Solve(onProgress: p =>
                SetGenerationProgress("Running Voronoi WFC collapse", Mathf.Lerp(0.56f, 0.72f, p)));
            LastResult = result == SolveResult.Success ? SolveResult.Success : SolveResult.Failure;
            Debug.Log($"[CityManager] Voronoi spatial hybrid WFC {LastResult} | " +
                      $"Collapses: {CollapseCount} | Backtracks: {BacktrackCount}");
        }

        private void SolveGridSingle(TileSet tileSet)
        {
            VoronoiStreetSolver = null;
            SetGenerationProgress("Preparing grid WFC", 0.26f);
            StreetSolver = new WFCSolver(tileSet, _rows, _columns, _seed, _maxBacktracks);

            if (_nuclei != null && _nuclei.Length > 0)
            {
                NucleusConstraintApplier.Apply(StreetSolver, _nuclei, _rows, _columns, _cellSize);
            }

            if (_terrainAdapter != null)
            {
                _terrainAdapter.ApplyTerrainConstraints(StreetSolver, _rows, _columns, _cellSize);
            }

            SetGenerationProgress("Running grid WFC collapse", 0.56f);
            LastResult = StreetSolver.Solve(onProgress: p =>
                SetGenerationProgress("Running grid WFC collapse", Mathf.Lerp(0.56f, 0.72f, p)));
            Debug.Log($"[CityManager] Street WFC {LastResult} | " +
                      $"Collapses: {CollapseCount} | Backtracks: {BacktrackCount}");
        }

        private void SolveOrganicVoronoi(TileSet tileSet)
        {
            float worldW = _columns * _cellSize;
            float worldH = _rows * _cellSize;

            SetGenerationProgress("Generating Voronoi cells", 0.30f);
            Vector2[] sites = CityVoronoiSiteGenerator.GenerateSites(
                VoronoiSiteCount,
                worldW,
                worldH,
                applySpatialGridSnap: false,
                GetVoronoiSiteGeneratorConfig());
            var cells = VoronoiGenerator.Generate(sites, worldW, worldH, _voronoiResolution);

            StreetSolver = null;
            VoronoiStreetSolver = new VoronoiWFCSolver(tileSet, cells, _seed, _maxBacktracks);

            if (_nuclei != null && _nuclei.Length > 0)
            {
                NucleusConstraintApplier.ApplyVoronoi(VoronoiStreetSolver, _nuclei);
            }

            if (_terrainAdapter != null)
            {
                _terrainAdapter.ApplyTerrainConstraintsVoronoi(VoronoiStreetSolver);
            }

            SetGenerationProgress("Running Voronoi WFC collapse", 0.56f);
            var result = VoronoiStreetSolver.Solve(onProgress: p =>
                SetGenerationProgress("Running Voronoi WFC collapse", Mathf.Lerp(0.56f, 0.72f, p)));
            LastResult = result == SolveResult.Success ? SolveResult.Success : SolveResult.Failure;
            Debug.Log($"[CityManager] Voronoi WFC {LastResult} | " +
                      $"Collapses: {VoronoiStreetSolver.CollapseCount} | " +
                      $"Backtracks: {VoronoiStreetSolver.BacktrackCount}");
        }

        public void ReportGenerationProgress(string stage, float progress)
        {
            SetGenerationProgress(stage, progress);
        }

        public void SetStreetIntersections(List<RoadIntersectionInfo> intersections)
        {
            _streetIntersections = intersections ?? new List<RoadIntersectionInfo>();
        }

        private void SetGenerationProgress(string stage, float progress)
        {
            _generationStage = stage;
            _generationProgress = Mathf.Clamp01(progress);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.DisplayProgressBar("City Generation", stage, _generationProgress);
            }
#endif
        }

        private CityVoronoiSiteGenerator.Config GetVoronoiSiteGeneratorConfig()
        {
            return new CityVoronoiSiteGenerator.Config(
                _seed,
                _voronoiCellSize,
                _nuclei,
                _spatialGradient,
                _gridTopologyInfluence,
                _columns,
                _rows,
                _morphologyBlend,
                _spatialInfluence,
                _nucleusFalloffWorld,
                _cellSize);
        }

        private void ClearGenerated()
        {
            _streetIntersections.Clear();

            var children = new List<Transform>();
            foreach (Transform child in transform)
            {
                children.Add(child);
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                foreach (var child in children.Where(c => c != null))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
            else
#endif
            {
                foreach (var child in children.Where(c => c != null))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showStreetIntersections || _streetIntersections == null || _streetIntersections.Count == 0)
            {
                return;
            }

            float sphereRadius = Mathf.Max(0.05f, _intersectionGizmoRadius);
            float dirLength = Mathf.Max(0.25f, _intersectionDirectionGizmoLength);

            for (int i = 0; i < _streetIntersections.Count; i++)
            {
                RoadIntersectionInfo intersection = _streetIntersections[i];
                if (intersection == null)
                {
                    continue;
                }

                Vector3 center = intersection.Position;
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                Gizmos.DrawSphere(center + Vector3.up * 0.15f, sphereRadius);

                if (intersection.ApproachDirections == null)
                {
                    continue;
                }

                Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
                for (int d = 0; d < intersection.ApproachDirections.Count; d++)
                {
                    Vector3 dir = intersection.ApproachDirections[d];
                    if (dir.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }

                    Vector3 n = dir.normalized;
                    Vector3 start = center + Vector3.up * 0.2f;
                    Vector3 end = start + n * dirLength;
                    Gizmos.DrawLine(start, end);
                    Gizmos.DrawSphere(end, sphereRadius * 0.35f);
                }
            }
        }
    }
}
