using System.Collections.Generic;

using Assets.Scripts.Runtime.Voronoi;

using UnityEngine;

namespace Assets.Scripts.Runtime.City
{
    public enum SpatialMorphologyGradient
    {
        OrganicNearNuclei_GridFar = 0,
        GridNearNuclei_OrganicFar = 1,
    }

    public static class SpatialMorphologyBias
    {
        public static float[] ComputeOrganicBiasPerCell(
            IReadOnlyList<VoronoiCell> cells,
            CityNucleus[] nuclei,
            SpatialMorphologyGradient gradient,
            float falloffWorld,
            float uniformBlend,
            float spatialInfluence)
        {
            int n = cells.Count;
            var result = new float[n];
            uniformBlend = Mathf.Clamp01(uniformBlend);
            spatialInfluence = Mathf.Clamp01(spatialInfluence);
            falloffWorld = Mathf.Max(0.5f, falloffWorld);

            if (nuclei == null || nuclei.Length == 0)
            {
                for (int i = 0; i < n; i++)
                {
                    result[i] = uniformBlend;
                }

                return result;
            }

            for (int i = 0; i < n; i++)
            {
                float spatial = ComputeSpatialOrganicBias(cells[i].Site, nuclei, gradient, falloffWorld);
                result[i] = Mathf.Lerp(uniformBlend, spatial, spatialInfluence);
            }

            return result;
        }

        public static float MinSignedDistanceToNuclei(Vector2 p, CityNucleus[] nuclei)
        {
            float minD = float.MaxValue;
            foreach (var n in nuclei)
            {
                float d = Vector2.Distance(p, n.Centre) - n.Radius;
                if (d < minD)
                {
                    minD = d;
                }
            }

            return minD;
        }

        public static float ComputeSpatialOrganicBias(
            Vector2 site,
            CityNucleus[] nuclei,
            SpatialMorphologyGradient gradient,
            float falloffWorld)
        {
            float minSigned = MinSignedDistanceToNuclei(site, nuclei);

            if (gradient == SpatialMorphologyGradient.OrganicNearNuclei_GridFar)
            {
                return minSigned <= 0f ? 1f : 0f;
            }

            float organicNearNucleus = minSigned <= 0f
                ? 1f
                : Mathf.Clamp01(1f - minSigned / falloffWorld);
            return 1f - organicNearNucleus;
        }
    }
}
