using System.Collections.Generic;

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

        public static void Apply(
            WFCSolver solver,
            IReadOnlyList<CityNucleus> nuclei,
            int rows,
            int columns,
            float cellSize,
            float sparseRadiusFraction = 1.5f)
        {
            if (nuclei == null || nuclei.Count == 0)
            {
                return;
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float wx = c * cellSize;
                    float wz = r * cellSize;

                    float minDist = float.MaxValue;
                    float influence = 0f;

                    foreach (var nucleus in nuclei)
                    {
                        float dist = Vector2.Distance(new Vector2(wx, wz), nucleus.Centre);
                        if (dist < minDist)
                        {
                            minDist = dist;
                        }
                        if (dist <= nucleus.Radius)
                        {
                            influence = Mathf.Max(influence, 1f);
                        }
                        else if (dist <= nucleus.Radius * sparseRadiusFraction)
                        {
                            influence = Mathf.Max(influence,
                                1f - (dist - nucleus.Radius) / (nucleus.Radius * (sparseRadiusFraction - 1f)));
                        }
                    }
                    if (influence >= 1f)
                    {
                        var allowed = new System.Collections.Generic.List<string>(_roadTileIds) { "empty" };
                        solver.ApplyConstraint(r, c, allowed);
                    }
                }
            }
        }
    }
}
