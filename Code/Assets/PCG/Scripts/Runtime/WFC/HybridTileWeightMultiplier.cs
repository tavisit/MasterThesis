using System;

namespace Assets.Scripts.Runtime.WFC
{
    public static class HybridTileWeightMultiplier
    {
        private const double HybridEmptySamplingScale = 0.22;

        public static double ForOrganicBias(TileDefinition tile, float organicBias)
        {
            if (tile == null || string.IsNullOrEmpty(tile.Id))
            {
                return 1.0;
            }

            organicBias = UnityEngine.Mathf.Clamp01(organicBias);
            string id = tile.Id;

            double m;
            if (id.StartsWith("grid_", StringComparison.Ordinal))
            {
                m = UnityEngine.Mathf.Lerp(1f, 0.04f, organicBias);
            }
            else if (id.StartsWith("organic_", StringComparison.Ordinal))
            {
                m = UnityEngine.Mathf.Lerp(0.04f, 1f, organicBias);
            }
            else if (id.StartsWith("transition_", StringComparison.Ordinal))
            {
                float mid = 1f - 2f * UnityEngine.Mathf.Abs(organicBias - 0.5f);
                m = UnityEngine.Mathf.Max(0.08f, UnityEngine.Mathf.Lerp(0.45f, 0.95f, mid));
            }
            else
            {
                return 1.0;
            }

            if (tile.IsEmpty)
            {
                m *= HybridEmptySamplingScale;
            }

            return m;
        }
    }
}
