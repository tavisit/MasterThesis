using Assets.Scripts.Runtime.Voronoi;
using Assets.Scripts.Runtime.WFC;

using UnityEngine;

namespace Assets.Scripts.Runtime.Adapters
{
    public sealed class TerrainAdapter : MonoBehaviour
    {
        [Header("Terrain")]
        [SerializeField] private Terrain _terrain;

        [Header("Constraints")]
        [SerializeField] private float _maxRoadSlopeDegrees = 15f;
        [SerializeField] private float _seaLevel = 0f;

        public float SampleHeight(float worldX, float worldZ)
        {
            if (_terrain == null)
            {
                return 0f;
            }

            return _terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));
        }

        public void ApplyTerrainConstraints(WFCSolver solver, int rows, int columns, float cellSize)
        {
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float worldX = c * cellSize;
                    float worldZ = r * cellSize;

                    if (IsBelowSeaLevel(worldX, worldZ) || IsOutsideBounds(worldX, worldZ))
                    {
                        solver.ApplyConstraint(r, c, new[] { "empty" });
                        continue;
                    }

                    if (ExceedsSlopeThreshold(worldX, worldZ, cellSize))
                    {
                        solver.ApplyConstraint(r, c, new[] { "empty" });
                    }
                }
            }
        }

        public void ApplyTerrainConstraintsVoronoi(VoronoiWFCSolver solver)
        {
            for (int i = 0; i < solver.CellCount; i++)
            {
                VoronoiCell cell = solver.GetCell(i);
                float wx = cell.Site.x;
                float wz = cell.Site.y;

                if (IsBelowSeaLevel(wx, wz) || IsOutsideBounds(wx, wz))
                {
                    solver.ApplyConstraint(i, new[] { "empty" });
                    continue;
                }

                if (cell.Vertices.Count >= 2)
                {
                    float hMin = float.MaxValue;
                    float hMax = float.MinValue;
                    foreach (var v in cell.Vertices)
                    {
                        float h = SampleHeight(v.x, v.y);
                        hMin = Mathf.Min(hMin, h);
                        hMax = Mathf.Max(hMax, h);
                    }
                    float delta = hMax - hMin;
                    float cellSize = Mathf.Max(1f, cell.Vertices.Count);
                    float slopeDeg = Mathf.Atan2(delta, cellSize) * Mathf.Rad2Deg;
                    if (slopeDeg > _maxRoadSlopeDegrees)
                    {
                        solver.ApplyConstraint(i, new[] { "empty" });
                    }
                }
            }
        }

        private bool IsBelowSeaLevel(float wx, float wz)
            => SampleHeight(wx, wz) <= _seaLevel;

        private bool IsOutsideBounds(float wx, float wz)
        {
            if (_terrain == null)
            {
                return false;
            }

            Bounds b = _terrain.terrainData.bounds;
            Vector3 origin = _terrain.transform.position;
            return wx < origin.x || wz < origin.z
                || wx > origin.x + b.size.x
                || wz > origin.z + b.size.z;
        }

        private bool ExceedsSlopeThreshold(float wx, float wz, float cellSize)
        {
            float h00 = SampleHeight(wx, wz);
            float h10 = SampleHeight(wx + cellSize, wz);
            float h01 = SampleHeight(wx, wz + cellSize);
            float h11 = SampleHeight(wx + cellSize, wz + cellSize);

            float maxDelta = Mathf.Max(
                Mathf.Abs(h10 - h00),
                Mathf.Abs(h01 - h00),
                Mathf.Abs(h11 - h00));

            float slopeDegrees = Mathf.Atan2(maxDelta, cellSize) * Mathf.Rad2Deg;
            return slopeDegrees > _maxRoadSlopeDegrees;
        }
    }
}
