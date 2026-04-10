using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.WFC;

using NUnit.Framework;

public class TileSetTests
{
    [Test]
    public void TileSet_GridStreet_ContainsCrossTile()
    {
        var tileSet = RoadTileSetFactory.CreateStreet(UrbanMorphology.Grid);

        Assert.IsNotNull(tileSet.GetTileById("cross"));
    }

    [Test]
    public void TileSet_GridStreet_ContainsEmptyTile()
    {
        var tileSet = RoadTileSetFactory.CreateStreet(UrbanMorphology.Grid);

        Assert.IsNotNull(tileSet.GetTileById("empty"));
    }

    [Test]
    public void TileSet_GridStreet_HasExpectedTileCount()
    {
        var tileSet = RoadTileSetFactory.CreateStreet(UrbanMorphology.Grid);

        Assert.AreEqual(12, tileSet.Count);
    }

    [Test]
    public void TileSet_OrganicStreet_HasExpectedTileCount()
    {
        var tileSet = RoadTileSetFactory.CreateStreet(UrbanMorphology.Organic);

        Assert.AreEqual(12, tileSet.Count);
    }

    [Test]
    public void TileSet_IndexOf_ReturnsCorrectIndex()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        int idx = tileSet.IndexOf("empty");

        Assert.GreaterOrEqual(idx, 0);
        Assert.AreEqual("empty", tileSet.GetTile(idx).Id);
    }

    [Test]
    public void TileSet_IndexOf_ReturnsNegativeForUnknownId()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();

        Assert.AreEqual(-1, tileSet.IndexOf("nonexistent_tile"));
    }

    [Test]
    public void TileDefinition_RoadNS_HasCorrectSockets()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var tile = tileSet.GetTileById("road_ns");

        Assert.AreEqual(RoadSockets.Road, tile.GetSocket(Direction.North));
        Assert.AreEqual(RoadSockets.None, tile.GetSocket(Direction.East));
        Assert.AreEqual(RoadSockets.Road, tile.GetSocket(Direction.South));
        Assert.AreEqual(RoadSockets.None, tile.GetSocket(Direction.West));
    }

    [Test]
    public void TileDefinition_Cross_HasRoadOnAllSides()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var tile = tileSet.GetTileById("cross");

        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            Assert.AreEqual(RoadSockets.Road, tile.GetSocket(dir), $"Expected Road on {dir}");
        }
    }

    [Test]
    public void TileDefinition_Empty_HasNoneOnAllSides()
    {
        var tileSet = RoadTileSetFactory.CreateStreet();
        var tile = tileSet.GetTileById("empty");

        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            Assert.AreEqual(RoadSockets.None, tile.GetSocket(dir), $"Expected None on {dir}");
        }
    }
}
