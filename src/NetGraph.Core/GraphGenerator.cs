namespace NetGraph.Core;

/// <summary>Генератор случайной связной сети: кольцо + дополнительные рёбра (все двунаправленные).</summary>
public static class GraphGenerator
{
    public static NetworkGraph CreateRandom(int nodeCount, int extraEdgeCount, Random rnd)
    {
        if (nodeCount < 2) throw new ArgumentOutOfRangeException(nameof(nodeCount));
        ArgumentNullException.ThrowIfNull(rnd);

        var links = new List<Link>(2 * (nodeCount + extraEdgeCount));
        var used = new HashSet<(int, int)>();

        void Add(int a, int b)
        {
            float latency = 1f + (float)rnd.NextDouble() * 19f;      // 1..20 ms
            float bandwidth = 100f + (float)rnd.NextDouble() * 900f; // 100..1000 Mbps
            links.Add(new Link(a, b, bandwidth, latency, latency));
            links.Add(new Link(b, a, bandwidth, latency, latency));
            used.Add((a, b));
            used.Add((b, a));
        }

        // кольцо гарантирует связность
        for (int i = 0; i < nodeCount - 1; i++)
            Add(i, i + 1);
        if (nodeCount > 2)
            Add(nodeCount - 1, 0);

        // дополнительные случайные линки
        int placed = 0, attempts = 0;
        while (placed < extraEdgeCount && attempts < extraEdgeCount * 50)
        {
            attempts++;
            int a = rnd.Next(nodeCount);
            int b = rnd.Next(nodeCount);
            if (a == b || used.Contains((a, b))) continue;
            Add(a, b);
            placed++;
        }

        return new NetworkGraph(links);
    }
}
