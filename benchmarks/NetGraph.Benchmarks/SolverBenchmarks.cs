using BenchmarkDotNet.Attributes;
using NetGraph.Core;

namespace NetGraph.Benchmarks;

[MemoryDiagnoser]
public class SolverBenchmarks
{
    private NetworkGraph _graph = null!;
    private PathCandidate[][] _candidates = null!;

    [GlobalSetup]
    public void Setup()
    {
        _graph = GraphGenerator.CreateRandom(50, 120, new Random(42));
        var demands = new TrafficGenerator().Generate(_graph, 60, new Random(42));
        _candidates = CandidateBuilder.Build(_graph, demands, k: 5);
    }

    [Benchmark(Baseline = true)]
    public SolverResult SequentialBeam() => new SequentialBeamSolver().Solve(_graph, _candidates, beamWidth: 8);

    [Benchmark]
    public SolverResult ParallelBranchAndBound() => new ParallelBranchAndBoundSolver().Solve(_graph, _candidates);
}
