namespace NetGraph.Core;

/// <summary>Линк сети. Плоская структура для cache locality (массив структур, не класс).</summary>
public readonly struct Link
{
    public readonly int From;
    public readonly int To;
    public readonly float Bandwidth; // Mbps
    public readonly float Latency;   // ms
    public readonly float Cost;      // вес для поиска пути (по умолчанию = latency)

    public Link(int from, int to, float bandwidth, float latency, float cost)
    {
        From = from;
        To = to;
        Bandwidth = bandwidth;
        Latency = latency;
        Cost = cost;
    }

    public override string ToString() => $"{From}->{To} bw={Bandwidth} lat={Latency}";
}
