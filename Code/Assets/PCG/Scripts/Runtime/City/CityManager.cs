using System.Collections.Generic;
using System.Linq;

using Assets.Scripts.Runtime.Adapters;
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

        [Header("Street")]
        [SerializeField] private bool _generateStreets = true;
        [SerializeField] private Material _streetMaterial;
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
        [SerializeField] private Material _metroMaterial;
        [SerializeField][Range(0f, 2f)] private float _metroBearingPenalty = 0.6f;
        [SerializeField] private float _metroStationInterval = 80f;
        [SerializeField] private Material _metroStationMaterial;

        [Header("Terrain")]
        [SerializeField] private TerrainAdapter _terrainAdapter;

        [Header("Mesh")]
        [SerializeField] private int _meshResolution = 20;
        [SerializeField] private float _bridgeHeightThreshold = 1f;
        [SerializeField] private float _tunnelHeightThreshold = 1f;
        [SerializeField] private float _roadMeshVerticalOffset = 0.02f;
        [SerializeField] private float _sidewalkMeshVerticalOffset = 0.01f;
        [SerializeField] private RoadSettings _roadSettings;

        [Header("Street Decoration")]
        [SerializeField] private bool _generateStreetDecor = true;
        [SerializeField] private bool _generateSidewalks = true;
        [SerializeField] private Material _sidewalkMaterial;
        [SerializeField] private float _sidewalkWidth = 1.2f;
        [SerializeField] private float _sidewalkVerticalOffset = 0.05f;
        [SerializeField] private bool _generateLightPosts = true;
        [SerializeField] private GameObject _lightPostPrefab;
        [SerializeField] private float _lightPostInterval = 28f;
        [SerializeField] private bool _generateSidewalkProps = true;
        [SerializeField] private List<GameObject> _sidewalkPropPrefabs = new();
        [SerializeField] private float _sidewalkPropInterval = 36f;
        [SerializeField][Range(0f, 1f)] private float _sidewalkPropSpawnChance = 0.45f;
        [SerializeField] private bool _avoidRoadOverlapForDecor = true;
        [SerializeField] private bool _parallelizeDecorChecks = true;

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
        public float BridgeHeightThreshold => _bridgeHeightThreshold;
        public float TunnelHeightThreshold => _tunnelHeightThreshold;
        public float RoadMeshVerticalOffset => _roadMeshVerticalOffset;
        public float SidewalkMeshVerticalOffset => _sidewalkMeshVerticalOffset;
        public Material StreetMaterial => _streetMaterial;
        public Material MetroMaterial => _metroMaterial;
        public TerrainAdapter TerrainAdapter => _terrainAdapter;
        public bool GenerateStreetDecor => _generateStreetDecor;
        public bool GenerateSidewalks => _generateSidewalks;
        public Material SidewalkMaterial => _sidewalkMaterial;
        public float SidewalkWidth => _sidewalkWidth;
        public float SidewalkVerticalOffset => _sidewalkVerticalOffset;
        public bool GenerateLightPosts => _generateLightPosts;
        public GameObject LightPostPrefab => _lightPostPrefab;
        public float LightPostInterval => _lightPostInterval;
        public bool GenerateSidewalkProps => _generateSidewalkProps;
        public IReadOnlyList<GameObject> SidewalkPropPrefabs => _sidewalkPropPrefabs;
        public float SidewalkPropInterval => _sidewalkPropInterval;
        public float SidewalkPropSpawnChance => _sidewalkPropSpawnChance;
        public bool AvoidRoadOverlapForDecor => _avoidRoadOverlapForDecor;
        public bool ParallelizeDecorChecks => _parallelizeDecorChecks;
        public string GenerationStage => _generationStage;
        public float GenerationProgress => _generationProgress;

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

        public void RegenerateMeshes()
        {
            SplineGenerator.Clear();
            _splineGenerator = new SplineRoadGenerator(this);
            _splineGenerator.Generate();
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
            Vector2[] sites = GenerateVoronoiSites(VoronoiSiteCount, worldW, worldH, applySpatialGridSnap: true);
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
            Vector2[] sites = GenerateVoronoiSites(VoronoiSiteCount, worldW, worldH);
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

        private Vector2[] GenerateVoronoiSites(int count, float worldW, float worldH, bool applySpatialGridSnap = false)
        {
            var rng = new System.Random(_seed);
            var sites = new List<Vector2>();
            float minDist = _voronoiCellSize * 0.8f;
            int maxAttempts = 30;
            int attempts = 0;

            if (_nuclei != null)
            {
                foreach (var nucleus in _nuclei)
                {
                    float nucleusMinDist = _voronoiCellSize * (0.5f / nucleus.Strength);
                    int extraCount = Mathf.RoundToInt(
                        Mathf.PI * nucleus.Radius * nucleus.Radius /
                        (nucleusMinDist * nucleusMinDist));
                    extraCount = Mathf.Clamp(extraCount, 4, 40);

                    for (int e = 0; e < extraCount * maxAttempts && sites.Count < count + extraCount; e++)
                    {
                        float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                        float r = (float)(rng.NextDouble() * nucleus.Radius);
                        var candidate = new Vector2(
                            nucleus.Centre.x + Mathf.Cos(angle) * r,
                            nucleus.Centre.y + Mathf.Sin(angle) * r);
                        if (applySpatialGridSnap)
                        {
                            candidate = SnapSiteTowardGrid(candidate, worldW, worldH);
                        }

                        if (candidate.x < 0 || candidate.x > worldW ||
                            candidate.y < 0 || candidate.y > worldH)
                        {
                            continue;
                        }

                        bool tooClose = false;
                        foreach (var s in sites)
                        {
                            if (Vector2.Distance(candidate, s) < nucleusMinDist)
                            { tooClose = true; break; }
                        }

                        if (!tooClose)
                        {
                            sites.Add(candidate);
                        }
                    }
                }
            }

            while (sites.Count < count && attempts < count * maxAttempts)
            {
                if (applySpatialGridSnap &&
                    _spatialGradient == SpatialMorphologyGradient.OrganicNearNuclei_GridFar &&
                    _gridTopologyInfluence >= 0.6f)
                {
                    int gxCount = Mathf.Max(1, _columns);
                    int gyCount = Mathf.Max(1, _rows);
                    float stepX = worldW / gxCount;
                    float stepY = worldH / gyCount;
                    var lattice = new List<Vector2>(gxCount * gyCount);

                    for (int gy = 0; gy < gyCount; gy++)
                    {
                        for (int gx = 0; gx < gxCount; gx++)
                        {
                            lattice.Add(new Vector2((gx + 0.5f) * stepX, (gy + 0.5f) * stepY));
                        }
                    }

                    for (int i = lattice.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        (lattice[i], lattice[j]) = (lattice[j], lattice[i]);
                    }

                    foreach (var p in lattice)
                    {
                        if (sites.Count >= count)
                        {
                            break;
                        }

                        var candidate = p;
                        if (_nuclei != null && _nuclei.Length > 0)
                        {
                            float organic = SpatialMorphologyBias.ComputeSpatialOrganicBias(
                                candidate, _nuclei, _spatialGradient, _nucleusFalloffWorld);
                            organic = Mathf.Lerp(_morphologyBlend, organic, _spatialInfluence);
                            float jitter = Mathf.Clamp01(organic) * Mathf.Min(stepX, stepY) * 0.28f;
                            if (jitter > 0.001f)
                            {
                                candidate += new Vector2(
                                    ((float)rng.NextDouble() * 2f - 1f) * jitter,
                                    ((float)rng.NextDouble() * 2f - 1f) * jitter);
                            }
                        }

                        candidate = SnapSiteTowardGrid(candidate, worldW, worldH);
                        candidate.x = Mathf.Clamp(candidate.x, 0f, worldW);
                        candidate.y = Mathf.Clamp(candidate.y, 0f, worldH);
                        sites.Add(candidate);
                    }

                    break;
                }
                else
                {
                    attempts++;
                    var candidate = new Vector2(
                        (float)(rng.NextDouble() * worldW),
                        (float)(rng.NextDouble() * worldH));
                    if (applySpatialGridSnap)
                    {
                        candidate = SnapSiteTowardGrid(candidate, worldW, worldH);
                    }

                    bool tooClose = false;
                    foreach (var s in sites)
                    {
                        if (Vector2.Distance(candidate, s) < minDist)
                        { tooClose = true; break; }
                    }

                    if (!tooClose)
                    {
                        sites.Add(candidate);
                    }
                }
            }

            return sites.ToArray();
        }
        private Vector2 SnapSiteTowardGrid(Vector2 site, float worldW, float worldH)
        {
            float organicBias;
            if (_nuclei == null || _nuclei.Length == 0)
            {
                organicBias = _morphologyBlend;
            }
            else
            {
                organicBias = SpatialMorphologyBias.ComputeSpatialOrganicBias(
                    site,
                    _nuclei,
                    _spatialGradient,
                    _nucleusFalloffWorld);
                organicBias = Mathf.Lerp(_morphologyBlend, organicBias, _spatialInfluence);
            }

            float gridAffinity = 1f - Mathf.Clamp01(organicBias);
            float snapStrength = Mathf.Clamp01(gridAffinity * _gridTopologyInfluence);
            if (snapStrength <= 0f)
            {
                return site;
            }

            float step = Mathf.Max(1f, Mathf.Max(_cellSize, _voronoiCellSize * 0.75f));
            Vector2 gridAnchor = new Vector2(
                Mathf.Round(site.x / step) * step,
                Mathf.Round(site.y / step) * step);

            Vector2 snapped = snapStrength >= 0.85f
                ? gridAnchor
                : Vector2.Lerp(site, gridAnchor, snapStrength);
            snapped.x = Mathf.Clamp(snapped.x, 0f, worldW);
            snapped.y = Mathf.Clamp(snapped.y, 0f, worldH);
            return snapped;
        }

        private void ClearGenerated()
        {
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
    }
}
