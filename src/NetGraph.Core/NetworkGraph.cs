namespace NetGraph.Core;

/// <summary>
/// раф сети в CSR-формате: смежность хранится в плоских массивах,
/// обход соседей — через Span без аллокаций.
/// </summary>
public sealed class NetworkGraph
{
    private readonly Link[] _links;
    private int[] _offsets = null!;     // размер NodeCount + 1
    private int[] _edgeIndices = null!; // индексы рёбер, отсортированные по From (stable)
    private Dictionary<int, int> _directEdge = null!; // from*NodeCount+to -> ребро с минимальным Cost

    public int NodeCount { get; }
    public int LinksCount => _links.Length;

    public NetworkGraph(IReadOnlyList<Link> links)
    {
        ArgumentNullException.ThrowIfNull(links);
        if (links.Count == 0)
            throw new ArgumentException("раф должен содержать хотя бы один линк.", nameof(links));

        _links = links.ToArray();

        int maxNode = 0;
        for (int i = 0; i < _links.Length; i++)
        {
            if (_links[i].From > maxNode) maxNode = _links[i].From;
            if (_links[i].To > maxNode) maxNode = _links[i].To;
        }
        NodeCount = maxNode + 1;

        BuildCSR();
        BuildDirectEdgeMap();
    }

    private void BuildCSR()
    {
        // counting sort по From: stable, O(N + E)
        var counts = new int[NodeCount + 1];
        for (int i = 0; i < _links.Length; i++)
            counts[_links[i].From + 1]++;
        for (int i = 1; i <= NodeCount; i++)
            counts[i] += counts[i - 1];

        _offsets = counts;
        _edgeIndices = new int[_links.Length];
        var cursor = new int[NodeCount];
        for (int i = 0; i < _links.Length; i++)
            _edgeIndices[_offsets[_links[i].From] + cursor[_links[i].From]++] = i;
    }

    private void BuildDirectEdgeMap()
    {
        _directEdge = new Dictionary<int, int>(_links.Length);
        for (int i = 0; i < _links.Length; i++)
        {
            int key = _links[i].From * NodeCount + _links[i].To;
            if (!_directEdge.TryGetValue(key, out int existing) || _links[i].Cost < _links[existing].Cost)
                _directEdge[key] = i;
        }
    }

    public Link GetLink(int index) => _links[index];

    /// <summary>ндексы исходящих рёбер узла — Span поверх CSR, ноль аллокаций.</summary>
    public ReadOnlySpan<int> GetNeighborEdges(int node)
    {
        int start = _offsets[node];
        int end = _offsets[node + 1];
        return new ReadOnlySpan<int>(_edgeIndices, start, end - start);
    }

    /// <summary>ндекс прямого ребра from->to с минимальной ценой, либо -1.</summary>
    public int GetEdgeIndex(int from, int to)
        => _directEdge.TryGetValue(from * NodeCount + to, out int e) ? e : -1;
}
