using NetGraph.Core;
using Xunit;

namespace NetGraph.Tests;

public class TrafficGeneratorTests
{
    [Fact]
    public void GeneratesValidDistinctPairs()
    {
        var graph = GraphGenerator.CreateRandom(50, 100, new Random(3));
        var demands = new TrafficGenerator().Generate(graph, 100, new Random(3));

        Assert.Equal(100, demands.Length);
        Assert.All(demands, d =>
        {
            Assert.NotEqual(d.From, d.To);
            Assert.InRange(d.From, 0, graph.NodeCount - 1);
            Assert.InRange(d.To, 0, graph.NodeCount - 1);
            Assert.InRange(d.RequiredBandwidth, 1f, 100f);
            Assert.True(d.MaxLatency > 0f);
        });
    }

    [Fact]
    public void GeneratedGraph_IsConnected()
    {
        var graph = GraphGenerator.CreateRandom(100, 200, new Random(5));

        // BFS из узла 0 должен достигать всех узлов
        var visited = new bool[graph.NodeCount];
        var queue = new Queue<int>();
        queue.Enqueue(0);
        visited[0] = true;
        int count = 1;
        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            foreach (int e in graph.GetNeighborEdges(u))
            {
                int v = graph.GetLink(e).To;
                if (!visited[v]) { visited[v] = true; count++; queue.Enqueue(v); }
            }
        }

        Assert.Equal(graph.NodeCount, count);
    }
}
