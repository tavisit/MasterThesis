using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.WFC;

using NUnit.Framework;

public class WFCSolverTests
{
    [Test]
    public void WFCSolver_GridMorphology_SolvesSuccessfully()
    {
        var tileSet = RoadTileSetFactory.CreateStreet(UrbanMorphology.Grid);
        var solver = new WFCSolver(tileSet, rows: 5, columns: 5, seed: 42);

        Assert.AreEqual(SolveResult.Success, solver.Solve());
    }

    [Test]
    public void WFCSolver_OrganicMorphology_SolvesSuccessfully()
    {
        var tileSet = RoadTileSetFactory.CreateStreet(UrbanMorphology.Organic);
        var solver = new WFCSolver(tileSet, rows: 5, columns: 5, seed: 42);

        Assert.AreEqual(SolveResult.Success, solver.Solve());
    }

    [Test]
    public void WFCSolver_CollapseCount_EqualsGridSize()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var solver = new WFCSolver(tileSet, rows: 4, columns: 4, seed: 1);
        solver.Solve();

        Assert.AreEqual(16, solver.CollapseCount);
    }

    [Test]
    public void WFCSolver_DifferentSeeds_ProduceDifferentResults()
    {
        var solver1 = new WFCSolver(RoadTileSetFactory.CreateStreet(), rows: 6, columns: 6, seed: 1);
        var solver2 = new WFCSolver(RoadTileSetFactory.CreateStreet(), rows: 6, columns: 6, seed: 99);
        solver1.Solve();
        solver2.Solve();

        bool anyDifference = false;
        for (int r = 0; r < 6 && !anyDifference; r++)
        {
            for (int c = 0; c < 6 && !anyDifference; c++)
            {
                if (solver1.GetCollapsedIndex(r, c) != solver2.GetCollapsedIndex(r, c))
                {
                    anyDifference = true;
                }
            }
        }

        Assert.IsTrue(anyDifference, "Different seeds should produce different layouts.");
    }

    [Test]
    public void WFCSolver_SameSeed_ProducesSameResult()
    {
        var solver1 = new WFCSolver(RoadTileSetFactory.CreateStreet(), rows: 5, columns: 5, seed: 7);
        var solver2 = new WFCSolver(RoadTileSetFactory.CreateStreet(), rows: 5, columns: 5, seed: 7);
        solver1.Solve();
        solver2.Solve();

        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                Assert.AreEqual(
                    solver1.GetCollapsedIndex(r, c),
                    solver2.GetCollapsedIndex(r, c),
                    $"Mismatch at [{r},{c}]");
            }
        }
    }

    [Test]
    public void WFCSolver_CollapsedTiles_AreAllValid()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var solver = new WFCSolver(tileSet, rows: 4, columns: 4, seed: 10);
        solver.Solve();

        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                int idx = solver.GetCollapsedIndex(r, c);
                Assert.GreaterOrEqual(idx, 0, $"Tile at [{r},{c}] has invalid index.");
                Assert.Less(idx, tileSet.Count, $"Tile at [{r},{c}] exceeds tile set size.");
            }
        }
    }

    [Test]
    public void WFCSolver_ThrowsOnNullTileSet()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new WFCSolver(null, rows: 5, columns: 5));
    }

    [Test]
    public void WFCSolver_ThrowsOnZeroRows()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new WFCSolver(tileSet, rows: 0, columns: 5));
    }

    [Test]
    public void WFCSolver_ThrowsOnZeroColumns()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new WFCSolver(tileSet, rows: 5, columns: 0));
    }

    [Test]
    public void WFCSolver_ApplyConstraint_ForcesSpecificTile()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var solver = new WFCSolver(tileSet, rows: 3, columns: 3, seed: 5);

        solver.ApplyConstraint(1, 1, new[] { "empty" });
        solver.Solve();

        Assert.AreEqual("empty", solver.GetCollapsedTile(1, 1).Id);
    }

    [Test]
    public void WFCSolver_LargeGrid_SolvesWithinIterationLimit()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var solver = new WFCSolver(tileSet, rows: 20, columns: 20, seed: 42);

        var result = solver.Solve(maxIterations: 10_000_000);

        Assert.AreNotEqual(SolveResult.IterationLimitReached, result);
    }
}
