using NetGraph.Core;
using Xunit;

namespace NetGraph.Tests;

public class GraphTests
{
    private static NetworkGraph SampleGraph()
    {
        var links = new List<Link>
        {
            new(0, 1, 100f, 1f, 1f),
            new(0, 2, 100f, 2f, 2f),
            new(1, 0, 100f, 1f, 1f),
            new(1, 3, 100f, 1f, 1f),
            new(2, 3, 100f, 1f, 1f),
            new(3, 1, 100f, 1f, 1f),
        };
        return new NetworkGraph(links);
    }

    [Fact]
    public void Csr_TraversesNeighborsWithoutLoss()
    {
        var graph = SampleGraph();

        Assert.Equal(4, graph.NodeCount);
        Assert.Equal(6, graph.LinksCount);

        var neighbors = graph.GetNeighborEdges(0);
        Assert.Equal(2, neighbors.Length);
        var targets = neighbors.ToArray().Select(e => graph.GetLink(e).To).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2 }, targets);
    }

    [Fact]
    public void Csr_EveryNodeCovered()
    {
        var graph = GraphGenerator.CreateRandom(200, 400, new Random(1));

        int total = 0;
        for (int n = 0; n < graph.NodeCount; n++)
            total += graph.GetNeighborEdges(n).Length;

        Assert.Equal(graph.LinksCount, total);
    }

    [Fact]
    public void GetEdgeIndex_ReturnsDirectEdgeOrMinusOne()
    {
        var graph = SampleGraph();

        Assert.Equal(0, graph.GetEdgeIndex(0, 1));
        Assert.Equal(1, graph.GetEdgeIndex(0, 2));
        Assert.Equal(-1, graph.GetEdgeIndex(0, 3)); // нет прямого ребра
    }
}
