using Assets.Scripts.Runtime.WFC;

using NUnit.Framework;

public class SocketAndDirectionTests
{
    [Test]
    public void Socket_EqualityOperator_SameId_ReturnsTrue()
    {
        var a = new Socket("road");
        var b = new Socket("road");

        Assert.IsTrue(a == b);
    }

    [Test]
    public void Socket_EqualityOperator_DifferentId_ReturnsFalse()
    {
        var a = new Socket("road");
        var b = new Socket("none");

        Assert.IsFalse(a == b);
    }

    [Test]
    public void Socket_ThrowsOnNullId()
    {
        Assert.Throws<System.ArgumentException>(() => new Socket(null));
    }

    [Test]
    public void Socket_ThrowsOnEmptyId()
    {
        Assert.Throws<System.ArgumentException>(() => new Socket(""));
    }

    [Test]
    public void Socket_ToString_ReturnsId()
    {
        var socket = new Socket("road");

        Assert.AreEqual("road", socket.ToString());
    }

    [Test]
    public void Direction_Opposite_North_ReturnsSouth()
    {
        Assert.AreEqual(Direction.South, Direction.North.Opposite());
    }

    [Test]
    public void Direction_Opposite_East_ReturnsWest()
    {
        Assert.AreEqual(Direction.West, Direction.East.Opposite());
    }

    [Test]
    public void Direction_Opposite_South_ReturnsNorth()
    {
        Assert.AreEqual(Direction.North, Direction.South.Opposite());
    }

    [Test]
    public void Direction_Opposite_West_ReturnsEast()
    {
        Assert.AreEqual(Direction.East, Direction.West.Opposite());
    }

    [Test]
    public void Direction_Opposite_IsSymmetric()
    {
        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            Assert.AreEqual(dir, dir.Opposite().Opposite(),
                $"Opposite of opposite should be self for {dir}");
        }
    }
}
