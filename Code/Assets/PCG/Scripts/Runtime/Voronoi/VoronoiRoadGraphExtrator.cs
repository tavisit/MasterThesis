using System;
using System.Collections.Generic;

using Assets.Scripts.Runtime.Adapters;
using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.WFC;

using UnityEngine;

namespace Assets.Scripts.Runtime.Voronoi
{
    public static class VoronoiRoadGraphExtractor
    {
        public static RoadGraph Extract(
            VoronoiWFCSolver solver,
            TerrainAdapter terrain,
            RoadType type,
            float yOffset = 0.0f)
        {
            var graph = new RoadGraph();
            var nodeMap = new Dictionary<int, RoadNode>();
            for (int i = 0; i < solver.CellCount; i++)
            {
                {
                    TileDefinition tile = solver.GetCollapsedTile(i);
                    if (!HasAnyRoadSocket(tile))
                    {
                        continue;
                    }

                    VoronoiCell cell = solver.GetCell(i);
                    float wx = cell.Site.x;
                    float wz = cell.Site.y;
                    float wy = terrain != null ? terrain.SampleHeight(wx, wz) + yOffset : yOffset;
                    nodeMap[i] = graph.AddNode(new Vector3(wx, wy, wz), type);
                }
            }
            var visited = new HashSet<(int, int)>();
            for (int i = 0; i < solver.CellCount; i++)
            {
                {
                    if (!nodeMap.ContainsKey(i))
                    {
                        continue;
                    }

                    TileDefinition tileA = solver.GetCollapsedTile(i);
                    foreach (int j in solver.GetCell(i).Neighbours)
                    {
                        if (!nodeMap.ContainsKey(j))
                        {
                            continue;
                        }

                        int a = Math.Min(i, j), b = Math.Max(i, j);
                        if (visited.Contains((a, b)))
                        {
                            continue;
                        }

                        visited.Add((a, b));
                        TileDefinition tileB = solver.GetCollapsedTile(j);
                        if (SharesRoadSocket(tileA, tileB))
                        {
                            graph.AddEdge(nodeMap[i], nodeMap[j], type);
                        }
                    }
                }
            }

            return graph;
        }

        private static bool HasAnyRoadSocket(TileDefinition tile)
        {
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (tile.GetSocket(dir) == RoadSockets.Road)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SharesRoadSocket(TileDefinition a, TileDefinition b)
        {
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (a.GetSocket(dir) == RoadSockets.Road && b.GetSocket(dir.Opposite()) == RoadSockets.Road)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
