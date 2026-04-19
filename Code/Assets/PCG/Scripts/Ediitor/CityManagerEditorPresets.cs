#if UNITY_EDITOR
using Assets.Scripts.Runtime.City;

namespace Assets.Scripts.Editor
{
    internal static class CityManagerEditorPresets
    {
        public readonly struct VoronoiSpatialConfiguration
        {
            public VoronoiSpatialConfiguration(
                float morphologyBlend,
                float spatialInfluence,
                float gridTopologyInfluence,
                float nucleusFalloffWorld,
                SpatialMorphologyGradient gradient)
            {
                MorphologyBlend = morphologyBlend;
                SpatialInfluence = spatialInfluence;
                GridTopologyInfluence = gridTopologyInfluence;
                NucleusFalloffWorld = nucleusFalloffWorld;
                Gradient = gradient;
            }

            public readonly float MorphologyBlend;
            public readonly float SpatialInfluence;
            public readonly float GridTopologyInfluence;
            public readonly float NucleusFalloffWorld;
            public readonly SpatialMorphologyGradient Gradient;
        }

        public static readonly VoronoiSpatialConfiguration GridCoreVoronoiOutside = new VoronoiSpatialConfiguration(
            morphologyBlend: 0.42f,
            spatialInfluence: 0.95f,
            gridTopologyInfluence: 0.88f,
            nucleusFalloffWorld: 85f,
            gradient: SpatialMorphologyGradient.GridNearNuclei_OrganicFar);

        public static readonly VoronoiSpatialConfiguration VoronoiCoreGridOutside = new VoronoiSpatialConfiguration(
            morphologyBlend: 0.52f,
            spatialInfluence: 1f,
            gridTopologyInfluence: 1f,
            nucleusFalloffWorld: 28f,
            gradient: SpatialMorphologyGradient.OrganicNearNuclei_GridFar);
    }
}
#endif
