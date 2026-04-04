using System;
using System.Collections.Generic;

namespace Assets.Scripts.Runtime.WFC
{
    public readonly struct Socket : IEquatable<Socket>
    {
        public readonly string Id;

        public Socket(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Socket id required.", nameof(id));
            }

            Id = id;
        }

        public bool Equals(Socket other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is Socket s && Equals(s);
        public override int GetHashCode() => Id?.GetHashCode() ?? 0;
        public override string ToString() => Id;

        public static bool operator ==(Socket a, Socket b) => a.Equals(b);
        public static bool operator !=(Socket a, Socket b) => !a.Equals(b);
    }

    public enum Direction { North = 0, East = 1, South = 2, West = 3 }

    public static class DirectionExtensions
    {
        public static Direction Opposite(this Direction d) => d switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => throw new ArgumentOutOfRangeException(nameof(d))
        };

        public static (int dc, int dr) ToOffset(this Direction d) => d switch
        {
            Direction.North => (0, 1),
            Direction.South => (0, -1),
            Direction.East => (1, 0),
            Direction.West => (-1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(d))
        };
    }

    public sealed class TileDefinition
    {
        public string Id { get; }
        public string AssetName { get; }
        public double Weight { get; }

        private readonly Socket[] _sockets;

        public TileDefinition(string id, string assetName,
            Socket north, Socket east, Socket south, Socket west,
            double weight = 1.0)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Id required.", nameof(id));
            }

            if (weight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            Id = id;
            AssetName = assetName;
            Weight = weight;
            _sockets = new[] { north, east, south, west };
        }

        public Socket GetSocket(Direction d) => _sockets[(int)d];

        public override string ToString() => $"Tile({Id})";
    }

    public sealed class TileSet
    {
        private readonly TileDefinition[] _tiles;
        private readonly HashSet<int>[][] _adjacency;

        public int Count => _tiles.Length;

        public TileSet(IReadOnlyList<TileDefinition> tiles)
        {
            if (tiles == null || tiles.Count == 0)
            {
                throw new ArgumentException("At least one tile required.", nameof(tiles));
            }

            _tiles = new TileDefinition[tiles.Count];
            _adjacency = new HashSet<int>[tiles.Count][];

            for (int i = 0; i < tiles.Count; i++)
            {
                _tiles[i] = tiles[i] ?? throw new ArgumentNullException($"tiles[{i}]");
                _adjacency[i] = new HashSet<int>[4];
                for (int d = 0; d < 4; d++)
                {
                    _adjacency[i][d] = new HashSet<int>();
                }
            }

            BuildAdjacencyTable();
        }

        private void BuildAdjacencyTable()
        {
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                Direction opp = dir.Opposite();
                for (int a = 0; a < _tiles.Length; a++)
                {
                    Socket socketA = _tiles[a].GetSocket(dir);
                    for (int b = 0; b < _tiles.Length; b++)
                    {
                        if (socketA == _tiles[b].GetSocket(opp))
                        {
                            _adjacency[a][(int)dir].Add(b);
                        }
                    }
                }
            }
        }

        public TileDefinition GetTile(int index) => _tiles[index];
        public IReadOnlyCollection<int> GetCompatible(int tileIndex, Direction dir) => _adjacency[tileIndex][(int)dir];

        public int IndexOf(string tileId)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i].Id == tileId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
