using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Runtime.WFC
{
    public sealed class WFCCell
    {
        private readonly TileSet _tileSet;
        private readonly HashSet<int> _candidates;

        private double _cachedEntropy;
        private bool _entropyDirty;

        public int Row { get; }
        public int Column { get; }

        public bool IsCollapsed => _candidates.Count == 1;
        public bool IsContradiction => _candidates.Count == 0;
        public int CandidateCount => _candidates.Count;

        public IReadOnlyCollection<int> Candidates => _candidates;

        public int CollapsedIndex
        {
            get
            {
                if (!IsCollapsed)
                {
                    throw new InvalidOperationException($"Cell ({Row},{Column}) is not collapsed.");
                }

                return _candidates.First();
            }
        }

        public WFCCell(int row, int column, TileSet tileSet)
        {
            Row = row;
            Column = column;
            _tileSet = tileSet ?? throw new ArgumentNullException(nameof(tileSet));
            _candidates = new HashSet<int>(Enumerable.Range(0, tileSet.Count));
            _entropyDirty = true;
        }

        public bool RemoveCandidate(int tileIndex)
        {
            bool removed = _candidates.Remove(tileIndex);
            if (removed)
            {
                _entropyDirty = true;
            }

            return removed;
        }

        public void CollapseToIndex(int tileIndex)
        {
            if (!_candidates.Contains(tileIndex))
            {
                throw new ArgumentException($"Tile {tileIndex} is not a candidate for ({Row},{Column}).");
            }

            _candidates.Clear();
            _candidates.Add(tileIndex);
            _entropyDirty = true;
        }
        public double Entropy
        {
            get
            {
                if (_entropyDirty)
                {
                    _cachedEntropy = ComputeEntropy();
                    _entropyDirty = false;
                }
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

            double weightSum = 0.0;
            double weightLogWeightSum = 0.0;

            foreach (int idx in _candidates)
            {
                double w = _tileSet.GetTile(idx).Weight;
                weightSum += w;
                weightLogWeightSum += w * Math.Log(w);
            }

            return Math.Log(weightSum) - weightLogWeightSum / weightSum;
        }

        public int SampleCandidate(double uniformSample)
        {
            if (IsCollapsed)
            {
                return CollapsedIndex;
            }

            if (IsContradiction)
            {
                throw new InvalidOperationException("Cannot sample a contradiction cell.");
            }

            double total = _candidates.Sum(idx => _tileSet.GetTile(idx).Weight);
            double threshold = uniformSample * total;
            double cumulative = 0.0;

            foreach (int idx in _candidates)
            {
                cumulative += _tileSet.GetTile(idx).Weight;
                if (cumulative >= threshold)
                {
                    return idx;
                }
            }

            return _candidates.Last();
        }

        public WFCCell Clone()
        {
            var clone = new WFCCell(Row, Column, _tileSet);
            clone._candidates.Clear();
            foreach (int idx in _candidates)
            {
                clone._candidates.Add(idx);
            }

            clone._entropyDirty = true;
            return clone;
        }

        public override string ToString()
        {
            if (IsCollapsed)
            {
                return $"[{Row},{Column}] Collapsed={_tileSet.GetTile(CollapsedIndex).Id}";
            }

            if (IsContradiction)
            {
                return $"[{Row},{Column}] CONTRADICTION";
            }

            return $"[{Row},{Column}] Candidates={_candidates.Count} H={Entropy:F4}";
        }
    }
}
