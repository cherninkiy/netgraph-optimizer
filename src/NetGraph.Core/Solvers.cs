namespace NetGraph.Core;

/// <summary>
/// Последовательный Beam Search: обходим спросы по очереди,
/// оставляем beamWidth лучших частичных решений по MLU.
/// Векторы утилизации берём из пула — минимум давления на GC.
/// </summary>
public sealed class SequentialBeamSolver
{
    private readonly struct State
    {
        public readonly int[] Assignment;
        public readonly float[] Utilization;
        public readonly float Mlu;

        public State(int[] assignment, float[] utilization, float mlu)
        {
            Assignment = assignment;
            Utilization = utilization;
            Mlu = mlu;
        }
    }

    public SolverResult Solve(NetworkGraph graph, PathCandidate[][] candidates, int beamWidth)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(candidates);
        beamWidth = Math.Max(1, beamWidth);

        int n = candidates.Length;
        foreach (var c in candidates)
            if (c.Length == 0)
                return new SolverResult(float.PositiveInfinity, null, null, Feasible: false);

        var states = new List<State> { new State(new int[n], new float[graph.LinksCount], 0f) };

        for (int d = 0; d < n; d++)
        {
            var next = new List<State>(states.Count * Math.Min(4, candidates[d].Length));
            var cands = candidates[d];

            foreach (var st in states)
            {
                for (int ci = 0; ci < cands.Length; ci++)
                {
                    var c = cands[ci];

                    var util = PathPool.RentFloat(graph.LinksCount);
                    Array.Copy(st.Utilization, util, graph.LinksCount);
                    float mlu = st.Mlu;
                    for (int j = 0; j < c.Edges.Length; j++)
                    {
                        int e = c.Edges[j];
                        float u = util[e] + c.Loads[j];
                        util[e] = u;
                        if (u > mlu) mlu = u;
                    }

                    var assign = PathPool.Rent(n);
                    Array.Copy(st.Assignment, assign, n);
                    assign[d] = ci;

                    next.Add(new State(assign, util, mlu));
                }
            }

            next.Sort((a, b) => a.Mlu.CompareTo(b.Mlu));

            // лишние состояния возвращаем в пул
            for (int i = beamWidth; i < next.Count; i++)
            {
                PathPool.Return(next[i].Assignment);
                PathPool.Return(next[i].Utilization);
            }
            if (next.Count > beamWidth)
                next.RemoveRange(beamWidth, next.Count - beamWidth);

            // освобождаем массивы отброшенных предыдущих состояний
            foreach (var st in states)
            {
                PathPool.Return(st.Assignment);
                PathPool.Return(st.Utilization);
            }

            states = next;
        }

        var best = states[0];
        bool feasible = best.Mlu <= 1f;
        var bestUtil = best.Utilization.AsSpan(0, graph.LinksCount).ToArray();
        var bestAssign = best.Assignment.AsSpan(0, n).ToArray(); // пул может вернуть массив длиннее n

        foreach (var st in states)
        {
            PathPool.Return(st.Assignment);
            PathPool.Return(st.Utilization);
        }

        return new SolverResult(best.Mlu, bestAssign, bestUtil, feasible);
    }
}

/// <summary>
/// Branch &amp; Bound с многопоточным обходом дерева:
/// первый уровень спроса распараллелен через Parallel.For,
/// лучший MLU разделяется (Volatile-чтение + lock-обновление),
/// ветки отсекаются, если текущий MLU уже не лучше найденного.
/// </summary>
public sealed class ParallelBranchAndBoundSolver
{
    private NetworkGraph _graph = null!;
    private PathCandidate[][] _candidates = null!;
    private int[] _bestAssignment = null!;
    private float _best;
    private readonly object _gate = new();
    private long _explored;
    private long _budget;
    private volatile bool _stop;
    private bool _improved;

    public long ExploredNodes => Interlocked.Read(ref _explored);

    public SolverResult Solve(
        NetworkGraph graph, PathCandidate[][] candidates,
        int? maxDegreeOfParallelism = null,
        float? initialUpperBound = null,
        long nodeBudget = 10_000_000)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(candidates);
        _budget = nodeBudget;
        _stop = false;
        _improved = false;

        _graph = graph;
        int n = candidates.Length;
        foreach (var c in candidates)
            if (c.Length == 0)
                return new SolverResult(float.PositiveInfinity, null, null, Feasible: false);

        // сначала «тяжёлые» спросы — так отсечения срабатывают раньше
        var order = Enumerable.Range(0, n)
            .OrderByDescending(i => candidates[i][0].RequiredBandwidth)
            .ToArray();
        _candidates = order.Select(i => candidates[i]).ToArray();

        _best = initialUpperBound ?? float.PositiveInfinity;
        _bestAssignment = new int[n];
        Interlocked.Exchange(ref _explored, 0);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount
        };

        int firstCount = _candidates[0].Length;
        Parallel.For(0, firstCount, options, ci0 =>
        {
            var util = new float[graph.LinksCount];
            var assign = new int[n];
            assign[0] = ci0;

            var c0 = _candidates[0][ci0];
            float mlu = 0f;
            for (int j = 0; j < c0.Edges.Length; j++)
            {
                int e = c0.Edges[j];
                float u = util[e] + c0.Loads[j];
                util[e] = u;
                if (u > mlu) mlu = u;
            }

            if (mlu < Volatile.Read(ref _best))
                Dfs(1, util, mlu, assign);
        });

        if (!_improved)
            return new SolverResult(_best, null, null, _best <= 1f); // улучшений не найдено
        if (float.IsPositiveInfinity(_best))
            return new SolverResult(float.PositiveInfinity, null, null, Feasible: false);

        var utilization = new float[graph.LinksCount];
        float finalMlu = MluEvaluator.Evaluate(graph, _candidates, _bestAssignment, utilization);
        return new SolverResult(finalMlu, (int[])_bestAssignment.Clone(), utilization, finalMlu <= 1f);
    }

    private void Dfs(int idx, float[] util, float mlu, int[] assign)
    {
        if (_stop) return;
        long explored = Interlocked.Increment(ref _explored);
        if (explored > _budget)
            _stop = true;

        if (idx == _candidates.Length)
        {
            TryUpdate(mlu, assign);
            return;
        }

        var cands = _candidates[idx];
        for (int ci = 0; ci < cands.Length; ci++)
        {
            var c = cands[ci];

            // проверка ёмкости линков
            bool ok = true;
            for (int j = 0; j < c.Edges.Length; j++)
            {
                if (util[c.Edges[j]] + c.Loads[j] > 1f) { ok = false; break; }
            }
            if (!ok) continue;

            // применяем путь (temporary apply)
            float newMlu = mlu;
            for (int j = 0; j < c.Edges.Length; j++)
            {
                int e = c.Edges[j];
                float u = util[e] + c.Loads[j];
                util[e] = u;
                if (u > newMlu) newMlu = u;
            }

            // Branch & Bound: нижняя граница = текущий MLU (утилизации только растут)
            if (newMlu >= Volatile.Read(ref _best))
            {
                Rollback(c, util);
                continue;
            }

            assign[idx] = ci;
            Dfs(idx + 1, util, newMlu, assign);
            Rollback(c, util);
        }
    }

    private static void Rollback(PathCandidate c, float[] util)
    {
        for (int j = 0; j < c.Edges.Length; j++)
            util[c.Edges[j]] -= c.Loads[j];
    }

    private void TryUpdate(float mlu, int[] assign)
    {
        lock (_gate)
        {
            if (mlu < _best)
            {
                _best = mlu;
                Array.Copy(assign, _bestAssignment, assign.Length);
                _improved = true;
            }
        }
    }
}
