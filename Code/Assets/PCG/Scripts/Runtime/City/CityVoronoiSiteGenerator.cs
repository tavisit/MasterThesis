using System.Collections.Generic;

using UnityEngine;

namespace Assets.Scripts.Runtime.City
{
    internal static class CityVoronoiSiteGenerator
    {
        internal readonly struct Config
        {
            internal readonly int Seed;
            internal readonly float VoronoiCellSize;
            internal readonly CityNucleus[] Nuclei;
            internal readonly SpatialMorphologyGradient SpatialGradient;
            internal readonly float GridTopologyInfluence;
            internal readonly int Columns;
            internal readonly int Rows;
            internal readonly float MorphologyBlend;
            internal readonly float SpatialInfluence;
            internal readonly float NucleusFalloffWorld;
            internal readonly float CellSize;

            internal Config(
                int seed,
                float voronoiCellSize,
                CityNucleus[] nuclei,
                SpatialMorphologyGradient spatialGradient,
                float gridTopologyInfluence,
                int columns,
                int rows,
                float morphologyBlend,
                float spatialInfluence,
                float nucleusFalloffWorld,
                float cellSize)
            {
                Seed = seed;
                VoronoiCellSize = voronoiCellSize;
                Nuclei = nuclei;
                SpatialGradient = spatialGradient;
                GridTopologyInfluence = gridTopologyInfluence;
                Columns = columns;
                Rows = rows;
                MorphologyBlend = morphologyBlend;
                SpatialInfluence = spatialInfluence;
                NucleusFalloffWorld = nucleusFalloffWorld;
                CellSize = cellSize;
            }
        }

        internal static Vector2[] GenerateSites(
            int count,
            float worldW,
            float worldH,
            bool applySpatialGridSnap,
            in Config config)
        {
            var rng = new System.Random(config.Seed);
            var sites = new List<Vector2>();
            float minDist = config.VoronoiCellSize * 0.8f;
            int maxAttempts = 30;
            int attempts = 0;

            if (config.Nuclei != null)
            {
                foreach (var nucleus in config.Nuclei)
                {
                    float nucleusMinDist = config.VoronoiCellSize * (0.5f / nucleus.Strength);
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
                            candidate = SnapSiteTowardGrid(candidate, worldW, worldH, config);
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
                            {
                                tooClose = true;
                                break;
                            }
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
                    config.SpatialGradient == SpatialMorphologyGradient.OrganicNearNuclei_GridFar &&
                    config.GridTopologyInfluence >= 0.6f)
                {
                    int gxCount = Mathf.Max(1, config.Columns);
                    int gyCount = Mathf.Max(1, config.Rows);
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
                        if (config.Nuclei != null && config.Nuclei.Length > 0)
                        {
                            float organic = SpatialMorphologyBias.ComputeSpatialOrganicBias(
                                candidate, config.Nuclei, config.SpatialGradient, config.NucleusFalloffWorld);
                            organic = Mathf.Lerp(config.MorphologyBlend, organic, config.SpatialInfluence);
                            float jitter = Mathf.Clamp01(organic) * Mathf.Min(stepX, stepY) * 0.28f;
                            if (jitter > 0.001f)
                            {
                                candidate += new Vector2(
                                    ((float)rng.NextDouble() * 2f - 1f) * jitter,
                                    ((float)rng.NextDouble() * 2f - 1f) * jitter);
                            }
                        }

                        candidate = SnapSiteTowardGrid(candidate, worldW, worldH, config);
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
                        candidate = SnapSiteTowardGrid(candidate, worldW, worldH, config);
                    }

                    bool tooClose = false;
                    foreach (var s in sites)
                    {
                        if (Vector2.Distance(candidate, s) < minDist)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        sites.Add(candidate);
                    }
                }
            }

            return sites.ToArray();
        }

        private static Vector2 SnapSiteTowardGrid(Vector2 site, float worldW, float worldH, in Config config)
        {
            float organicBias;
            if (config.Nuclei == null || config.Nuclei.Length == 0)
            {
                organicBias = config.MorphologyBlend;
            }
            else
            {
                organicBias = SpatialMorphologyBias.ComputeSpatialOrganicBias(
                    site,
                    config.Nuclei,
                    config.SpatialGradient,
                    config.NucleusFalloffWorld);
                organicBias = Mathf.Lerp(config.MorphologyBlend, organicBias, config.SpatialInfluence);
            }

            float gridAffinity = 1f - Mathf.Clamp01(organicBias);
            float snapStrength = Mathf.Clamp01(gridAffinity * config.GridTopologyInfluence);
            if (snapStrength <= 0f)
            {
                return site;
            }

            float step = Mathf.Max(1f, Mathf.Max(config.CellSize, config.VoronoiCellSize * 0.75f));
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
    }
}
