using System.Collections.Generic;

using UnityEngine;

namespace Assets.Scripts.Runtime.City
{
    [CreateAssetMenu(
        fileName = "NeighborhoodProfile",
        menuName = "PCG/City/Neighborhood Profile")]
    public sealed class NeighborhoodProfile : ScriptableObject
    {
        [Header("Road style")]
        [Range(0.6f, 2.0f)]
        public float RoadWidthMultiplier = 1.0f;
        public Material RoadMaterial;
        public Material BoulevardMaterial;

        [Header("Sidewalk style")]
        public Material SidewalkMaterial;
        [Min(0.2f)]
        public float SidewalkWidth = 1.0f;
        public float SidewalkVerticalOffset = 0.05f;

        [Header("Street decor style")]
        public GameObject LightPostPrefab;
        [Min(0.01f)]
        public float LightPostInterval = 28f;
        public GameObject MetroStationEntrancePrefab;
        [Range(0f, 1f)]
        public float SidewalkPropSpawnChance = 0.45f;
        [Min(0.01f)]
        public float SidewalkPropInterval = 36f;
        public List<GameObject> SidewalkPropPrefabs = new();

        [Header("Boulevard interior sidewalk style")]
        public Material BoulevardInteriorSidewalkMaterial;
        [Min(0.2f)]
        public float BoulevardInteriorSidewalkWidth = 1.6f;
        public float BoulevardInteriorSidewalkVerticalOffset = 0.08f;
        public GameObject BoulevardInteriorLightPostPrefab;
        [Min(0.01f)]
        public float BoulevardInteriorLightPostInterval = 24f;
        [Range(0f, 1f)]
        public float BoulevardInteriorPropSpawnChance = 0.35f;
        [Min(0.01f)]
        public float BoulevardInteriorPropInterval = 28f;
        public List<GameObject> BoulevardInteriorPropPrefabs = new();
    }
}
