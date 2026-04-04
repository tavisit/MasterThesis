using System.Collections.Generic;

using UnityEngine;


namespace Assets.Scripts.Runtime.Graph
{
    public enum RoadType { Street, Boulevard, Metro }

    public sealed class RoadNode
    {
        public int Id { get; }
        public Vector3 Position { get; set; }
        public RoadType Type { get; }

        public RoadNode(int id, Vector3 position, RoadType type)
        {
            Id = id;
            Position = position;
            Type = type;
        }
    }

    public sealed class RoadEdge
    {
        public RoadNode From { get; }
        public RoadNode To { get; }
        public RoadType Type { get; }

        public RoadEdge(RoadNode from, RoadNode to, RoadType type)
        {
            From = from;
            To = to;
            Type = type;
        }
    }

    public sealed class RoadGraph
    {
        private readonly List<RoadNode> _nodes = new();
        private readonly List<RoadEdge> _edges = new();
        private int _nextId;

        public IReadOnlyList<RoadNode> Nodes => _nodes;
        public IReadOnlyList<RoadEdge> Edges => _edges;

        public RoadNode AddNode(Vector3 position, RoadType type)
        {
            var node = new RoadNode(_nextId++, position, type);
            _nodes.Add(node);
            return node;
        }

        public RoadEdge AddEdge(RoadNode from, RoadNode to, RoadType type)
        {
            var edge = new RoadEdge(from, to, type);
            _edges.Add(edge);
            return edge;
        }
        public void RetainNodes(HashSet<RoadNode> keepNodes)
        {
            _nodes.RemoveAll(n => !keepNodes.Contains(n));
            _edges.RemoveAll(e => !keepNodes.Contains(e.From) || !keepNodes.Contains(e.To));
        }

        public RoadGraph SubgraphUpToEdge(int edgeCount)
        {
            var sub = new RoadGraph();
            var nodeMap = new Dictionary<RoadNode, RoadNode>();

            RoadNode Map(RoadNode n)
            {
                if (!nodeMap.TryGetValue(n, out var mapped))
                {
                    mapped = sub.AddNode(n.Position, n.Type);
                    nodeMap[n] = mapped;
                }
                return mapped;
            }

            for (int i = 0; i < Mathf.Min(edgeCount, _edges.Count); i++)
            {
                var e = _edges[i];
                sub.AddEdge(Map(e.From), Map(e.To), e.Type);
            }

            return sub;
        }

        public List<List<RoadNode>> ExtractChains()
        {
            var visited = new HashSet<RoadEdge>();
            var chains = new List<List<RoadNode>>();

            var adjacency = new Dictionary<RoadNode, List<RoadEdge>>();
            foreach (var edge in _edges)
            {
                if (!adjacency.ContainsKey(edge.From))
                {
                    adjacency[edge.From] = new();
                }

                if (!adjacency.ContainsKey(edge.To))
                {
                    adjacency[edge.To] = new();
                }

                adjacency[edge.From].Add(edge);
                adjacency[edge.To].Add(edge);
            }

            foreach (var edge in _edges)
            {
                if (visited.Contains(edge))
                {
                    continue;
                }

                var chain = new List<RoadNode> { edge.From, edge.To };
                visited.Add(edge);

                ExtendChain(chain, adjacency, visited, edge.Type, forward: true);
                ExtendChain(chain, adjacency, visited, edge.Type, forward: false);

                chains.Add(chain);
            }

            return chains;
        }

        private void ExtendChain(
            List<RoadNode> chain,
            Dictionary<RoadNode, List<RoadEdge>> adjacency,
            HashSet<RoadEdge> visited,
            RoadType type,
            bool forward)
        {
            while (true)
            {
                RoadNode tip = forward ? chain[chain.Count - 1] : chain[0];
                if (!adjacency.TryGetValue(tip, out var neighbours))
                {
                    break;
                }

                RoadEdge next = null;
                foreach (var e in neighbours)
                {
                    if (visited.Contains(e) || e.Type != type)
                    {
                        continue;
                    }

                    next = e;
                    break;
                }

                if (next == null)
                {
                    break;
                }

                visited.Add(next);
                RoadNode nextNode = next.From == tip ? next.To : next.From;

                if (forward)
                {
                    chain.Add(nextNode);
                }
                else
                {
                    chain.Insert(0, nextNode);
                }
            }
        }
    }
}
