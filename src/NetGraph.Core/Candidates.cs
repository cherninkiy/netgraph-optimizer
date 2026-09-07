namespace NetGraph.Core;

/// <summary>Кандидат-путь для одного спроса: узлы, рёбра и нагрузка на каждое ребро.</summary>
public sealed record PathCandidate(int[] Nodes, int[] Edges, float[] Loads, float Latency, float RequiredBandwidth);

/// <summary>Результат солвера: MLU, назначение путей по спросам и вектор утилизации линков.</summary>
public sealed record SolverResult(float Mlu, int[]? PathIndices, float[]? Utilization, bool Feasible);

public static class CandidateBuilder
{
    /// <summary>Для каждого спроса находит k путей-кандидатов и считает нагрузки на рёбра.</summary>
    public static PathCandidate[][] Build(
        NetworkGraph graph, TrafficDemand[] demands, int k, KShortestPathsFinder? finder = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(demands);

        finder ??= new KShortestPathsFinder();
        var result = new PathCandidate[demands.Length][];

        for (int i = 0; i < demands.Length; i++)
        {
            var d = demands[i];
            var paths = finder.FindKPaths(graph, d.From, d.To, k, d.MaxLatency);
            var list = new List<PathCandidate>(paths.Count);

            foreach (var p in paths)
            {
                if (p.Edges.Length == 0) continue; // тривиальный путь from == to

                var loads = new float[p.Edges.Length];
                for (int j = 0; j < p.Edges.Length; j++)
                    loads[j] = d.RequiredBandwidth / graph.GetLink(p.Edges[j]).Bandwidth;

                list.Add(new PathCandidate(p.Nodes, p.Edges, loads, p.Latency, d.RequiredBandwidth));
            }

            result[i] = list.ToArray();
        }

        return result;
    }
}

/// <summary>Оценка MLU (Max Link Utilization) для назначения путей.</summary>
public static class MluEvaluator
{
    /// <summary>Заполняет вектор утилизации и возвращает MLU.</summary>
    public static float Evaluate(
        NetworkGraph graph, PathCandidate[][] candidates, ReadOnlySpan<int> assignment, Span<float> utilization)
    {
        utilization.Clear();

        for (int i = 0; i < candidates.Length; i++)
        {
            var c = candidates[i][assignment[i]];
            for (int j = 0; j < c.Edges.Length; j++)
                utilization[c.Edges[j]] += c.Loads[j];
        }

        float mlu = 0f;
        for (int e = 0; e < utilization.Length; e++)
            if (utilization[e] > mlu) mlu = utilization[e];
        return mlu;
    }
}
