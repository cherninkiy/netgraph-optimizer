using System.Diagnostics;
using NetGraph.Core;

// args: [nodes] [extraEdges] [flows] [k] [beamWidth]
int nodes = args.Length > 0 ? int.Parse(args[0]) : 50;
int extraEdges = args.Length > 1 ? int.Parse(args[1]) : 120;
int flows = args.Length > 2 ? int.Parse(args[2]) : 60;
int k = args.Length > 3 ? int.Parse(args[3]) : 5;
int beamWidth = args.Length > 4 ? int.Parse(args[4]) : 8;


var rnd = new Random(42);
var graph = GraphGenerator.CreateRandom(nodes, extraEdges, rnd);
var demands = new TrafficGenerator().Generate(graph, flows, rnd);
Console.WriteLine($"Сеть: {nodes} узлов, {graph.LinksCount} линков | потоков: {flows} | k={k} | beam={beamWidth}");

var sw = Stopwatch.StartNew();
var candidates = CandidateBuilder.Build(graph, demands, k);
Console.WriteLine($"Кандидаты: {candidates.Sum(c => c.Length)} путей для {flows} спросов за {sw.ElapsedMilliseconds} ms");

var seq = Timed(() => new SequentialBeamSolver().Solve(graph, candidates, beamWidth));
Print("Sequential Beam", seq);

var bnbSolver = new ParallelBranchAndBoundSolver();
var bnb = Timed(() => bnbSolver.Solve(graph, candidates, initialUpperBound: seq.Result.Mlu));
Print("Parallel B&B", bnb);
Console.WriteLine($"  узлов дерева B&B: {bnbSolver.ExploredNodes}");

DotExporter.Export("graph.dot", graph, bnb.Result.Utilization ?? Array.Empty<float>());
Console.WriteLine();
Console.WriteLine("Визуализация: dot -Tpng graph.dot -o network.png");

static (SolverResult Result, long Ms) Timed(Func<SolverResult> f)
{
    var sw = Stopwatch.StartNew();
    var r = f();
    return (r, sw.ElapsedMilliseconds);
}

static void Print(string name, (SolverResult Result, long Ms) t)
    => Console.WriteLine($"{name,-16} MLU={t.Result.Mlu:F3} feasible={t.Result.Feasible} time={t.Ms} ms");
