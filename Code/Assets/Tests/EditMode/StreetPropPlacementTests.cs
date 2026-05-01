using System.Collections.Generic;

using Assets.Scripts.Runtime.Road.Generators;

using NUnit.Framework;

using UnityEngine;

/// <summary>
/// Placement rules for props vs road meshes: overlap with street segments, intersection roundabouts,
/// and intersection/stub roundabout surfaces used by decor and lights.
/// </summary>
public class StreetPropPlacementTests
{
    private static GameObject CreateRoundaboutChild(string objectName, Vector3 worldCenter, float xzHalfExtent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objectName;
        go.transform.position = worldCenter;
        go.transform.localScale = new Vector3(xzHalfExtent * 2f, 1f, xzHalfExtent * 2f);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    [Test]
    public void StreetRoadOverlap_PointOverStreetSegment_IsBlocked()
    {
        var segment = new RoadSegmentData(
            new Vector3(0f, 0f, 0f),
            new Vector3(20f, 0f, 0f),
            halfSpan: 4f);
        var segments = new List<RoadSegmentData> { segment };
        var candidates = new List<Vector3> { new Vector3(10f, 0f, 0f) };

        bool[] blocked = StreetRoadOverlapUtility.EvaluateRoadOverlap(candidates, segments, runParallel: false);

        Assert.AreEqual(1, blocked.Length);
        Assert.IsTrue(blocked[0], "Point on segment centre (within half-span) should count as on-road.");
    }

    [Test]
    public void StreetRoadOverlap_PointBesideStreetSegment_IsNotBlocked()
    {
        var segment = new RoadSegmentData(
            new Vector3(0f, 0f, 0f),
            new Vector3(20f, 0f, 0f),
            halfSpan: 2f);
        var segments = new List<RoadSegmentData> { segment };
        var candidates = new List<Vector3> { new Vector3(10f, 0f, 8f) };

        bool[] blocked = StreetRoadOverlapUtility.EvaluateRoadOverlap(candidates, segments, runParallel: false);

        Assert.IsFalse(blocked[0], "Point well outside lateral half-span should not count as on-road.");
    }

    [Test]
    public void StreetRoadOverlap_PointWithVerticalMismatch_IsNotBlocked()
    {
        var segment = new RoadSegmentData(
            new Vector3(0f, 0f, 0f),
            new Vector3(20f, 0f, 0f),
            halfSpan: 10f);
        var segments = new List<RoadSegmentData> { segment };
        var candidates = new List<Vector3> { new Vector3(10f, 5f, 0f) };

        bool[] blocked = StreetRoadOverlapUtility.EvaluateRoadOverlap(candidates, segments, runParallel: false);

        Assert.IsFalse(blocked[0], "Point more than 1m above road segment should not count as on-road.");
    }

    [Test]
    public void StreetRoadOverlap_EmptySegments_NoBlocks()
    {
        var candidates = new List<Vector3> { Vector3.zero };
        bool[] blocked = StreetRoadOverlapUtility.EvaluateRoadOverlap(
            candidates,
            new List<RoadSegmentData>(),
            runParallel: false);

        Assert.IsFalse(blocked[0]);
    }

    [Test]
    public void IsWorldPointOnRoundaboutOrStubSurface_IntersectionCenter_IsTrue()
    {
        var root = new GameObject("RoadRoot");
        try
        {
            GameObject rb = CreateRoundaboutChild("IntersectionRoundabout", new Vector3(50f, 1f, 50f), 12f);
            rb.transform.SetParent(root.transform, worldPositionStays: true);

            Assert.IsTrue(
                StreetDecorationGenerator.IsWorldPointOnRoundaboutOrStubSurface(
                    root.transform,
                    new Vector3(50f, 1f, 50f)));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void IsWorldPointOnRoundaboutOrStubSurface_DeadEndStubCenter_IsTrue()
    {
        var root = new GameObject("RoadRoot");
        try
        {
            GameObject rb = CreateRoundaboutChild("RoadStubRoundabout", new Vector3(0f, 0.5f, 0f), 8f);
            rb.transform.SetParent(root.transform, worldPositionStays: true);

            Assert.IsTrue(
                StreetDecorationGenerator.IsWorldPointOnRoundaboutOrStubSurface(
                    root.transform,
                    new Vector3(0f, 0.5f, 0f)));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void IsWorldPointOnRoundaboutOrStubSurface_OutsideDisc_IsFalse()
    {
        var root = new GameObject("RoadRoot");
        try
        {
            GameObject rb = CreateRoundaboutChild("IntersectionRoundabout", Vector3.zero, 2f);
            rb.transform.SetParent(root.transform, worldPositionStays: true);

            Assert.IsFalse(
                StreetDecorationGenerator.IsWorldPointOnRoundaboutOrStubSurface(
                    root.transform,
                    new Vector3(50f, 0f, 0f)));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void IsWorldPointOnIntersectionRoundabout_IntersectionChild_IsTrue()
    {
        var root = new GameObject("RoadRoot");
        try
        {
            GameObject rb = CreateRoundaboutChild("IntersectionRoundabout", new Vector3(5f, 1f, 5f), 10f);
            rb.transform.SetParent(root.transform, worldPositionStays: true);

            Assert.IsTrue(StreetLightPlacer.IsWorldPointOnIntersectionRoundabout(root.transform, new Vector3(5f, 1f, 5f)));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void IsWorldPointOnIntersectionRoundabout_StubOnly_IsFalse()
    {
        var root = new GameObject("RoadRoot");
        try
        {
            GameObject rb = CreateRoundaboutChild("RoadStubRoundabout", Vector3.zero, 10f);
            rb.transform.SetParent(root.transform, worldPositionStays: true);

            Assert.IsFalse(
                StreetLightPlacer.IsWorldPointOnIntersectionRoundabout(root.transform, Vector3.zero),
                "Light placer only filters intersection roundabouts, not dead-end stubs.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void IsWorldPointOnRoundaboutOrStubSurface_UnrelatedChild_IsFalse()
    {
        var root = new GameObject("RoadRoot");
        try
        {
            var noise = GameObject.CreatePrimitive(PrimitiveType.Cube);
            noise.name = "RoadSpline_Street";
            noise.transform.SetParent(root.transform, worldPositionStays: false);
            noise.transform.localPosition = Vector3.zero;

            Assert.IsFalse(
                StreetDecorationGenerator.IsWorldPointOnRoundaboutOrStubSurface(
                    root.transform,
                    noise.transform.position));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
