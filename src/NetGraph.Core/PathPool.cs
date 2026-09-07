using System.Collections.Concurrent;

namespace NetGraph.Core;

/// <summary>Пул массивов для путей и векторов утилизации: минимум давления на GC в горячем цикле.</summary>
public static class PathPool
{
    private static readonly ConcurrentBag<int[]> IntPool = new();
    private static readonly ConcurrentBag<float[]> FloatPool = new();

    public static int[] Rent(int minSize)
        => IntPool.TryTake(out var arr) && arr.Length >= minSize ? arr : new int[Math.Max(1, minSize)];

    public static void Return(int[] arr) => IntPool.Add(arr);

    public static float[] RentFloat(int minSize)
        => FloatPool.TryTake(out var arr) && arr.Length >= minSize ? arr : new float[Math.Max(1, minSize)];

    public static void Return(float[] arr) => FloatPool.Add(arr);
}
