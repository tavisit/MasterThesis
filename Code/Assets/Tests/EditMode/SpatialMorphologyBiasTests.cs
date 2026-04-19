using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Voronoi;

using NUnit.Framework;

using UnityEngine;

public class SpatialMorphologyBiasTests
{
    private static CityNucleus[] SingleNucleus(Vector2 centre, float radius)
    {
        return new[]
        {
            new CityNucleus { Centre = centre, Radius = radius, Strength = 1f }
        };
    }

    [Test]
    public void MinSignedDistance_InsideDisc_IsNegative()
    {
        var nuclei = SingleNucleus(Vector2.zero, 10f);
        float d = SpatialMorphologyBias.MinSignedDistanceToNuclei(new Vector2(5f, 0f), nuclei);

        Assert.Less(d, 0f);
    }

    [Test]
    public void MinSignedDistance_OutsideDisc_IsPositive()
    {
        var nuclei = SingleNucleus(Vector2.zero, 10f);
        float d = SpatialMorphologyBias.MinSignedDistanceToNuclei(new Vector2(20f, 0f), nuclei);

        Assert.Greater(d, 0f);
    }

    [Test]
    public void ComputeSpatialOrganicBias_OrganicNearNuclei_InsideIsOneOutsideIsZero()
    {
        var nuclei = SingleNucleus(Vector2.zero, 10f);

        float inside = SpatialMorphologyBias.ComputeSpatialOrganicBias(
            new Vector2(5f, 0f),
            nuclei,
            SpatialMorphologyGradient.OrganicNearNuclei_GridFar,
            falloffWorld: 40f);
        float outside = SpatialMorphologyBias.ComputeSpatialOrganicBias(
            new Vector2(50f, 0f),
            nuclei,
            SpatialMorphologyGradient.OrganicNearNuclei_GridFar,
            falloffWorld: 40f);

        Assert.AreEqual(1f, inside);
        Assert.AreEqual(0f, outside);
    }

    [Test]
    public void ComputeSpatialOrganicBias_GridNearNuclei_InsideIsZeroOutsideApproachesOne()
    {
        var nuclei = SingleNucleus(Vector2.zero, 10f);

        float inside = SpatialMorphologyBias.ComputeSpatialOrganicBias(
            new Vector2(0f, 0f),
            nuclei,
            SpatialMorphologyGradient.GridNearNuclei_OrganicFar,
            falloffWorld: 20f);
        float outside = SpatialMorphologyBias.ComputeSpatialOrganicBias(
            new Vector2(100f, 0f),
            nuclei,
            SpatialMorphologyGradient.GridNearNuclei_OrganicFar,
            falloffWorld: 20f);

        Assert.AreEqual(0f, inside);
        Assert.AreEqual(1f, outside);
    }

    [Test]
    public void ComputeSpatialOrganicBias_GridNear_FalloffAtHalfDistance()
    {
        var nuclei = SingleNucleus(Vector2.zero, 10f);
        float bias = SpatialMorphologyBias.ComputeSpatialOrganicBias(
            new Vector2(20f, 0f),
            nuclei,
            SpatialMorphologyGradient.GridNearNuclei_OrganicFar,
            falloffWorld: 20f);

        Assert.AreEqual(0.5f, bias, 1e-5f);
    }

    [Test]
    public void ComputeOrganicBiasPerCell_NoNuclei_FillsUniformBlend()
    {
        var cells = new List<VoronoiCell>
        {
            new VoronoiCell(0, Vector2.zero),
            new VoronoiCell(1, Vector2.one * 50f)
        };

        float[] bias = SpatialMorphologyBias.ComputeOrganicBiasPerCell(
            cells,
            null,
            SpatialMorphologyGradient.OrganicNearNuclei_GridFar,
            falloffWorld: 40f,
            uniformBlend: 0.35f,
            spatialInfluence: 1f);

        Assert.AreEqual(2, bias.Length);
        Assert.AreEqual(0.35f, bias[0]);
        Assert.AreEqual(0.35f, bias[1]);
    }

    [Test]
    public void ComputeOrganicBiasPerCell_SpatialInfluenceZero_IgnoresNuclei()
    {
        var cells = new List<VoronoiCell> { new VoronoiCell(0, Vector2.zero) };
        var nuclei = SingleNucleus(Vector2.zero, 100f);

        float[] bias = SpatialMorphologyBias.ComputeOrganicBiasPerCell(
            cells,
            nuclei,
            SpatialMorphologyGradient.OrganicNearNuclei_GridFar,
            falloffWorld: 40f,
            uniformBlend: 0.2f,
            spatialInfluence: 0f);

        Assert.AreEqual(0.2f, bias[0]);
    }
}
