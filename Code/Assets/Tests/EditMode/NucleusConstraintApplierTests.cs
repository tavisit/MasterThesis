using System.Collections.Generic;

using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.WFC;

using NUnit.Framework;

using UnityEngine;

public class NucleusConstraintApplierTests
{
    [Test]
    public void NucleusConstraint_EmptyNuclei_DoesNotThrow()
    {
        var solver = new WFCSolver(RoadTileSetFactory.CreateStreet(), rows: 5, columns: 5, seed: 1);

        Assert.DoesNotThrow(() =>
            NucleusConstraintApplier.Apply(solver, new List<CityNucleus>(), 5, 5, 10f));
    }

    [Test]
    public void NucleusConstraint_NullNuclei_DoesNotThrow()
    {
        var solver = new WFCSolver(RoadTileSetFactory.CreateStreet(), rows: 5, columns: 5, seed: 1);

        Assert.DoesNotThrow(() =>
            NucleusConstraintApplier.Apply(solver, null, 5, 5, 10f));
    }

    [Test]
    public void NucleusConstraint_Applied_SolverStillSucceeds()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var solver = new WFCSolver(tileSet, rows: 8, columns: 8, seed: 42);

        var nuclei = new List<CityNucleus>
        {
            new CityNucleus { Centre = new Vector2(40f, 40f), Radius = 30f, Strength = 2f }
        };

        NucleusConstraintApplier.Apply(solver, nuclei, 8, 8, 10f);

        Assert.AreEqual(SolveResult.Success, solver.Solve());
    }

    [Test]
    public void NucleusConstraint_HighStrength_SolverStillSucceeds()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var solver = new WFCSolver(tileSet, rows: 6, columns: 6, seed: 5);

        var nuclei = new List<CityNucleus>
        {
            new CityNucleus { Centre = new Vector2(30f, 30f), Radius = 50f, Strength = 5f }
        };

        NucleusConstraintApplier.Apply(solver, nuclei, 6, 6, 10f);

        Assert.AreEqual(SolveResult.Success, solver.Solve());
    }

    [Test]
    public void NucleusConstraint_MultipleNuclei_SolverStillSucceeds()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var solver = new WFCSolver(tileSet, rows: 10, columns: 10, seed: 99);

        var nuclei = new List<CityNucleus>
        {
            new CityNucleus { Centre = new Vector2(20f, 20f), Radius = 20f, Strength = 1f },
            new CityNucleus { Centre = new Vector2(80f, 80f), Radius = 20f, Strength = 3f },
            new CityNucleus { Centre = new Vector2(50f, 50f), Radius = 15f, Strength = 2f }
        };

        NucleusConstraintApplier.Apply(solver, nuclei, 10, 10, 10f);

        Assert.AreEqual(SolveResult.Success, solver.Solve());
    }

    [Test]
    public void NucleusConstraint_HybridTilePrefixes_SolverStillSucceeds()
    {
        var tileSet = HybridTileSetFactory.CreateHybridStreet(0.5f);
        var solver = new WFCSolver(tileSet, rows: 8, columns: 8, seed: 7);

        var nuclei = new List<CityNucleus>
        {
            new CityNucleus { Centre = new Vector2(40f, 40f), Radius = 25f, Strength = 2f }
        };

        NucleusConstraintApplier.Apply(solver, nuclei, 8, 8, 10f);

        Assert.AreEqual(SolveResult.Success, solver.Solve());
    }
}
