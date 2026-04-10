using System.Collections.Generic;
using System.Linq;

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

        [Header("Nuclei")]
        [SerializeField] private CityNucleus[] _nuclei;

        [Header("Voronoi (Organic only)")]
        [SerializeField] private int _voronoiResolution = 256;
        [SerializeField] private float _voronoiCellSize = 40f;


        [Header("Street")]
        [SerializeField] private bool _generateStreets = true;
        [SerializeField] private Material _streetMaterial;
        [SerializeField] private int _connectionsPerComponent = 2;

        [Header("Metro")]
        [SerializeField] private bool _generateMetro = true;
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
        [SerializeField] private RoadSettings _roadSettings;

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
        public RoadSettings RoadSettings => _roadSettings;
        public float MetroStationInterval => _metroStationInterval;
        public Material MetroStationMaterial => _metroStationMaterial;
        public int Rows => _rows;
        public int Columns => _columns;
        public float CellSize => _cellSize;
        public UrbanMorphology Morphology => _morphology;
        public int Seed => _seed;
        public int MaxBacktracks => _maxBacktracks;
        public CityNucleus[] Nuclei => _nuclei;
        public int VoronoiResolution => _voronoiResolution;
        public bool GenerateStreets => _generateStreets;
        public bool GenerateMetro => _generateMetro;
        public float MetroBearingPenalty => _metroBearingPenalty;
        public int ConnectionsPerComponent => _connectionsPerComponent;
        public int MeshResolution => _meshResolution;
        public float BridgeHeightThreshold => _bridgeHeightThreshold;
        public float TunnelHeightThreshold => _tunnelHeightThreshold;
        public Material StreetMaterial => _streetMaterial;
        public Material MetroMaterial => _metroMaterial;
        public TerrainAdapter TerrainAdapter => _terrainAdapter;

        public WFCSolver StreetSolver { get; private set; }
        public VoronoiWFCSolver VoronoiStreetSolver { get; private set; }
        public SolveResult LastResult { get; private set; }
        public int CollapseCount => StreetSolver?.CollapseCount ?? 0;
        public int BacktrackCount => StreetSolver?.BacktrackCount ?? 0;

        private SplineRoadGenerator _splineGenerator;

        private SplineRoadGenerator SplineGenerator =>
            _splineGenerator ??= new SplineRoadGenerator(this);


        [ContextMenu("Generate City")]
        public void Generate()
        {
            ClearGenerated();
            SolveStreets();

            if (LastResult == SolveResult.Success)
                SplineGenerator.Generate();
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
                SolveGrid(tileSet);
            }
        }

        private void SolveGrid(TileSet tileSet)
        {
            StreetSolver = new WFCSolver(tileSet, _rows, _columns, _seed, _maxBacktracks);

            if (_nuclei != null && _nuclei.Length > 0)
                NucleusConstraintApplier.Apply(StreetSolver, _nuclei, _rows, _columns, _cellSize);

            if (_terrainAdapter != null)
                _terrainAdapter.ApplyTerrainConstraints(StreetSolver, _rows, _columns, _cellSize);

            LastResult = StreetSolver.Solve();
            Debug.Log($"[CityManager] Street WFC {LastResult} | " +
                      $"Collapses: {CollapseCount} | Backtracks: {BacktrackCount}");
        }

        private void SolveOrganicVoronoi(TileSet tileSet)
        {
            float worldW = _columns * _cellSize;
            float worldH = _rows * _cellSize;

            Vector2[] sites = GenerateVoronoiSites(VoronoiSiteCount, worldW, worldH);
            var cells = VoronoiGenerator.Generate(sites, worldW, worldH, _voronoiResolution);

            VoronoiStreetSolver = new VoronoiWFCSolver(tileSet, cells, _seed, _maxBacktracks);

            if (_nuclei != null && _nuclei.Length > 0)
                NucleusConstraintApplier.ApplyVoronoi(VoronoiStreetSolver, _nuclei);

            if (_terrainAdapter != null)
                _terrainAdapter.ApplyTerrainConstraintsVoronoi(VoronoiStreetSolver);

            var result = VoronoiStreetSolver.Solve();
            LastResult = result == SolveResult.Success ? SolveResult.Success : SolveResult.Failure;
            Debug.Log($"[CityManager] Voronoi WFC {LastResult} | " +
                      $"Collapses: {VoronoiStreetSolver.CollapseCount} | " +
                      $"Backtracks: {VoronoiStreetSolver.BacktrackCount}");
        }

        private Vector2[] GenerateVoronoiSites(int count, float worldW, float worldH)
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

                        if (candidate.x < 0 || candidate.x > worldW ||
                            candidate.y < 0 || candidate.y > worldH) continue;

                        bool tooClose = false;
                        foreach (var s in sites)
                            if (Vector2.Distance(candidate, s) < nucleusMinDist)
                            { tooClose = true; break; }

                        if (!tooClose) sites.Add(candidate);
                    }
                }
            }

            while (sites.Count < count && attempts < count * maxAttempts)
            {
                attempts++;
                var candidate = new Vector2(
                    (float)(rng.NextDouble() * worldW),
                    (float)(rng.NextDouble() * worldH));

                bool tooClose = false;
                foreach (var s in sites)
                    if (Vector2.Distance(candidate, s) < minDist)
                    { tooClose = true; break; }

                if (!tooClose) sites.Add(candidate);
            }

            return sites.ToArray();
        }

        private void ClearGenerated()
        {
            var children = new List<Transform>();
            foreach (Transform child in transform)
                children.Add(child);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                foreach (var child in children.Where(c => c != null))
                    DestroyImmediate(child.gameObject);
            }
            else
#endif
            {
                foreach (var child in children.Where(c => c != null))
                    Destroy(child.gameObject);
            }
        }
    }
}
