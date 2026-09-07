namespace NetGraph.Core;

/// <summary>Спрос на трафик: полоса и максимальная задержка.</summary>
public readonly record struct TrafficDemand(int From, int To, float RequiredBandwidth, float MaxLatency);

/// <summary>
/// Генератор матрицы спроса с реалистичным «горячим» трафиком между edge-узлами.
/// Лимит задержки — адаптивный: множитель к кратчайшей задержке между парой узлов.
/// </summary>
public sealed class TrafficGenerator
{
    private readonly float _latencySlack;

    public TrafficGenerator(float latencySlack = 2.5f)
    {
        if (latencySlack <= 1f) throw new ArgumentOutOfRangeException(nameof(latencySlack));
        _latencySlack = latencySlack;
    }

    /// <param name="graph">Граф сети.</param>
    /// <param name="flowCount">Число потоков.</param>
    /// <param name="rnd">Источник случайности.</param>
    public TrafficDemand[] Generate(NetworkGraph graph, int flowCount, Random rnd)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(rnd);
        if (flowCount <= 0) throw new ArgumentOutOfRangeException(nameof(flowCount));
        if (graph.NodeCount < 2)
            throw new ArgumentException("Для генерации трафика нужно минимум 2 узла.", nameof(graph));

        int nodeCount = graph.NodeCount;
        int hotCount = Math.Min(nodeCount, Math.Max(2, nodeCount * 3 / 10)); // «горячие» edge-узлы
        var finder = new KShortestPathsFinder();

        var demands = new TrafficDemand[flowCount];
        for (int i = 0; i < flowCount; i++)
        {
            int from, to;
            do
            {
                from = PickNode(rnd, nodeCount, hotCount);
                to = PickNode(rnd, nodeCount, hotCount);
            } while (from == to);

            float bandwidth = 1f + (float)rnd.NextDouble() * 99f; // 1..100 Mbps

            // базовая задержка кратчайшего пути * slack (с разбросом), чтобы спрос был реализуем
            var shortest = finder.FindKPaths(graph, from, to, k: 1, maxLatency: float.MaxValue);
            float baseLatency = shortest.Count > 0 ? shortest[0].Latency : 0f;
            float maxLatency = baseLatency * _latencySlack * (0.8f + (float)rnd.NextDouble() * 0.4f);

            demands[i] = new TrafficDemand(from, to, bandwidth, maxLatency);
        }

        return demands;
    }

    private static int PickNode(Random rnd, int nodeCount, int hotCount)
        => rnd.NextDouble() < 0.7 ? rnd.Next(hotCount) : rnd.Next(nodeCount);
}
