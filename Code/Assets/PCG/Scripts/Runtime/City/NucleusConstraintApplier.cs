using System.Collections.Generic;
using System.Linq;

using Assets.Scripts.Runtime.WFC;

using UnityEngine;

namespace Assets.Scripts.Runtime.City
{
    public static class NucleusConstraintApplier
    {
        private static readonly string[] _roadTileIds =
        {
            "road_ns", "road_ew",
            "corner_ne", "corner_nw", "corner_se", "corner_sw",
            "t_nse", "t_nsw", "t_new", "t_sew",
            "cross"
        };

        private const float SparseFractionDefault = 1.5f;

        public static void Apply(
            WFCSolver solver,
            IReadOnlyList<CityNucleus> nuclei,
            int rows,
            int columns,
            float cellSize,
            float sparseRadiusFraction = SparseFractionDefault)
        {
            if (nuclei == null || nuclei.Count == 0)
            {
                return;
            }

            bool hasHybridTilePrefixes = TileSetHasHybridPrefixes(solver);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float wx = c * cellSize;
                    float wz = r * cellSize;

                    ComputeInfluence(new Vector2(wx, wz), nuclei, sparseRadiusFraction,
                        out float maxInfluence, out float maxStrength);

                    if (maxInfluence <= 0f)
                    {
                        continue;
                    }

                    ApplyTileConstraint(solver, r, c, maxInfluence, maxStrength, hasHybridTilePrefixes);
                }
            }
        }

        public static void ApplyVoronoi(
            VoronoiWFCSolver solver,
            IReadOnlyList<CityNucleus> nuclei,
            float sparseRadiusFraction = SparseFractionDefault)
        {
            if (nuclei == null || nuclei.Count == 0)
            {
                return;
            }

            bool hasHybridTilePrefixes = TileSetHasHybridPrefixes(solver.TileSet);

            int cellCount = solver.CellCount;
            for (int i = 0; i < cellCount; i++)
            {
                var cell = solver.GetCell(i);

                ComputeInfluence(cell.Site, nuclei, sparseRadiusFraction,
                    out float maxInfluence, out float maxStrength);

                if (maxInfluence <= 0f)
                {
                    continue;
                }

                ApplyVoronoiTileConstraint(solver, i, maxInfluence, maxStrength, hasHybridTilePrefixes);
            }
        }

        private static void ComputeInfluence(
            Vector2 worldPos,
            IReadOnlyList<CityNucleus> nuclei,
            float sparseRadiusFraction,
            out float maxInfluence,
            out float maxStrength)
        {
            maxInfluence = 0f;
            maxStrength = 1f;

            foreach (var nucleus in nuclei)
            {
                float dist = Vector2.Distance(worldPos, nucleus.Centre);

                float influence;
                if (dist <= nucleus.Radius)
                {
                    influence = 1f - (dist / Mathf.Max(0.001f, nucleus.Radius));
                }
                else if (dist <= nucleus.Radius * sparseRadiusFraction)
                {
                    float outer = nucleus.Radius * sparseRadiusFraction;
                    influence = (1f - (dist - nucleus.Radius) /
                                  Mathf.Max(0.001f, outer - nucleus.Radius)) * 0.3f;
                }
                else
                {
                    influence = 0f;
                }

                if (influence > maxInfluence)
                {
                    maxInfluence = influence;
                    maxStrength = nucleus.Strength;
                }
            }
        }

        private static bool TileSetHasHybridPrefixes(WFCSolver solver)
            => TileSetHasHybridPrefixes(solver.TileSet);

        private static bool TileSetHasHybridPrefixes(TileSet tileSet)
        {
            // Check if any tile has grid_, organic_, or transition_ prefix
            foreach (var tile in tileSet.Tiles)
            {
                if (tile.Id.StartsWith("grid_") ||
                    tile.Id.StartsWith("organic_") ||
                    tile.Id.StartsWith("transition_"))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ApplyTileConstraint(
            WFCSolver solver, int row, int col,
            float maxInfluence, float maxStrength, bool hasHybridTilePrefixes)
        {
            List<string> allowed;

            if (hasHybridTilePrefixes)
            {
                // For hybrid tiles, allow all road tiles regardless of prefix
                allowed = new List<string>();

                foreach (var tile in solver.TileSet.Tiles)
                {
                    string id = tile.Id;
                    if (id.EndsWith("empty") ||
                        id.Contains("road_ns") || id.Contains("road_ew") ||
                        id.Contains("corner_") ||
                        id.Contains("_t_") ||
                        id.Contains("cross"))
                    {
                        allowed.Add(id);
                    }
                }
            }
            else
            {
                allowed = new List<string>(_roadTileIds) { "empty" };
            }

            if (allowed.Count > 0)
            {
                solver.ApplyConstraint(row, col, allowed);
            }
        }

        private static void ApplyVoronoiTileConstraint(
            VoronoiWFCSolver solver, int cellId,
            float maxInfluence, float maxStrength, bool hasHybridTilePrefixes)
        {
            List<string> allowed;
            if (hasHybridTilePrefixes)
            {
                allowed = solver.TileSet.Tiles
                    .Select(t => t.Id)
                    .Where(id =>
                        id.EndsWith("empty") ||
                        id.Contains("road_ns") || id.Contains("road_ew") ||
                        id.Contains("corner_") ||
                        id.Contains("_t_") ||
                        id.Contains("cross"))
                    .ToList();
            }
            else
            {
                allowed = new List<string>(_roadTileIds) { "empty" };
            }

            solver.ApplyConstraint(cellId, allowed);
        }
    }
}
