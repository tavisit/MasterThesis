using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.MeshRelated;

using UnityEngine;

namespace Assets.Scripts.Runtime.Road.Generators
{
    internal sealed class MetroEntrancePlacer
    {
        private readonly CityManager _manager;
        private readonly RoadSettings _roadSettings;
        private readonly MetroEntranceLocator _locator;

        internal MetroEntrancePlacer(
            CityManager manager,
            Transform root,
            RoadSettings roadSettings,
            HashSet<Vector2Int> placedEntranceCells)
        {
            _manager = manager;
            _roadSettings = roadSettings;
            _locator = new MetroEntranceLocator(manager, root, roadSettings, placedEntranceCells);
        }

        internal void PlaceStationEntrances(
            Vector3 railPos,
            Vector3 tangent,
            Transform parent)
        {
            NeighborhoodStyleSample style = NeighborhoodStyleEvaluator.Evaluate(railPos, _manager.Nuclei);
            float styleRoadMul = Mathf.Max(0.01f, style.RoadWidthMultiplier);
            float streetHalfWidth = RoadMeshExtruder.GetHalfWidth(RoadType.Street, _roadSettings) * styleRoadMul;
            float sidewalkWidth = Mathf.Max(0.2f, style.SidewalkWidth);
            float sideOffset = streetHalfWidth + sidewalkWidth + RoadGenerationOffsets.MetroEntranceSideOffsetClearance;
            Vector3 flatForward = Vector3.ProjectOnPlane(tangent, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 1e-6f)
            {
                flatForward = Vector3.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;
            if (right.sqrMagnitude < 1e-6f)
            {
                right = Vector3.right;
            }

            if (_locator.TryFindValidSidewalkSpot(
                    railPos,
                    right,
                    sideSign: -1f,
                    initialOffset: sideOffset,
                    sidewalkWidth: sidewalkWidth,
                    out Vector3 leftPos))
            {
                Vector3 leftForward = _locator.TryGetNearestStreetTangent(leftPos, out Vector3 leftStreetTangent)
                    ? leftStreetTangent
                    : flatForward;
                MetroEntranceBuilder.Build(
                    parent,
                    leftPos,
                    leftForward,
                    "MetroEntrance_Left",
                    style.MetroStationEntrancePrefab,
                    _manager.MetroStationMaterial,
                    _manager.TerrainAdapter);
            }

            if (_locator.TryFindValidSidewalkSpot(
                    railPos,
                    right,
                    sideSign: 1f,
                    initialOffset: sideOffset,
                    sidewalkWidth: sidewalkWidth,
                    out Vector3 rightPos))
            {
                Vector3 rightForward = _locator.TryGetNearestStreetTangent(rightPos, out Vector3 rightStreetTangent)
                    ? rightStreetTangent
                    : flatForward;
                MetroEntranceBuilder.Build(
                    parent,
                    rightPos,
                    rightForward,
                    "MetroEntrance_Right",
                    style.MetroStationEntrancePrefab,
                    _manager.MetroStationMaterial,
                    _manager.TerrainAdapter);
            }
        }

        internal bool IsPointUnderStreet(Vector3 worldPoint)
        {
            return _locator.IsPointUnderStreet(worldPoint);
        }
    }
}
