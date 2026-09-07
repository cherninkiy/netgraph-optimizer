using NetGraph.Core;
using Xunit;

namespace NetGraph.Tests;

public class KShortestPathsTests
{
    private static NetworkGraph SampleGraph()
    {
        var links = new List<Link>
        {
            new(0, 1, 100f, 1f, 1f),  // 0-1-3 : 2
            new(1, 3, 100f, 1f, 1f),
            new(0, 2, 100f, 2f, 2f),  // 0-2-3 : 3
            new(2, 3, 100f, 1f, 1f),
            new(0, 3, 100f, 10f, 10f) // прямой : 10
        };
        return new NetworkGraph(links);
    }

    [Fact]
    public void FindsPathsSortedByLatency()
    {
        var finder = new KShortestPathsFinder();
        var paths = finder.FindKPaths(SampleGraph(), 0, 3, k: 3, maxLatency: float.MaxValue);

        Assert.Equal(3, paths.Count);
        Assert.Equal(2f, paths[0].Latency, 3);
        Assert.Equal(3f, paths[1].Latency, 3);
        Assert.Equal(10f, paths[2].Latency, 3);
        Assert.Equal(new[] { 0, 1, 3 }, paths[0].Nodes);
    }

    [Fact]
    public void LatencyCutoff_DiscardsSlowPaths()
    {
        var finder = new KShortestPathsFinder();
        var paths = finder.FindKPaths(SampleGraph(), 0, 3, k: 5, maxLatency: 5f);

        Assert.Equal(2, paths.Count);
        Assert.All(paths, p => Assert.True(p.Latency <= 5f));
    }

    [Fact]
    public void PathsAreSimple_NoRepeatedNodes()
    {
        var graph = GraphGenerator.CreateRandom(100, 200, new Random(7));
        var finder = new KShortestPathsFinder();

        foreach (var p in finder.FindKPaths(graph, 0, 99, k: 5, maxLatency: float.MaxValue))
            Assert.Equal(p.Nodes.Length, p.Nodes.Distinct().Count());
    }
}
