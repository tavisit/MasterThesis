using System.Collections.Generic;

using UnityEngine;

namespace Assets.Scripts.Runtime.Graph
{
    public sealed class RoadIntersectionInfo
    {
        public Vector3 Position { get; }
        public IReadOnlyList<Vector3> ApproachDirections { get; }

        public RoadIntersectionInfo(Vector3 position, List<Vector3> approachDirections)
        {
            Position = position;
            ApproachDirections = approachDirections;
        }
    }

    public static class RoadIntersectionExtractor
    {
        public static List<RoadIntersectionInfo> Extract(
            RoadGraph graph,
            float minIntersectionAngleDegrees = 20f)
        {
            var result = new List<RoadIntersectionInfo>();
            if (graph == null || graph.Nodes == null || graph.Edges == null)
            {
                return result;
            }

            var adjacency = new Dictionary<RoadNode, List<RoadEdge>>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                adjacency[graph.Nodes[i]] = new List<RoadEdge>();
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                RoadEdge edge = graph.Edges[i];
                if (edge == null || edge.From == null || edge.To == null)
                {
                    continue;
                }

                if (adjacency.TryGetValue(edge.From, out var fromList))
                {
                    fromList.Add(edge);
                }
                if (adjacency.TryGetValue(edge.To, out var toList))
                {
                    toList.Add(edge);
                }
            }

            float mergeDot = Mathf.Cos(minIntersectionAngleDegrees * Mathf.Deg2Rad);

            foreach (var pair in adjacency)
            {
                RoadNode node = pair.Key;
                List<RoadEdge> edges = pair.Value;
                if (node == null || edges == null || edges.Count < 3)
                {
                    continue;
                }

                var directions = new List<Vector3>();
                for (int i = 0; i < edges.Count; i++)
                {
                    RoadEdge edge = edges[i];
                    RoadNode other = edge.From == node ? edge.To : edge.From;
                    if (other == null)
                    {
                        continue;
                    }

                    Vector3 d = other.Position - node.Position;
                    d.y = 0f;
                    if (d.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }
                    d.Normalize();

                    bool duplicate = false;
                    for (int j = 0; j < directions.Count; j++)
                    {
                        if (Vector3.Dot(directions[j], d) >= mergeDot)
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                    {
                        directions.Add(d);
                    }
                }

                if (directions.Count >= 3)
                {
                    result.Add(new RoadIntersectionInfo(node.Position, directions));
                }
            }

            return result;
        }
    }
}
