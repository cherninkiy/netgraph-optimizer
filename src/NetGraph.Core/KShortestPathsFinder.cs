namespace NetGraph.Core;

public readonly record struct PathResult(int[] Nodes, int[] Edges, float Latency);

/// <summary>
/// Поиск k кратчайших путей (по latency) — алгоритм Йена на базе Дейкстры,
/// с отсечением по maxLatency (Branch & Bound на уровне пути).
/// Буферы Дейкстры переиспользуются между вызовами (generation-штампы).
/// </summary>
public sealed class KShortestPathsFinder
{
    private float[] _dist = Array.Empty<float>();
    private long[] _stamp = Array.Empty<long>();
    private int[] _prevEdge = Array.Empty<int>();
    private long _generation;

    public List<PathResult> FindKPaths(NetworkGraph graph, int from, int to, int k, float maxLatency)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k));

        var result = new List<PathResult>(k);
        EnsureCapacity(graph.NodeCount);

        if (from == to)
        {
            result.Add(new PathResult(new[] { from }, Array.Empty<int>(), 0f));
            return result;
        }

        var accepted = new List<PathResult>();
        var candidates = new BinaryHeap<PathResult>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var first = ShortestPath(graph, from, to, Array.Empty<int>(), null);
        if (first is not { } p0 || p0.Latency > maxLatency)
            return result;

        accepted.Add(p0);
        seen.Add(Key(p0.Nodes));

        int frontier = 0;
        while (accepted.Count < k)
        {
            if (frontier < accepted.Count)
            {
                GenerateSpurs(graph, accepted, frontier, to, maxLatency, seen, candidates);
                frontier++;
            }

            if (candidates.Count == 0)
            {
                if (frontier >= accepted.Count) break;
                continue;
            }

            while (candidates.Count > 0)
            {
                var best = candidates.Pop().Value;
                if (seen.Add(Key(best.Nodes)))
                {
                    accepted.Add(best);
                    break;
                }
            }
        }

        result.AddRange(accepted);
        return result;
    }

    private void GenerateSpurs(
        NetworkGraph graph, List<PathResult> accepted, int basePathIndex, int to, float maxLatency,
        HashSet<string> seen, BinaryHeap<PathResult> candidates)
    {
        var basePath = accepted[basePathIndex];

        for (int spurIndex = 0; spurIndex < basePath.Nodes.Length - 1; spurIndex++)
        {
            int spurNode = basePath.Nodes[spurIndex];
            int[]? bannedNodes = spurIndex == 0 ? null : basePath.Nodes[..spurIndex];

            // запрещаем spur-рёбра всех принятых путей с тем же корнем (классический Йен)
            var bannedEdges = new List<int>();
            foreach (var p in accepted)
            {
                if (p.Edges.Length <= spurIndex) continue;
                bool sameRoot = true;
                for (int i = 0; i < spurIndex; i++)
                    if (p.Nodes[i] != basePath.Nodes[i]) { sameRoot = false; break; }
                if (sameRoot)
                    bannedEdges.Add(p.Edges[spurIndex]);
            }

            var spur = ShortestPath(graph, spurNode, to, bannedEdges.ToArray(), bannedNodes);
            if (spur is not { } sp)
                continue;

            float rootLatency = 0f;
            for (int i = 0; i < spurIndex; i++)
                rootLatency += graph.GetLink(basePath.Edges[i]).Latency;

            float totalLatency = rootLatency + sp.Latency;
            if (totalLatency > maxLatency)
                continue; // отсечение по задержке

            var totalNodes = new int[spurIndex + sp.Nodes.Length];
            Array.Copy(basePath.Nodes, 0, totalNodes, 0, spurIndex + 1);
            Array.Copy(sp.Nodes, 1, totalNodes, spurIndex + 1, sp.Nodes.Length - 1);

            var totalEdges = new int[spurIndex + sp.Edges.Length];
            Array.Copy(basePath.Edges, 0, totalEdges, 0, spurIndex);
            Array.Copy(sp.Edges, 0, totalEdges, spurIndex, sp.Edges.Length);

            if (totalEdges.Length == 0)
                continue;

            var candidate = new PathResult(totalNodes, totalEdges, totalLatency);
            if (!seen.Contains(Key(totalNodes)))
                candidates.Push(totalLatency, candidate);
        }
    }

    /// <summary>Дейкстра по Cost с переиспользуемыми буферами и «запрещёнными» элементами.</summary>
    private PathResult? ShortestPath(NetworkGraph graph, int src, int dst, int[] bannedEdges, int[]? bannedNodes)
    {
        _generation++;
        long gen = _generation;
        _dist[src] = 0f;
        _stamp[src] = gen;
        _prevEdge[src] = -1;

        var heap = new BinaryHeap<int>(graph.LinksCount);
        heap.Push(0f, src);

        while (heap.Count > 0)
        {
            var (d, u) = heap.Pop();
            if (_stamp[u] != gen || d > _dist[u]) continue; // устаревшая запись
            if (u == dst) break;

            foreach (int edgeIdx in graph.GetNeighborEdges(u))
            {
                if (Array.IndexOf(bannedEdges, edgeIdx) >= 0) continue;
                var link = graph.GetLink(edgeIdx);
                int v = link.To;

                if (bannedNodes is not null)
                {
                    bool banned = false;
                    for (int i = 0; i < bannedNodes.Length; i++)
                        if (bannedNodes[i] == v) { banned = true; break; }
                    if (banned) continue;
                }

                float nd = d + link.Cost;
                if (_stamp[v] != gen || nd < _dist[v])
                {
                    _dist[v] = nd;
                    _stamp[v] = gen;
                    _prevEdge[v] = edgeIdx;
                    heap.Push(nd, v);
                }
            }
        }

        if (_stamp[dst] != gen)
            return null;

        // восстановление пути
        var revNodes = new List<int>();
        var revEdges = new List<int>();
        int node = dst;
        revNodes.Add(node);
        while (node != src)
        {
            int e = _prevEdge[node];
            revEdges.Add(e);
            node = graph.GetLink(e).From;
            revNodes.Add(node);
        }

        revNodes.Reverse();
        revEdges.Reverse();
        return new PathResult(revNodes.ToArray(), revEdges.ToArray(), _dist[dst]);
    }

    private void EnsureCapacity(int nodeCount)
    {
        if (_stamp.Length < nodeCount)
        {
            _dist = new float[nodeCount];
            _stamp = new long[nodeCount];
            _prevEdge = new int[nodeCount];
            _generation = 0;
        }
    }

    private static string Key(int[] nodes) => string.Join(",", nodes);
}
