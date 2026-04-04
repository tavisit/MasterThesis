using System.Collections.Generic;

using Assets.Scripts.Runtime.WFC;

namespace Assets.Scripts.Runtime.City
{
    public static class RoadSockets
    {
        public static readonly Socket Road = new Socket("road");
        public static readonly Socket Metro = new Socket("metro");
        public static readonly Socket None = new Socket("none");
    }

    public enum UrbanMorphology { Grid, Organic }

    public static class RoadTileSetFactory
    {
        public static TileSet CreateStreet(UrbanMorphology morphology = UrbanMorphology.Grid)
            => morphology == UrbanMorphology.Organic ? CreateOrganicStreet() : CreateGridStreet();
        private static TileSet CreateGridStreet()
        {
            var road = RoadSockets.Road;
            var none = RoadSockets.None;

            return new TileSet(new List<TileDefinition>
            {
                new TileDefinition("empty",     "Tile_Empty",     none, none, none, none, weight: 4.0),
                new TileDefinition("road_ns",   "Tile_Road_NS",   road, none, road, none, weight: 2.0),
                new TileDefinition("road_ew",   "Tile_Road_EW",   none, road, none, road, weight: 2.0),
                new TileDefinition("corner_ne", "Tile_Corner_NE", road, road, none, none, weight: 1.0),
                new TileDefinition("corner_nw", "Tile_Corner_NW", road, none, none, road, weight: 1.0),
                new TileDefinition("corner_se", "Tile_Corner_SE", none, road, road, none, weight: 1.0),
                new TileDefinition("corner_sw", "Tile_Corner_SW", none, none, road, road, weight: 1.0),
                new TileDefinition("t_nse",     "Tile_T_NSE",     road, road, road, none, weight: 0.8),
                new TileDefinition("t_nsw",     "Tile_T_NSW",     road, none, road, road, weight: 0.8),
                new TileDefinition("t_new",     "Tile_T_NEW",     road, road, none, road, weight: 0.8),
                new TileDefinition("t_sew",     "Tile_T_SEW",     none, road, road, road, weight: 0.8),
                new TileDefinition("cross",     "Tile_Cross",     road, road, road, road, weight: 0.5),
            });
        }
        private static TileSet CreateOrganicStreet()
        {
            var road = RoadSockets.Road;
            var none = RoadSockets.None;

            return new TileSet(new List<TileDefinition>
            {
                new TileDefinition("empty",     "Tile_Empty",     none, none, none, none, weight: 3.0),
                new TileDefinition("road_ns",   "Tile_Road_NS",   road, none, road, none, weight: 0.6),
                new TileDefinition("road_ew",   "Tile_Road_EW",   none, road, none, road, weight: 0.6),
                new TileDefinition("corner_ne", "Tile_Corner_NE", road, road, none, none, weight: 2.5),
                new TileDefinition("corner_nw", "Tile_Corner_NW", road, none, none, road, weight: 2.5),
                new TileDefinition("corner_se", "Tile_Corner_SE", none, road, road, none, weight: 2.5),
                new TileDefinition("corner_sw", "Tile_Corner_SW", none, none, road, road, weight: 2.5),
                new TileDefinition("t_nse",     "Tile_T_NSE",     road, road, road, none, weight: 0.4),
                new TileDefinition("t_nsw",     "Tile_T_NSW",     road, none, road, road, weight: 0.4),
                new TileDefinition("t_new",     "Tile_T_NEW",     road, road, none, road, weight: 0.4),
                new TileDefinition("t_sew",     "Tile_T_SEW",     none, road, road, road, weight: 0.4),
                new TileDefinition("cross",     "Tile_Cross",     road, road, road, road, weight: 0.1),
            });
        }
        public static TileSet CreateMetro()
        {
            var metro = RoadSockets.Metro;
            var none = RoadSockets.None;

            return new TileSet(new List<TileDefinition>
            {
                new TileDefinition("metro_empty",     "Tile_Metro_Empty",     none,  none,  none,  none,  weight: 6.0),
                new TileDefinition("metro_ns",        "Tile_Metro_NS",        metro, none,  metro, none,  weight: 3.0),
                new TileDefinition("metro_ew",        "Tile_Metro_EW",        none,  metro, none,  metro, weight: 3.0),
                new TileDefinition("metro_corner_ne", "Tile_Metro_Corner_NE", metro, metro, none,  none,  weight: 0.2),
                new TileDefinition("metro_corner_nw", "Tile_Metro_Corner_NW", metro, none,  none,  metro, weight: 0.2),
                new TileDefinition("metro_corner_se", "Tile_Metro_Corner_SE", none,  metro, metro, none,  weight: 0.2),
                new TileDefinition("metro_corner_sw", "Tile_Metro_Corner_SW", none,  none,  metro, metro, weight: 0.2),
            });
        }
        public static TileSet CreateBoulevard()
        {
            var road = RoadSockets.Road;
            var none = RoadSockets.None;

            return new TileSet(new List<TileDefinition>
            {
                new TileDefinition("empty",     "Tile_Empty",     none, none, none, none, weight: 2.0),
                new TileDefinition("road_ns",   "Tile_Road_NS",   road, none, road, none, weight: 4.0),
                new TileDefinition("road_ew",   "Tile_Road_EW",   none, road, none, road, weight: 4.0),
                new TileDefinition("corner_ne", "Tile_Corner_NE", road, road, none, none, weight: 0.3),
                new TileDefinition("corner_nw", "Tile_Corner_NW", road, none, none, road, weight: 0.3),
                new TileDefinition("corner_se", "Tile_Corner_SE", none, road, road, none, weight: 0.3),
                new TileDefinition("corner_sw", "Tile_Corner_SW", none, none, road, road, weight: 0.3),
                new TileDefinition("t_nse",     "Tile_T_NSE",     road, road, road, none, weight: 0.15),
                new TileDefinition("t_nsw",     "Tile_T_NSW",     road, none, road, road, weight: 0.15),
                new TileDefinition("t_new",     "Tile_T_NEW",     road, road, none, road, weight: 0.15),
                new TileDefinition("t_sew",     "Tile_T_SEW",     none, road, road, road, weight: 0.15),
                new TileDefinition("cross",     "Tile_Cross",     road, road, road, road, weight: 0.05),
            });
        }
    }
}
