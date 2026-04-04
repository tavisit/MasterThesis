using System.Collections.Generic;

using Assets.Scripts.Runtime.Adapters;
using Assets.Scripts.Runtime.Road.Generators;
using Assets.Scripts.Runtime.Voronoi;
using Assets.Scripts.Runtime.WFC;

using UnityEngine;

namespace Assets.Scripts.Runtime.City
{
    public sealed class CityManager : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private int _rows = 20;
        [SerializeField] private int _columns = 20;
        [SerializeField] private float _cellSize = 10f;

        [Header("Morphology")]
        [SerializeField] private UrbanMorphology _morphology = UrbanMorphology.Grid;

        [Header("Solver")]
        [SerializeField] private int _seed = 0;
        [SerializeField] private int _maxBacktracks = 1000;

        [Header("Nuclei (Organic only)")]
        [SerializeField] private CityNucleus[] _nuclei;

        [Header("Voronoi (Organic only)")]
        [SerializeField] private int _voronoiResolution = 256;
        [SerializeField] private float _voronoiCellSize = 40f;
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

        [Header("Boulevard")]
        [SerializeField] private bool _generateBoulevards = true;
        [SerializeField] private float _minBoulevardLength = 40f;
        [SerializeField] private int _connectionsPerNucleus = 2;
        [SerializeField] private Material _boulevardMaterial;
        [SerializeField]
        [Range(0f, 2f)]
        private float _boulevardBearingPenalty = 0.6f;

        [Header("Street")]
        [SerializeField] private bool _generateStreets = true;
        [SerializeField] private Material _streetMaterial;
        [SerializeField] private Material _bridgeMaterial;
        [SerializeField] private Material _tunnelMaterial;
        [SerializeField] private int _connectionsPerComponent = 2;

        [Header("Metro")]
        [SerializeField] private bool _generateMetro = true;
        [SerializeField] private int _metroSeed = 2;
        [SerializeField] private Material _metroMaterial;

        [Header("Terrain")]
        [SerializeField] private TerrainAdapter _terrainAdapter;

        [Header("Mesh")]
        [SerializeField] private int _meshResolution = 20;
        [SerializeField] private float _bridgeHeightThreshold = 1f;
        [SerializeField] private float _tunnelHeightThreshold = 1f;

        public int ConnectionsPerComponent => _connectionsPerComponent;
        public WFCSolver StreetSolver { get; private set; }
        public VoronoiWFCSolver VoronoiStreetSolver { get; private set; }
        public int VoronoiResolution => _voronoiResolution;
        public WFCSolver MetroSolver { get; private set; }
        public SolveResult LastResult { get; private set; }
        public int CollapseCount => StreetSolver?.CollapseCount ?? 0;
        public int BacktrackCount => StreetSolver?.BacktrackCount ?? 0;
        public float BoulevardBearingPenalty => _boulevardBearingPenalty;
        public int Rows => _rows;
        public int Columns => _columns;
        public float CellSize => _cellSize;
        public UrbanMorphology Morphology => _morphology;
        public int Seed => _seed;
        public int MaxBacktracks => _maxBacktracks;
        public CityNucleus[] Nuclei => _nuclei;
        public bool GenerateBoulevards => _generateBoulevards;
        public float MinBoulevardLength => _minBoulevardLength;
        public int MaxBoulevards => _connectionsPerNucleus;
        public bool GenerateStreets => _generateStreets;
        public bool GenerateMetro => _generateMetro;
        public int MetroSeed => _metroSeed;
        public int MeshResolution => _meshResolution;
        public float BridgeHeightThreshold => _bridgeHeightThreshold;
        public float TunnelHeightThreshold => _tunnelHeightThreshold;
        public Material BoulevardMaterial => _boulevardMaterial;
        public Material StreetMaterial => _streetMaterial;
        public Material BridgeMaterial => _bridgeMaterial;
        public Material TunnelMaterial => _tunnelMaterial;
        public Material MetroMaterial => _metroMaterial;
        public TerrainAdapter TerrainAdapter => _terrainAdapter;

        private readonly List<GameObject> _generated = new();

        private SplineRoadGenerator _splineGenerator;

        private void Awake()
        {
            _splineGenerator = GetComponent<SplineRoadGenerator>();
        }

        [ContextMenu("Generate City")]
        public void Generate()
        {
            ClearGenerated();
            SolveStreets();

            if (LastResult == SolveResult.Success)
            {
                var splineGen = GetComponent<SplineRoadGenerator>();
                if (splineGen != null)
                {
                    splineGen.Generate();
                }
                else
                {
                    Debug.LogWarning("[CityManager] No SplineRoadGenerator found on this GameObject.");
                }
            }
        }

        [ContextMenu("Clear City")]
        public void Clear()
        {
            ClearGenerated();
            GetComponent<SplineRoadGenerator>()?.Clear();
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
                SolveGrid(tileSet);
            }
        }

        private void SolveGrid(TileSet tileSet)
        {
            StreetSolver = new WFCSolver(tileSet, _rows, _columns, _seed, _maxBacktracks);

            if (_nuclei != null && _nuclei.Length > 0)
            {
                NucleusConstraintApplier.Apply(StreetSolver, _nuclei, _rows, _columns, _cellSize);
            }

            if (_terrainAdapter != null)
            {
                _terrainAdapter.ApplyTerrainConstraints(StreetSolver, _rows, _columns, _cellSize);
            }

            LastResult = StreetSolver.Solve();
            Debug.Log($"[CityManager] Street WFC {LastResult} | Collapses: {CollapseCount} | Backtracks: {BacktrackCount}");
        }

        private void SolveOrganicVoronoi(TileSet tileSet)
        {
            float worldW = _columns * _cellSize;
            float worldH = _rows * _cellSize;

            Vector2[] sites = GenerateVoronoiSites(VoronoiSiteCount, worldW, worldH);
            var cells = VoronoiGenerator.Generate(sites, worldW, worldH, _voronoiResolution);

            VoronoiStreetSolver = new VoronoiWFCSolver(tileSet, cells, _seed, _maxBacktracks);

            if (_terrainAdapter != null)
            {
                _terrainAdapter.ApplyTerrainConstraintsVoronoi(VoronoiStreetSolver);
            }

            var result = VoronoiStreetSolver.Solve();
            LastResult = result == SolveResult.Success ? SolveResult.Success : SolveResult.Failure;
            Debug.Log($"[CityManager] Voronoi WFC {LastResult} | Collapses: {VoronoiStreetSolver.CollapseCount} | Backtracks: {VoronoiStreetSolver.BacktrackCount}");
        }

        private Vector2[] GenerateVoronoiSites(int count, float worldW, float worldH)
        {
            var rng = new System.Random(_seed);
            var sites = new List<Vector2>();
            float minDist = _voronoiCellSize * 0.8f;
            int maxAttempts = 30;

            int attempts = 0;
            while (sites.Count < count && attempts < count * maxAttempts)
            {
                attempts++;
                var candidate = new Vector2(
                    (float)(rng.NextDouble() * worldW),
                    (float)(rng.NextDouble() * worldH));

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

            return sites.ToArray();
        }

        private void ClearGenerated()
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
                    DestroyImmediate(go);
                }
                else
#endif
                    Destroy(go);
            }
            _generated.Clear();
        }
    }
}
