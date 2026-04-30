namespace Assets.Scripts.Runtime.Road.Generators
{
    // Shared tuning values for procedural road generation.
    public static class RoadGenerationOffsets
    {
        // Small floor values used by width/spacing math.
        public const float MinPositive = 0.01f;

        // Base vertical offsets for generated road and sidewalk meshes.
        public const float RoadMeshVerticalOffset = 0.02f;
        public const float SidewalkMeshVerticalOffset = 0.01f;

        // UV scaling used by all roundabout meshes.
        public const float RoundaboutRoadUvWorldTiling = 1f;
        public const float RoundaboutSidewalkUvWorldTiling = 1f;

        // Geometry shared by intersection and dead-end roundabouts.
        public const float RoundaboutEdgeYDrop = 0.08f;
        public const float RoundaboutMinRadius = 1.6f;
        public const float RoundaboutWidthMultiplier = 1.05f;
        public const float RoundaboutRadiusScale = 1.3f;
        public const float RoundaboutSidewalkWidth = 1.25f;
        public const int RoundaboutDiscSegments = 22;
        public const int RoundaboutSidewalkSegments = 26;

        // Placement offsets for intersection roundabouts.
        public const float IntersectionRoundaboutRoadYOffset = 0.1f;
        public const float IntersectionRoundaboutSidewalkYOffsetDelta = -0.11f;

        // Placement offsets for dead-end roundabouts.
        public const float DeadEndRoundaboutCenterOutwardFactor = 0.18f;
        public const float DeadEndRoundaboutRoadYOffset = 0.0009f;
        public const float DeadEndRoundaboutSidewalkYOffsetDelta = -0.025f;

        // Extra vertical nudge for boulevard interior decor.
        public const float BoulevardInteriorDecorationYOffsetDelta = -0.02f;

        // Sidewalk and decor sampling defaults.
        public const float SidewalkRingDensityScale = 0.75f;
        public const float SidewalkMinMeshResolution = 1f;
        public const float SidewalkMinHalfWidth = 0.1f;
        public const float PlacementFirstSampleRatio = 0.5f;
        public const float PropIntervalJitterRatio = 0.3f;
        public const float SidewalkCenterOffsetRatio = 0.5f;
        public const float LateralCenterSnapEpsilon = 1e-4f;

        // Metro entrance probing around street and boulevard sidewalks.
        public const float MetroEntranceSideOffsetClearance = 1.6f;
        public const float MetroEntranceProbeMinStep = 0.6f;
        public const float MetroEntranceProbeStepSidewalkFactor = 0.45f;
        public const float MetroEntranceProbeMinDistance = 6f;
        public const float MetroEntranceProbeMaxDistanceSidewalkFactor = 4f;
        public const int MetroEntranceProbeMinTries = 4;
        public const float MetroEntranceSidewalkToleranceMin = 0.8f;
        public const float MetroEntranceSidewalkToleranceFactor = 0.9f;
        public const float MetroEntranceMinSidewalkInset = 0.45f;
        public const float MetroEntranceMinSidewalkInsetFactor = 0.35f;
        public const float MetroEntranceMaxSidewalkInset = 1.25f;
        public const float MetroEntranceMaxSidewalkInsetFactor = 1.45f;
        public const float StreetCorridorPadding = 0.2f;
        public const float StreetUnderPadding = 0.35f;
        public const float EntranceSlotGridSize = 2.5f;
        public const float SidewalkPointMinYDelta = -1f;
        public const float SidewalkPointMaxYDelta = 2.5f;
        public const float EntranceOccupancyRadius = 1.25f;
        public const float EntranceOccupancyHalfHeight = 1.6f;
    }
}
