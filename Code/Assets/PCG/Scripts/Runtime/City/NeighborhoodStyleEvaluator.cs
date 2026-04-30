using System.Collections.Generic;

using UnityEngine;

namespace Assets.Scripts.Runtime.City
{
    public readonly struct NeighborhoodStyleSample
    {
        public readonly float RoadWidthMultiplier;
        public readonly float SidewalkWidth;
        public readonly float SidewalkVerticalOffset;
        public readonly float LightPostInterval;
        public readonly float SidewalkPropSpawnChance;
        public readonly float SidewalkPropInterval;
        public readonly float BoulevardInteriorSidewalkWidth;
        public readonly float BoulevardInteriorSidewalkVerticalOffset;
        public readonly float BoulevardInteriorLightPostInterval;
        public readonly float BoulevardInteriorPropSpawnChance;
        public readonly float BoulevardInteriorPropInterval;
        public readonly Material RoadMaterial;
        public readonly Material BoulevardMaterial;
        public readonly Material SidewalkMaterial;
        public readonly Material BoulevardInteriorSidewalkMaterial;
        public readonly GameObject LightPostPrefab;
        public readonly GameObject MetroStationEntrancePrefab;
        public readonly GameObject BoulevardInteriorLightPostPrefab;
        public readonly List<GameObject> SidewalkPropPrefabs;
        public readonly List<GameObject> BoulevardInteriorPropPrefabs;

        public NeighborhoodStyleSample(
            float roadWidthMultiplier,
            float sidewalkWidth,
            float sidewalkVerticalOffset,
            float lightPostInterval,
            float sidewalkPropSpawnChance,
            float sidewalkPropInterval,
            float boulevardInteriorSidewalkWidth,
            float boulevardInteriorSidewalkVerticalOffset,
            float boulevardInteriorLightPostInterval,
            float boulevardInteriorPropSpawnChance,
            float boulevardInteriorPropInterval,
            Material roadMaterial,
            Material boulevardMaterial,
            Material sidewalkMaterial,
            Material boulevardInteriorSidewalkMaterial,
            GameObject lightPostPrefab,
            GameObject metroStationEntrancePrefab,
            GameObject boulevardInteriorLightPostPrefab,
            List<GameObject> sidewalkPropPrefabs,
            List<GameObject> boulevardInteriorPropPrefabs)
        {
            RoadWidthMultiplier = roadWidthMultiplier;
            SidewalkWidth = sidewalkWidth;
            SidewalkVerticalOffset = sidewalkVerticalOffset;
            LightPostInterval = lightPostInterval;
            SidewalkPropSpawnChance = sidewalkPropSpawnChance;
            SidewalkPropInterval = sidewalkPropInterval;
            BoulevardInteriorSidewalkWidth = boulevardInteriorSidewalkWidth;
            BoulevardInteriorSidewalkVerticalOffset = boulevardInteriorSidewalkVerticalOffset;
            BoulevardInteriorLightPostInterval = boulevardInteriorLightPostInterval;
            BoulevardInteriorPropSpawnChance = boulevardInteriorPropSpawnChance;
            BoulevardInteriorPropInterval = boulevardInteriorPropInterval;
            RoadMaterial = roadMaterial;
            BoulevardMaterial = boulevardMaterial;
            SidewalkMaterial = sidewalkMaterial;
            BoulevardInteriorSidewalkMaterial = boulevardInteriorSidewalkMaterial;
            LightPostPrefab = lightPostPrefab;
            MetroStationEntrancePrefab = metroStationEntrancePrefab;
            BoulevardInteriorLightPostPrefab = boulevardInteriorLightPostPrefab;
            SidewalkPropPrefabs = sidewalkPropPrefabs;
            BoulevardInteriorPropPrefabs = boulevardInteriorPropPrefabs;
        }

        public NeighborhoodStyleSample ScaledBySidewalkRatio()
        {
            float ratio = Mathf.Clamp(RoadWidthMultiplier, 0.01f, 2.5f);
            return new NeighborhoodStyleSample(
                roadWidthMultiplier: RoadWidthMultiplier,
                sidewalkWidth: Mathf.Max(0.1f, SidewalkWidth * ratio),
                sidewalkVerticalOffset: SidewalkVerticalOffset,
                lightPostInterval: LightPostInterval,
                sidewalkPropSpawnChance: SidewalkPropSpawnChance,
                sidewalkPropInterval: SidewalkPropInterval,
                boulevardInteriorSidewalkWidth: Mathf.Max(0.1f, BoulevardInteriorSidewalkWidth * ratio),
                boulevardInteriorSidewalkVerticalOffset: BoulevardInteriorSidewalkVerticalOffset,
                boulevardInteriorLightPostInterval: BoulevardInteriorLightPostInterval,
                boulevardInteriorPropSpawnChance: BoulevardInteriorPropSpawnChance,
                boulevardInteriorPropInterval: BoulevardInteriorPropInterval,
                roadMaterial: RoadMaterial,
                boulevardMaterial: BoulevardMaterial,
                sidewalkMaterial: SidewalkMaterial,
                boulevardInteriorSidewalkMaterial: BoulevardInteriorSidewalkMaterial,
                lightPostPrefab: LightPostPrefab,
                metroStationEntrancePrefab: MetroStationEntrancePrefab,
                boulevardInteriorLightPostPrefab: BoulevardInteriorLightPostPrefab,
                sidewalkPropPrefabs: SidewalkPropPrefabs,
                boulevardInteriorPropPrefabs: BoulevardInteriorPropPrefabs);
        }
    }

    public static class NeighborhoodStyleEvaluator
    {
        public static NeighborhoodStyleSample Evaluate(Vector3 worldPos, CityNucleus[] nuclei)
        {
            if (nuclei == null || nuclei.Length == 0)
            {
                return DefaultSample();
            }

            float roadWidthMul = 0f;
            float sidewalkWidth = 0f;
            float sidewalkVerticalOffset = 0f;
            float lightPostInterval = 0f;
            float propSpawnChance = 0f;
            float propInterval = 0f;
            float interiorSidewalkWidth = 0f;
            float interiorSidewalkVerticalOffset = 0f;
            float interiorLightPostInterval = 0f;
            float interiorPropSpawnChance = 0f;
            float interiorPropInterval = 0f;
            float sumW = 0f;

            float bestW = -1f;
            Material bestRoadMat = null;
            Material bestBoulevardMat = null;
            Material bestSidewalkMat = null;
            Material bestInteriorSidewalkMat = null;
            GameObject bestLampPrefab = null;
            GameObject bestMetroEntrancePrefab = null;
            GameObject bestInteriorLampPrefab = null;

            var mergedPropPrefabs = new List<GameObject>();
            var seenPropPrefabs = new HashSet<GameObject>();
            var mergedInteriorPropPrefabs = new List<GameObject>();
            var seenInteriorPropPrefabs = new HashSet<GameObject>();

            Vector2 p = new Vector2(worldPos.x, worldPos.z);
            for (int i = 0; i < nuclei.Length; i++)
            {
                NeighborhoodProfile profile = nuclei[i].Profile;
                if (profile == null)
                {
                    continue;
                }

                float radius = Mathf.Max(0.001f, nuclei[i].Radius);
                float blendDistance = Mathf.Max(1f, radius * 0.9f);
                float dist = Vector2.Distance(p, nuclei[i].Centre);
                float outside = Mathf.Max(0f, dist - radius);
                float x = outside / blendDistance;
                float weight = Mathf.Exp(-(x * x)) * Mathf.Max(0.01f, nuclei[i].Strength);

                sumW += weight;
                roadWidthMul += profile.RoadWidthMultiplier * weight;
                sidewalkWidth += profile.SidewalkWidth * weight;
                sidewalkVerticalOffset += profile.SidewalkVerticalOffset * weight;
                lightPostInterval += profile.LightPostInterval * weight;
                propSpawnChance += profile.SidewalkPropSpawnChance * weight;
                propInterval += profile.SidewalkPropInterval * weight;
                interiorSidewalkWidth += profile.BoulevardInteriorSidewalkWidth * weight;
                interiorSidewalkVerticalOffset += profile.BoulevardInteriorSidewalkVerticalOffset * weight;
                interiorLightPostInterval += profile.BoulevardInteriorLightPostInterval * weight;
                interiorPropSpawnChance += profile.BoulevardInteriorPropSpawnChance * weight;
                interiorPropInterval += profile.BoulevardInteriorPropInterval * weight;

                if (weight > 1e-6f && profile.SidewalkPropPrefabs != null)
                {
                    foreach (GameObject propPrefab in profile.SidewalkPropPrefabs)
                    {
                        if (propPrefab != null && seenPropPrefabs.Add(propPrefab))
                        {
                            mergedPropPrefabs.Add(propPrefab);
                        }
                    }
                }

                if (weight > 1e-6f && profile.BoulevardInteriorPropPrefabs != null)
                {
                    foreach (GameObject propPrefab in profile.BoulevardInteriorPropPrefabs)
                    {
                        if (propPrefab != null && seenInteriorPropPrefabs.Add(propPrefab))
                        {
                            mergedInteriorPropPrefabs.Add(propPrefab);
                        }
                    }
                }

                if (weight > bestW)
                {
                    bestW = weight;
                    bestRoadMat = profile.RoadMaterial;
                    bestBoulevardMat = profile.BoulevardMaterial;
                    bestSidewalkMat = profile.SidewalkMaterial;
                    bestInteriorSidewalkMat = profile.BoulevardInteriorSidewalkMaterial;
                    bestLampPrefab = profile.LightPostPrefab;
                    bestMetroEntrancePrefab = profile.MetroStationEntrancePrefab;
                    bestInteriorLampPrefab = profile.BoulevardInteriorLightPostPrefab;
                }
            }

            if (sumW <= 1e-5f)
            {
                return DefaultSample();
            }

            return new NeighborhoodStyleSample(
                roadWidthMultiplier: Mathf.Clamp(roadWidthMul / sumW, 0.5f, 2.5f),
                sidewalkWidth: Mathf.Max(0.2f, sidewalkWidth / sumW),
                sidewalkVerticalOffset: sidewalkVerticalOffset / sumW,
                lightPostInterval: Mathf.Max(4f, lightPostInterval / sumW),
                sidewalkPropSpawnChance: Mathf.Clamp01(propSpawnChance / sumW),
                sidewalkPropInterval: Mathf.Max(4f, propInterval / sumW),
                boulevardInteriorSidewalkWidth: Mathf.Max(0.2f, interiorSidewalkWidth / sumW),
                boulevardInteriorSidewalkVerticalOffset: interiorSidewalkVerticalOffset / sumW,
                boulevardInteriorLightPostInterval: Mathf.Max(4f, interiorLightPostInterval / sumW),
                boulevardInteriorPropSpawnChance: Mathf.Clamp01(interiorPropSpawnChance / sumW),
                boulevardInteriorPropInterval: Mathf.Max(4f, interiorPropInterval / sumW),
                roadMaterial: bestRoadMat,
                boulevardMaterial: bestBoulevardMat,
                sidewalkMaterial: bestSidewalkMat,
                boulevardInteriorSidewalkMaterial: bestInteriorSidewalkMat,
                lightPostPrefab: bestLampPrefab,
                metroStationEntrancePrefab: bestMetroEntrancePrefab,
                boulevardInteriorLightPostPrefab: bestInteriorLampPrefab,
                sidewalkPropPrefabs: mergedPropPrefabs.Count > 0 ? mergedPropPrefabs : null,
                boulevardInteriorPropPrefabs: mergedInteriorPropPrefabs.Count > 0 ? mergedInteriorPropPrefabs : null);
        }

        private static NeighborhoodStyleSample DefaultSample()
            => new NeighborhoodStyleSample(
                1f, 1f, 0.05f, 28f, 0.45f, 36f,
                1.6f, 0.08f, 24f, 0.35f, 28f,
                null, null, null, null, null, null, null, null, null);
    }
}
