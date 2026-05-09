using UnityEngine;

namespace Assets.Scripts.Runtime.MeshRelated
{
    /// <summary>Lane-mark look for <c>PCG/Asphalt With Road Marks</c>. Boulevard = denser dashes / four lanes.</summary>
    public enum RoadLaneSurfacePreset
    {
        Auto,
        None,
        Street,
        Boulevard
    }

    /// <summary>Pushes asphalt lane uniforms into a <see cref="MaterialPropertyBlock"/> (street vs boulevard tuning).</summary>
    public static class RoadLaneSurfaceMark
    {
        // Matches legacy Assets/PCG/Shaders/RoadLaneShader.shader street tuning.
        private const float StreetRoadStartU = 0.15f;
        private const float StreetRoadEndU = 0.85f;
        private const float StreetLaneLineWidth = 0.012f;
        private const float StreetDashLength = 3.2f;
        private const float StreetGapLength = 2.4f;
        private const float StreetCenterLineWidth = 0.014f;
        private const float StreetCenterLineGap = 0.014f;

        // Boulevard: wider paint band, finer strokes; UV span stays 0–1.
        private const float BoulevardRoadStartU = 0.08f;
        private const float BoulevardRoadEndU = 0.92f;
        private const float BoulevardLaneLineWidth = 0.009f;
        private const float BoulevardDashLength = 4.0f;
        private const float BoulevardGapLength = 2.0f;
        private const float BoulevardCenterLineWidth = 0.011f;
        private const float BoulevardCenterLineGap = 0.012f;

        private static readonly Vector4 DefaultLaneColor = new(0.96f, 0.96f, 0.92f, 1f);

        private static readonly int LaneCountId = Shader.PropertyToID("_LaneCount");
        private static readonly int RoadStartUId = Shader.PropertyToID("_RoadStartU");
        private static readonly int RoadEndUId = Shader.PropertyToID("_RoadEndU");
        private static readonly int LaneLineWidthId = Shader.PropertyToID("_LaneLineWidth");
        private static readonly int DashLengthId = Shader.PropertyToID("_DashLength");
        private static readonly int GapLengthId = Shader.PropertyToID("_GapLength");
        private static readonly int CenterLineWidthId = Shader.PropertyToID("_CenterLineWidth");
        private static readonly int CenterLineGapId = Shader.PropertyToID("_CenterLineGap");
        private static readonly int LaneMarkingColorId = Shader.PropertyToID("_LaneMarkingColor");
        private static readonly int CenterLineColorId = Shader.PropertyToID("_CenterLineColor");

        public static RoadLaneSurfacePreset ResolvePreset(RoadLaneSurfacePreset requested, Transform root, int laneCount)
        {
            if (requested != RoadLaneSurfacePreset.Auto)
            {
                return requested;
            }

            if (laneCount <= 0)
            {
                return RoadLaneSurfacePreset.None;
            }

            string n = root != null ? root.name : string.Empty;
            if (n == "RoadSpline_Boulevard")
            {
                return RoadLaneSurfacePreset.Boulevard;
            }

            return RoadLaneSurfacePreset.Street;
        }

        public static void Apply(MaterialPropertyBlock block, RoadLaneSurfacePreset preset, int laneCount)
        {
            block.SetFloat(LaneCountId, Mathf.Max(0f, laneCount));

            switch (preset)
            {
                case RoadLaneSurfacePreset.None:
                    block.SetFloat(RoadStartUId, 1f);
                    block.SetFloat(RoadEndUId, 0f);
                    ApplyStrokeWidths(block, StreetLaneLineWidth, StreetDashLength, StreetGapLength, StreetCenterLineWidth, StreetCenterLineGap);
                    break;
                case RoadLaneSurfacePreset.Boulevard:
                    block.SetFloat(RoadStartUId, BoulevardRoadStartU);
                    block.SetFloat(RoadEndUId, BoulevardRoadEndU);
                    ApplyStrokeWidths(block, BoulevardLaneLineWidth, BoulevardDashLength, BoulevardGapLength, BoulevardCenterLineWidth, BoulevardCenterLineGap);
                    break;
                default:
                    block.SetFloat(RoadStartUId, StreetRoadStartU);
                    block.SetFloat(RoadEndUId, StreetRoadEndU);
                    ApplyStrokeWidths(block, StreetLaneLineWidth, StreetDashLength, StreetGapLength, StreetCenterLineWidth, StreetCenterLineGap);
                    break;
            }

            block.SetColor(LaneMarkingColorId, DefaultLaneColor);
            block.SetColor(CenterLineColorId, DefaultLaneColor);
        }

        private static void ApplyStrokeWidths(
            MaterialPropertyBlock block,
            float laneWidth,
            float dash,
            float gap,
            float centerWidth,
            float centerGap)
        {
            block.SetFloat(LaneLineWidthId, laneWidth);
            block.SetFloat(DashLengthId, dash);
            block.SetFloat(GapLengthId, gap);
            block.SetFloat(CenterLineWidthId, centerWidth);
            block.SetFloat(CenterLineGapId, centerGap);
        }
    }
}
