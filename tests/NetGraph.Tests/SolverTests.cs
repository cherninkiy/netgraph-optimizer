using NetGraph.Core;
using Xunit;

namespace NetGraph.Tests;

public class SolverTests
{
    private static NetworkGraph SampleGraph()
    {
        var links = new List<Link>
        {
            new(0, 1, 100f, 1f, 1f),
            new(0, 2, 100f, 2f, 2f),
            new(1, 3, 100f, 1f, 1f),
            new(2, 3, 100f, 1f, 1f),
            new(1, 0, 100f, 1f, 1f),
            new(2, 0, 100f, 2f, 2f),
            new(3, 1, 100f, 1f, 1f),
            new(3, 2, 100f, 1f, 1f),
        };
        return new NetworkGraph(links);
    }

    private static (NetworkGraph Graph, PathCandidate[][] Candidates) SmallInstance()
    {
        var graph = SampleGraph();
        var demands = new[]
        {
            new TrafficDemand(0, 3, 40f, 100f),
            new TrafficDemand(0, 3, 30f, 100f),
            new TrafficDemand(1, 2, 20f, 100f),
        };
        var candidates = CandidateBuilder.Build(graph, demands, k: 3);
        return (graph, candidates);
    }

    [Fact]
    public void Beam_FindsFeasibleSolution()
    {
        var (graph, candidates) = SmallInstance();
        var result = new SequentialBeamSolver().Solve(graph, candidates, beamWidth: 4);

        Assert.True(result.Feasible);
        Assert.True(result.Mlu <= 1f);
        Assert.Equal(candidates.Length, result.PathIndices!.Length);
    }

    [Fact]
    public void MluEvaluator_MatchesSolverResult()
    {
        var (graph, candidates) = SmallInstance();
        var result = new SequentialBeamSolver().Solve(graph, candidates, beamWidth: 4);

        var util = new float[graph.LinksCount];
        float mlu = MluEvaluator.Evaluate(graph, candidates, result.PathIndices!, util);

        Assert.Equal(result.Mlu, mlu, 3);
        Assert.Equal(result.Utilization!, util);
    }

    private static void BruteForce(PathCandidate[][] candidates, int idx, int[] assign, List<int[]> all)
    {
        if (idx == candidates.Length) { all.Add((int[])assign.Clone()); return; }
        for (int ci = 0; ci < candidates[idx].Length; ci++)
        {
            assign[idx] = ci;
            BruteForce(candidates, idx + 1, assign, all);
        }
    }

    [Fact]
    public void BranchAndBound_MatchesExhaustiveSearch()
    {
        var (graph, candidates) = SmallInstance();

        // полный перебор — эталон
        var all = new List<int[]>();
        BruteForce(candidates, 0, new int[candidates.Length], all);
        var util = new float[graph.LinksCount];
        float expected = all.Min(a => MluEvaluator.Evaluate(graph, candidates, a, util));

        var bnb = new ParallelBranchAndBoundSolver().Solve(graph, candidates, maxDegreeOfParallelism: 2);

        Assert.True(bnb.Feasible);
        Assert.Equal(expected, bnb.Mlu, 3);
    }

    [Fact]
    public void InfeasibleDemand_ReturnsNotFeasible()
    {
        var graph = SampleGraph();
        var candidates = new PathCandidate[][] { Array.Empty<PathCandidate>() };

        var beam = new SequentialBeamSolver().Solve(graph, candidates, 4);
        var bnb = new ParallelBranchAndBoundSolver().Solve(graph, candidates);

        Assert.False(beam.Feasible);
        Assert.False(bnb.Feasible);
    }

    [Fact]
    public void GeneratedInstance_IsSolvableAndFeasible()
    {
        var graph = GraphGenerator.CreateRandom(60, 150, new Random(42));
        var demands = new TrafficGenerator().Generate(graph, 15, new Random(42))
            .Select(d => d with { RequiredBandwidth = Math.Min(d.RequiredBandwidth, 10f) })
            .ToArray();
        var candidates = CandidateBuilder.Build(graph, demands, k: 5);

        var bnb = new ParallelBranchAndBoundSolver().Solve(graph, candidates);

        Assert.True(bnb.Feasible, "лёгкий трафик обязан размещаться без перегрузок");
        Assert.True(bnb.Mlu > 0f);
    }
}
