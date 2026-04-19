using System;
using System.Collections.Generic;
using System.Linq;

using Assets.Scripts.Runtime.Voronoi;

namespace Assets.Scripts.Runtime.WFC
{
    public sealed class VoronoiWFCSolver
    {
        private readonly TileSet _tileSet;
        private readonly List<VoronoiCell> _cells;
        private readonly VoronoiWFCNode[] _nodes;
        private readonly Random _rng;
        private readonly int _maxBacktracks;

        private readonly Stack<(VoronoiWFCNode[] snapshot, int cellId, int excludedTile)> _backtrackStack;

        public int BacktrackCount { get; private set; }
        public int CollapseCount { get; private set; }
        public SolveResult LastResult { get; private set; }

        public TileSet TileSet => _tileSet;

        public VoronoiWFCSolver(
            TileSet tileSet,
            List<VoronoiCell> cells,
            int seed = 0,
            int maxBacktracks = 1000,
            float[] organicBiasPerCell = null)
        {
            _tileSet = tileSet ?? throw new ArgumentNullException(nameof(tileSet));
            _cells = cells ?? throw new ArgumentNullException(nameof(cells));
            _rng = seed == 0 ? new Random() : new Random(seed);
            _maxBacktracks = maxBacktracks;
            _backtrackStack = new Stack<(VoronoiWFCNode[], int, int)>();

            _nodes = new VoronoiWFCNode[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                float bias = 0.5f;
                if (organicBiasPerCell != null && i < organicBiasPerCell.Length)
                {
                    bias = organicBiasPerCell[i];
                }

                _nodes[i] = new VoronoiWFCNode(i, tileSet, bias);
            }
        }

        public SolveResult Solve(int maxIterations = 1_000_000, Action<float> onProgress = null)
        {
            BacktrackCount = 0;
            CollapseCount = 0;
            int totalCells = Math.Max(1, _nodes.Length);
            onProgress?.Invoke(0f);

            for (int i = 0; i < maxIterations; i++)
            {
                if (IsFullyCollapsed())
                {
                    onProgress?.Invoke(1f);
                    return LastResult = SolveResult.Success;
                }

                if (StepOnce() == StepResult.Contradiction)
                {
                    if (BacktrackCount >= _maxBacktracks)
                    {
                        return LastResult = SolveResult.Failure;
                    }

                    if (!Backtrack())
                    {
                        return LastResult = SolveResult.Failure;
                    }

                    BacktrackCount++;
                }

                if ((i & 31) == 0)
                {
                    onProgress?.Invoke(Math.Min(1f, CollapseCount / (float)totalCells));
                }
            }

            onProgress?.Invoke(IsFullyCollapsed() ? 1f : Math.Min(1f, CollapseCount / (float)totalCells));
            return LastResult = IsFullyCollapsed() ? SolveResult.Success : SolveResult.IterationLimitReached;
        }

        public StepResult StepOnce()
        {
            VoronoiWFCNode target = SelectLowestEntropy();
            if (target == null)
            {
                return StepResult.AlreadyComplete;
            }

            int chosen = target.SampleCandidate(_rng.NextDouble());
            PushSnapshot(target.CellId, chosen);
            target.CollapseToIndex(chosen);
            CollapseCount++;

            return Propagate(target.CellId) ? StepResult.Success : StepResult.Contradiction;
        }

        public bool IsFullyCollapsed() => _nodes.All(n => n.IsCollapsed);

        public int GetCollapsedIndex(int cellId) => _nodes[cellId].CollapsedIndex;
        public TileDefinition GetCollapsedTile(int cellId) => _tileSet.GetTile(_nodes[cellId].CollapsedIndex);
        public VoronoiCell GetCell(int cellId) => _cells[cellId];
        public int CellCount => _cells.Count;

        public bool ApplyConstraint(int cellId, IEnumerable<string> allowedIds)
        {
            var allowed = new HashSet<int>(allowedIds.Select(id => _tileSet.IndexOf(id)).Where(idx => idx >= 0));
            var node = _nodes[cellId];

            foreach (int idx in node.Candidates.ToList())
            {
                if (!allowed.Contains(idx))
                {
                    node.RemoveCandidate(idx);
                }
            }

            if (node.IsContradiction)
            {
                return false;
            }

            return Propagate(cellId);
        }

        private VoronoiWFCNode SelectLowestEntropy()
        {
            VoronoiWFCNode best = null;
            double bestH = double.PositiveInfinity;

            foreach (var node in _nodes)
            {
                if (node.IsCollapsed)
                {
                    continue;
                }

                double h = node.Entropy + _rng.NextDouble() * 1e-6;
                if (h < bestH) { bestH = h; best = node; }
            }

            return best;
        }

        private bool Propagate(int startId)
        {
            var queue = new Queue<int>();
            queue.Enqueue(startId);

            while (queue.Count > 0)
            {
                int id = queue.Dequeue();
                var node = _nodes[id];

                foreach (int neighbourId in _cells[id].Neighbours)
                {
                    var neighbour = _nodes[neighbourId];
                    if (neighbour.IsCollapsed)
                    {
                        continue;
                    }
                    var allowed = new HashSet<int>();
                    foreach (int candidate in node.Candidates)
                    {
                        foreach (Direction dir in Enum.GetValues(typeof(Direction)))
                        {
                            foreach (int compatible in _tileSet.GetCompatible(candidate, dir))
                            {
                                allowed.Add(compatible);
                            }
                        }
                    }

                    bool changed = false;
                    foreach (int idx in neighbour.Candidates.ToList())
                    {
                        if (!allowed.Contains(idx))
                        {
                            neighbour.RemoveCandidate(idx);
                            changed = true;
                        }
                    }

                    if (neighbour.IsContradiction)
                    {
                        return false;
                    }

                    if (changed)
                    {
                        queue.Enqueue(neighbourId);
                    }
                }
            }

            return true;
        }

        private void PushSnapshot(int cellId, int chosenTile)
        {
            var snapshot = new VoronoiWFCNode[_nodes.Length];
            for (int i = 0; i < _nodes.Length; i++)
            {
                snapshot[i] = _nodes[i].Clone();
            }

            _backtrackStack.Push((snapshot, cellId, chosenTile));
        }

        private bool Backtrack()
        {
            while (_backtrackStack.Count > 0)
            {
                var (snapshot, cellId, excluded) = _backtrackStack.Pop();
                for (int i = 0; i < _nodes.Length; i++)
                {
                    _nodes[i] = snapshot[i];
                }

                _nodes[cellId].RemoveCandidate(excluded);
                if (_nodes[cellId].IsContradiction)
                {
                    continue;
                }

                return Propagate(cellId);
            }
            return false;
        }
    }

    public sealed class VoronoiWFCNode
    {
        private readonly TileSet _tileSet;
        private readonly float _organicBias;
        private readonly HashSet<int> _candidates;
        private double _cachedEntropy;
        private bool _entropyDirty;

        public int CellId { get; }
        public bool IsCollapsed => _candidates.Count == 1;
        public bool IsContradiction => _candidates.Count == 0;
        public IReadOnlyCollection<int> Candidates => _candidates;

        public int CollapsedIndex
        {
            get
            {
                if (!IsCollapsed)
                {
                    throw new InvalidOperationException($"Node {CellId} not collapsed.");
                }

                return _candidates.First();
            }
        }

        public VoronoiWFCNode(int cellId, TileSet tileSet, float organicBias = 0.5f)
        {
            CellId = cellId;
            _tileSet = tileSet;
            _organicBias = organicBias;
            _candidates = new HashSet<int>(Enumerable.Range(0, tileSet.Count));
            _entropyDirty = true;
        }

        public bool RemoveCandidate(int idx) { bool r = _candidates.Remove(idx); if (r) { _entropyDirty = true; } return r; }
        public void CollapseToIndex(int idx) { _candidates.Clear(); _candidates.Add(idx); _entropyDirty = true; }

        public double Entropy
        {
            get
            {
                if (_entropyDirty) { _cachedEntropy = ComputeEntropy(); _entropyDirty = false; }
                return _cachedEntropy;
            }
        }

        private double ComputeEntropy()
        {
            if (IsContradiction)
            {
                return double.PositiveInfinity;
            }

            if (IsCollapsed)
            {
                return 0.0;
            }

            double wSum = 0, wlwSum = 0;
            foreach (int idx in _candidates)
            {
                var tile = _tileSet.GetTile(idx);
                double w = tile.Weight * HybridTileWeightMultiplier.ForOrganicBias(tile, _organicBias);
                wSum += w;
                wlwSum += w * Math.Log(w);
            }

            return Math.Log(wSum) - wlwSum / wSum;
        }

        public int SampleCandidate(double u)
        {
            double total = 0;
            foreach (int idx in _candidates)
            {
                var tile = _tileSet.GetTile(idx);
                total += tile.Weight * HybridTileWeightMultiplier.ForOrganicBias(tile, _organicBias);
            }

            double cum = 0;
            foreach (int idx in _candidates)
            {
                var tile = _tileSet.GetTile(idx);
                cum += tile.Weight * HybridTileWeightMultiplier.ForOrganicBias(tile, _organicBias);
                if (cum >= u * total)
                {
                    return idx;
                }
            }

            return _candidates.Last();
        }

        public VoronoiWFCNode Clone()
        {
            var c = new VoronoiWFCNode(CellId, _tileSet, _organicBias);
            c._candidates.Clear();
            foreach (int idx in _candidates)
            {
                c._candidates.Add(idx);
            }

            c._entropyDirty = true;
            return c;
        }
    }
}
