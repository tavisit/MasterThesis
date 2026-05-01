using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.Graph;
using Assets.Scripts.Runtime.Road.Generators;

using NUnit.Framework;

using UnityEngine;

public class BoulevardGeneratorTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("BoulevardTests_Root");
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
        {
            Object.DestroyImmediate(_root);
        }
    }

    private static RoadGraph BuildLongStreetLine()
    {
        var graph = new RoadGraph();
        RoadNode prev = null;
        for (int i = 0; i <= 20; i++)
        {
            RoadNode n = graph.AddNode(new Vector3(i * 10f, 0f, 0f));
            if (prev != null)
            {
                graph.AddEdge(prev, n);
            }

            prev = n;
        }

        return graph;
    }

    private static CityNucleus[] FourNucleiAlongStreet()
    {
        return new[]
        {
            new CityNucleus { Centre = new Vector2(0f, 0f), Radius = 50f, Strength = 1f },
            new CityNucleus { Centre = new Vector2(40f, 0f), Radius = 50f, Strength = 1f },
            new CityNucleus { Centre = new Vector2(80f, 0f), Radius = 50f, Strength = 1f },
            new CityNucleus { Centre = new Vector2(120f, 0f), Radius = 50f, Strength = 1f },
        };
    }

    [Test]
    public void Generate_MaxLinesOne_EnforcesMinimumSpanningBoulevardCount()
    {
        RoadGraph graph = BuildLongStreetLine();
        CityNucleus[] nuclei = FourNucleiAlongStreet();
        var settings = ScriptableObject.CreateInstance<RoadSettings>();

        var containers = BoulevardGenerator.Generate(
            graph,
            nuclei,
            _root.transform,
            settings,
            UrbanMorphology.Grid,
            bearingPenaltyWeight: 0.3f,
            maxLines: 1);

        Assert.GreaterOrEqual(
            containers.Count,
            3,
            "Four nuclei require at least three boulevard path splines when maxLines is raised to validNucleusCount - 1.");
    }

    [Test]
    public void BuildPriorityEdgeKeys_MaxLinesOne_ReturnsNonEmptyEdgeSet()
    {
        RoadGraph graph = BuildLongStreetLine();
        CityNucleus[] nuclei = FourNucleiAlongStreet();

        var keys = BoulevardGenerator.BuildPriorityEdgeKeys(
            graph,
            nuclei,
            bearingPenaltyWeight: 0.3f,
            maxLines: 1);

        Assert.Greater(keys.Count, 0, "Priority edge keys should be produced for boulevard routing.");
    }
}
