using Assets.Scripts.Runtime.Adapters;
using Assets.Scripts.Runtime.WFC;

using UnityEngine;

namespace Assets.Scripts.Runtime.Graph
{
    public static class RoadGraphExtractor
    {
        public const float MetroDepth = -10f;

        public static RoadGraph Extract(
            WFCSolver solver,
            int rows,
            int columns,
            float cellSize,
            TerrainAdapter terrain,
            RoadType type,
            Socket socket,
            float yOffset = 0.0f)
        {
            var graph = new RoadGraph();
            var nodeGrid = new RoadNode[rows, columns];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    TileDefinition tile = solver.GetCollapsedTile(r, c);
                    if (!HasSocket(tile, socket))
                    {
                        continue;
                    }

                    float wx = c * cellSize;
                    float wz = r * cellSize;
                    float wy = type == RoadType.Metro
                        ? MetroDepth
                        : SampleMaxHeight(terrain, wx, wz, cellSize) + yOffset;

                    nodeGrid[r, c] = graph.AddNode(new Vector3(wx, wy, wz), type);
                }
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    RoadNode nodeA = nodeGrid[r, c];
                    if (nodeA == null)
                    {
                        continue;
                    }

                    TileDefinition tileA = solver.GetCollapsedTile(r, c);

                    if (c + 1 < columns && nodeGrid[r, c + 1] != null)
                    {
                        TileDefinition tileB = solver.GetCollapsedTile(r, c + 1);
                        if (tileA.GetSocket(Direction.East) == socket &&
                            tileB.GetSocket(Direction.West) == socket)
                        {
                            graph.AddEdge(nodeA, nodeGrid[r, c + 1], type);
                        }
                    }

                    if (r + 1 < rows && nodeGrid[r + 1, c] != null)
                    {
                        TileDefinition tileB = solver.GetCollapsedTile(r + 1, c);
                        if (tileA.GetSocket(Direction.North) == socket &&
                            tileB.GetSocket(Direction.South) == socket)
                        {
                            graph.AddEdge(nodeA, nodeGrid[r + 1, c], type);
                        }
                    }
                }
            }

            return graph;
        }
        private static float SampleMaxHeight(TerrainAdapter terrain, float wx, float wz, float half)
        {
            if (terrain == null)
            {
                return 0f;
            }

            float max = float.MinValue;
            for (int si = 0; si <= 2; si++)
            {
                for (int sj = 0; sj <= 2; sj++)
                {
                    max = Mathf.Max(max, terrain.SampleHeight(wx + si * half * 0.5f, wz + sj * half * 0.5f));
                }
            }

            return max;
        }

        private static bool HasSocket(TileDefinition tile, Socket socket)
        {
            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                if (tile.GetSocket(dir) == socket)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
