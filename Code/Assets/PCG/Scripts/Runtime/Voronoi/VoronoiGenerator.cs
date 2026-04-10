using System.Collections.Generic;

using UnityEngine;

namespace Assets.Scripts.Runtime.Voronoi
{
    public sealed class VoronoiCell
    {
        public int Id { get; }
        public Vector2 Site { get; }
        public List<Vector2> Vertices { get; }
        public List<int> Neighbours { get; }

        public VoronoiCell(int id, Vector2 site)
        {
            Id = id;
            Site = site;
            Vertices = new List<Vector2>();
            Neighbours = new List<int>();
        }
    }
    public static class VoronoiGenerator
    {
        public static List<VoronoiCell> Generate(
            Vector2[] sites,
            float worldWidth,
            float worldHeight,
            int resolution = 256)
        {
            var cells = new VoronoiCell[sites.Length];
            for (int i = 0; i < sites.Length; i++)
            {
                cells[i] = new VoronoiCell(i, sites[i]);
            }

            int[,] ownership = JumpFlood(sites, worldWidth, worldHeight, resolution);
            BuildNeighbours(cells, ownership, resolution);
            BuildPolygons(cells, sites, ownership, worldWidth, worldHeight, resolution);

            return new List<VoronoiCell>(cells);
        }

        private static int[,] JumpFlood(
            Vector2[] sites, float w, float h, int res)
        {
            var map = new int[res, res];
            var dist = new float[res, res];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    map[y, x] = -1;
                    dist[y, x] = float.MaxValue;
                }
            }
            foreach (var (site, idx) in Indexed(sites))
            {
                int px = Mathf.Clamp((int)(site.x / w * res), 0, res - 1);
                int py = Mathf.Clamp((int)(site.y / h * res), 0, res - 1);
                map[py, px] = idx;
                dist[py, px] = 0f;
            }

            for (int step = res / 2; step >= 1; step /= 2)
            {
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float px = x / (float)res * w;
                        float py = y / (float)res * h;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx * step;
                                int ny = y + dy * step;
                                if (nx < 0 || nx >= res || ny < 0 || ny >= res)
                                {
                                    continue;
                                }

                                if (map[ny, nx] < 0)
                                {
                                    continue;
                                }

                                int sid = map[ny, nx];
                                float d = Vector2.Distance(new Vector2(px, py), sites[sid]);
                                if (d < dist[y, x])
                                {
                                    dist[y, x] = d;
                                    map[y, x] = sid;
                                }
                            }
                        }
                    }
                }
            }

            return map;
        }

        private static void BuildNeighbours(VoronoiCell[] cells, int[,] map, int res)
        {
            for (int y = 0; y < res - 1; y++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    int a = map[y, x];
                    int b = map[y, x + 1];
                    int c = map[y + 1, x];

                    if (a >= 0 && b >= 0 && a != b)
                    {
                        if (!cells[a].Neighbours.Contains(b))
                        {
                            cells[a].Neighbours.Add(b);
                        }

                        if (!cells[b].Neighbours.Contains(a))
                        {
                            cells[b].Neighbours.Add(a);
                        }
                    }
                    if (a >= 0 && c >= 0 && a != c)
                    {
                        if (!cells[a].Neighbours.Contains(c))
                        {
                            cells[a].Neighbours.Add(c);
                        }

                        if (!cells[c].Neighbours.Contains(a))
                        {
                            cells[c].Neighbours.Add(a);
                        }
                    }
                }
            }
        }

        private static void BuildPolygons(
            VoronoiCell[] cells, Vector2[] sites,
            int[,] map, float w, float h, int res)
        {
            var buckets = new List<Vector2>[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                buckets[i] = new List<Vector2>();
            }

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int id = map[y, x];
                    if (id < 0)
                    {
                        continue;
                    }

                    bool isBoundary = false;
                    if (x > 0 && map[y, x - 1] != id)
                    {
                        isBoundary = true;
                    }

                    if (x < res - 1 && map[y, x + 1] != id)
                    {
                        isBoundary = true;
                    }

                    if (y > 0 && map[y - 1, x] != id)
                    {
                        isBoundary = true;
                    }

                    if (y < res - 1 && map[y + 1, x] != id)
                    {
                        isBoundary = true;
                    }

                    if (isBoundary)
                    {
                        buckets[id].Add(new Vector2(x / (float)res * w, y / (float)res * h));
                    }
                }
            }

            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Vertices.AddRange(ConvexHull(buckets[i]));
            }
        }
        private static List<Vector2> ConvexHull(List<Vector2> points)
        {
            if (points.Count < 3)
            {
                return points;
            }

            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            var hull = new List<Vector2>();
            foreach (var p in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(p);
            }
            int lower = hull.Count + 1;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(points[i]);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
            => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        private static IEnumerable<(T item, int index)> Indexed<T>(T[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                yield return (arr[i], i);
            }
        }
    }
}
