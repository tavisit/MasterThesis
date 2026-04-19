using System.Collections.Generic;

using Assets.Scripts.Runtime.WFC;

using UnityEngine;

namespace Assets.Scripts.Runtime.City
{
    public static class HybridTileSetFactory
    {
        public static TileSet CreateHybridStreet(float gridBias = 0.5f)
        {
            var road = SocketDefinitions.Road;
            var none = SocketDefinitions.None;

            var tiles = new List<TileDefinition>();

            gridBias = Mathf.Clamp01(gridBias);

            float gridWeight = Mathf.Max(0.01f, gridBias);
            float organicWeight = Mathf.Max(0.01f, 1.0f - gridBias);
            float transitionWeight = 0.3f;

            tiles.AddRange(GetGridTiles(road, none, gridWeight));
            tiles.AddRange(GetOrganicTiles(road, none, organicWeight));
            tiles.AddRange(GetTransitionTiles(road, none, transitionWeight));

            return new TileSet(tiles);
        }

        private static List<TileDefinition> GetGridTiles(Socket road, Socket none, double weight)
        {
            double baseWeight = System.Math.Max(0.01, weight);

            return new List<TileDefinition>
            {
                new TileDefinition("grid_empty",     "Tile_Empty",     none, none, none, none, weight: 1.8 * baseWeight),
                new TileDefinition("grid_road_ns",   "Tile_Road_NS",   road, none, road, none, weight: 2.0 * baseWeight),
                new TileDefinition("grid_road_ew",   "Tile_Road_EW",   none, road, none, road, weight: 2.0 * baseWeight),
                new TileDefinition("grid_corner_ne", "Tile_Corner_NE", road, road, none, none, weight: 1.0 * baseWeight),
                new TileDefinition("grid_corner_nw", "Tile_Corner_NW", road, none, none, road, weight: 1.0 * baseWeight),
                new TileDefinition("grid_corner_se", "Tile_Corner_SE", none, road, road, none, weight: 1.0 * baseWeight),
                new TileDefinition("grid_corner_sw", "Tile_Corner_SW", none, none, road, road, weight: 1.0 * baseWeight),
                new TileDefinition("grid_t_nse",     "Tile_T_NSE",     road, road, road, none, weight: System.Math.Max(0.01, 0.8 * baseWeight)),
                new TileDefinition("grid_t_nsw",     "Tile_T_NSW",     road, none, road, road, weight: System.Math.Max(0.01, 0.8 * baseWeight)),
                new TileDefinition("grid_t_new",     "Tile_T_NEW",     road, road, none, road, weight: System.Math.Max(0.01, 0.8 * baseWeight)),
                new TileDefinition("grid_t_sew",     "Tile_T_SEW",     none, road, road, road, weight: System.Math.Max(0.01, 0.8 * baseWeight)),
                new TileDefinition("grid_cross",     "Tile_Cross",     road, road, road, road, weight: System.Math.Max(0.01, 0.5 * baseWeight)),
            };
        }

        private static List<TileDefinition> GetOrganicTiles(Socket road, Socket none, double weight)
        {
            double baseWeight = System.Math.Max(0.01, weight);

            return new List<TileDefinition>
            {
                new TileDefinition("organic_empty",     "Tile_Empty",     none, none, none, none, weight: 1.35 * baseWeight),
                new TileDefinition("organic_road_ns",   "Tile_Road_NS",   road, none, road, none, weight: System.Math.Max(0.01, 1.0 * baseWeight)),
                new TileDefinition("organic_road_ew",   "Tile_Road_EW",   none, road, none, road, weight: System.Math.Max(0.01, 1.0 * baseWeight)),
                new TileDefinition("organic_corner_ne", "Tile_Corner_NE", road, road, none, none, weight: 2.5 * baseWeight),
                new TileDefinition("organic_corner_nw", "Tile_Corner_NW", road, none, none, road, weight: 2.5 * baseWeight),
                new TileDefinition("organic_corner_se", "Tile_Corner_SE", none, road, road, none, weight: 2.5 * baseWeight),
                new TileDefinition("organic_corner_sw", "Tile_Corner_SW", none, none, road, road, weight: 2.5 * baseWeight),
                new TileDefinition("organic_t_nse",     "Tile_T_NSE",     road, road, road, none, weight: System.Math.Max(0.01, 0.4 * baseWeight)),
                new TileDefinition("organic_t_nsw",     "Tile_T_NSW",     road, none, road, road, weight: System.Math.Max(0.01, 0.4 * baseWeight)),
                new TileDefinition("organic_t_new",     "Tile_T_NEW",     road, road, none, road, weight: System.Math.Max(0.01, 0.4 * baseWeight)),
                new TileDefinition("organic_t_sew",     "Tile_T_SEW",     none, road, road, road, weight: System.Math.Max(0.01, 0.4 * baseWeight)),
                new TileDefinition("organic_cross",     "Tile_Cross",     road, road, road, road, weight: System.Math.Max(0.01, 0.1 * baseWeight)),
            };
        }

        private static List<TileDefinition> GetTransitionTiles(Socket road, Socket none, double weight)
        {
            double baseWeight = System.Math.Max(0.01, weight);

            return new List<TileDefinition>
            {
                new TileDefinition("transition_empty",     "Tile_Empty",     none, none, none, none, weight: 1.0 * baseWeight),
                new TileDefinition("transition_road_ns",   "Tile_Road_NS",   road, none, road, none, weight: 1.5 * baseWeight),
                new TileDefinition("transition_road_ew",   "Tile_Road_EW",   none, road, none, road, weight: 1.5 * baseWeight),
                new TileDefinition("transition_corner_ne", "Tile_Corner_NE", road, road, none, none, weight: 1.5 * baseWeight),
                new TileDefinition("transition_corner_nw", "Tile_Corner_NW", road, none, none, road, weight: 1.5 * baseWeight),
                new TileDefinition("transition_corner_se", "Tile_Corner_SE", none, road, road, none, weight: 1.5 * baseWeight),
                new TileDefinition("transition_corner_sw", "Tile_Corner_SW", none, none, road, road, weight: 1.5 * baseWeight),
                new TileDefinition("transition_t_nse",     "Tile_T_NSE",     road, road, road, none, weight: 1.0 * baseWeight),
                new TileDefinition("transition_t_nsw",     "Tile_T_NSW",     road, none, road, road, weight: 1.0 * baseWeight),
                new TileDefinition("transition_t_new",     "Tile_T_NEW",     road, road, none, road, weight: 1.0 * baseWeight),
                new TileDefinition("transition_t_sew",     "Tile_T_SEW",     none, road, road, road, weight: 1.0 * baseWeight),
                new TileDefinition("transition_cross",     "Tile_Cross",     road, road, road, road, weight: System.Math.Max(0.01, 0.5 * baseWeight)),
            };
        }
    }
}
