using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.WFC;

using NUnit.Framework;

public class HybridTileWeightMultiplierTests
{
    [Test]
    public void HybridTileSet_HasGridOrganicTransitionPrefixes()
    {
        var tileSet = HybridTileSetFactory.CreateHybridStreet(0.5f);

        Assert.IsNotNull(tileSet.GetTileById("grid_road_ns"));
        Assert.IsNotNull(tileSet.GetTileById("organic_road_ns"));
        Assert.IsNotNull(tileSet.GetTileById("transition_road_ns"));
    }

    [Test]
    public void HybridTileSet_HasExpectedTileCount()
    {
        var tileSet = HybridTileSetFactory.CreateHybridStreet(0.5f);

        Assert.AreEqual(36, tileSet.Count);
    }

    [Test]
    public void ForOrganicBias_GridRoad_FavorsLowOrganicBias()
    {
        var tile = new TileDefinition("grid_road_ns", "Tile_Road_NS",
            SocketDefinitions.Road, SocketDefinitions.None,
            SocketDefinitions.Road, SocketDefinitions.None,
            weight: 1.0);

        double m0 = HybridTileWeightMultiplier.ForOrganicBias(tile, 0f);
        double m1 = HybridTileWeightMultiplier.ForOrganicBias(tile, 1f);

        Assert.Greater(m0, m1);
    }

    [Test]
    public void ForOrganicBias_OrganicRoad_FavorsHighOrganicBias()
    {
        var tile = new TileDefinition("organic_road_ns", "Tile_Road_NS",
            SocketDefinitions.Road, SocketDefinitions.None,
            SocketDefinitions.Road, SocketDefinitions.None,
            weight: 1.0);

        double m0 = HybridTileWeightMultiplier.ForOrganicBias(tile, 0f);
        double m1 = HybridTileWeightMultiplier.ForOrganicBias(tile, 1f);

        Assert.Less(m0, m1);
    }

    [Test]
    public void ForOrganicBias_TransitionRoad_PeaksNearHalf()
    {
        var tile = new TileDefinition("transition_road_ns", "Tile_Road_NS",
            SocketDefinitions.Road, SocketDefinitions.None,
            SocketDefinitions.Road, SocketDefinitions.None,
            weight: 1.0);

        double mid = HybridTileWeightMultiplier.ForOrganicBias(tile, 0.5f);
        double low = HybridTileWeightMultiplier.ForOrganicBias(tile, 0f);
        double high = HybridTileWeightMultiplier.ForOrganicBias(tile, 1f);

        Assert.Greater(mid, low);
        Assert.Greater(mid, high);
    }

    [Test]
    public void ForOrganicBias_UnknownPrefix_ReturnsOne()
    {
        var tile = new TileDefinition("custom_road_ns", "Tile_Road_NS",
            SocketDefinitions.Road, SocketDefinitions.None,
            SocketDefinitions.Road, SocketDefinitions.None,
            weight: 1.0);

        Assert.AreEqual(1.0, HybridTileWeightMultiplier.ForOrganicBias(tile, 0.5f));
    }

    [Test]
    public void ForOrganicBias_NullTile_ReturnsOne()
    {
        Assert.AreEqual(1.0, HybridTileWeightMultiplier.ForOrganicBias(null, 0.5f));
    }

    [Test]
    public void ForOrganicBias_EmptyTile_ScalesByEmptyFactor()
    {
        var gridEmpty = new TileDefinition("grid_empty", "Tile_Empty",
            SocketDefinitions.None, SocketDefinitions.None,
            SocketDefinitions.None, SocketDefinitions.None,
            weight: 1.0);
        var gridRoad = new TileDefinition("grid_road_ns", "Tile_Road_NS",
            SocketDefinitions.Road, SocketDefinitions.None,
            SocketDefinitions.Road, SocketDefinitions.None,
            weight: 1.0);

        double emptyW = HybridTileWeightMultiplier.ForOrganicBias(gridEmpty, 0.3f);
        double roadW = HybridTileWeightMultiplier.ForOrganicBias(gridRoad, 0.3f);

        Assert.Less(emptyW, roadW);
    }
}
