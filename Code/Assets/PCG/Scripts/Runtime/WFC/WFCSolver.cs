using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Runtime.WFC
{
    public enum SolveResult { Success, Failure, IterationLimitReached }
    public enum StepResult { Success, Contradiction, AlreadyComplete }

    public sealed class WFCSolver
    {
        private readonly TileSet _tileSet;
        private readonly int _rows;
        private readonly int _columns;
        private readonly Random _rng;
        private readonly int _maxBacktracks;

        private WFCCell[,] _grid;
        private readonly Stack<(WFCCell[,] snapshot, int row, int col, int excludedTile)> _backtrackStack;

        public int BacktrackCount { get; private set; }
        public int CollapseCount { get; private set; }

        public WFCSolver(TileSet tileSet, int rows, int columns, int seed = 0, int maxBacktracks = 1000)
        {
            _tileSet = tileSet ?? throw new ArgumentNullException(nameof(tileSet));
            _rows = rows > 0 ? rows : throw new ArgumentOutOfRangeException(nameof(rows));
            _columns = columns > 0 ? columns : throw new ArgumentOutOfRangeException(nameof(columns));
            _rng = seed == 0 ? new Random() : new Random(seed);
            _maxBacktracks = maxBacktracks;
            _backtrackStack = new Stack<(WFCCell[,], int, int, int)>();

            InitialiseGrid();
        }


        public SolveResult Solve(int maxIterations = 1_000_000)
        {
            BacktrackCount = 0;
            CollapseCount = 0;

            for (int i = 0; i < maxIterations; i++)
            {
                if (IsFullyCollapsed())
                {
                    return SolveResult.Success;
                }

                if (StepOnce() == StepResult.Contradiction)
                {
                    if (BacktrackCount >= _maxBacktracks)
                    {
                        return SolveResult.Failure;
                    }

                    if (!Backtrack())
                    {
                        return SolveResult.Failure;
                    }

                    BacktrackCount++;
                }
            }

            return IsFullyCollapsed() ? SolveResult.Success : SolveResult.IterationLimitReached;
        }

        public StepResult StepOnce()
        {
            WFCCell target = SelectLowestEntropyCell();
            if (target == null)
            {
                return StepResult.AlreadyComplete;
            }

            int chosen = target.SampleCandidate(_rng.NextDouble());
            PushSnapshot(target.Row, target.Column, chosen);
            target.CollapseToIndex(chosen);
            CollapseCount++;

            return Propagate(target.Row, target.Column) ? StepResult.Success : StepResult.Contradiction;
        }

        public TileDefinition GetCollapsedTile(int row, int col)
            => _tileSet.GetTile(_grid[row, col].CollapsedIndex);

        public int GetCollapsedIndex(int row, int col)
            => _grid[row, col].CollapsedIndex;

        public bool ApplyConstraint(int row, int col, IEnumerable<string> allowedIds)
        {
            var allowedSet = new HashSet<int>(allowedIds.Select(id => _tileSet.IndexOf(id)).Where(idx => idx >= 0));
            WFCCell cell = _grid[row, col];

            var toRemove = cell.Candidates.Where(idx => !allowedSet.Contains(idx)).ToList();
            foreach (int idx in toRemove)
            {
                cell.RemoveCandidate(idx);
            }

            if (cell.IsContradiction)
            {
                return false;
            }

            return Propagate(row, col);
        }

        public bool IsFullyCollapsed()
        {
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    if (!_grid[r, c].IsCollapsed)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private WFCCell SelectLowestEntropyCell()
        {
            WFCCell best = null;
            double bestH = double.PositiveInfinity;

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    WFCCell cell = _grid[r, c];
                    if (cell.IsCollapsed)
                    {
                        continue;
                    }

                    double h = cell.Entropy + _rng.NextDouble() * 1e-6;
                    if (h < bestH)
                    {
                        bestH = h;
                        best = cell;
                    }
                }
            }

            return best;
        }

        private bool Propagate(int startRow, int startCol)
        {
            var queue = new Queue<(int r, int c)>();
            queue.Enqueue((startRow, startCol));

            while (queue.Count > 0)
            {
                var (r, c) = queue.Dequeue();
                WFCCell cell = _grid[r, c];

                foreach (Direction dir in Enum.GetValues(typeof(Direction)))
                {
                    var (dc, dr) = dir.ToOffset();
                    int nr = r + dr;
                    int nc = c + dc;

                    if (!InBounds(nr, nc))
                    {
                        continue;
                    }

                    WFCCell neighbour = _grid[nr, nc];
                    if (neighbour.IsCollapsed)
                    {
                        continue;
                    }

                    var allowed = new HashSet<int>();
                    foreach (int candidate in cell.Candidates)
                    {
                        foreach (int compatible in _tileSet.GetCompatible(candidate, dir))
                        {
                            allowed.Add(compatible);
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
                        queue.Enqueue((nr, nc));
                    }
                }
            }

            return true;
        }

        private void PushSnapshot(int row, int col, int chosenTile)
        {
            var snapshot = new WFCCell[_rows, _columns];
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    snapshot[r, c] = _grid[r, c].Clone();
                }
            }

            _backtrackStack.Push((snapshot, row, col, chosenTile));
        }

        private bool Backtrack()
        {
            while (_backtrackStack.Count > 0)
            {
                var (snapshot, row, col, excluded) = _backtrackStack.Pop();

                _grid = snapshot;

                WFCCell cell = _grid[row, col];
                cell.RemoveCandidate(excluded);

                if (cell.IsContradiction)
                {
                    continue;
                }

                return Propagate(row, col);
            }

            return false;
        }

        private void InitialiseGrid()
        {
            _grid = new WFCCell[_rows, _columns];
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    _grid[r, c] = new WFCCell(r, c, _tileSet);
                }
            }
        }

        private bool InBounds(int r, int c) => r >= 0 && r < _rows && c >= 0 && c < _columns;
    }
}
