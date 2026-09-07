using System.Text;

namespace NetGraph.Core;

/// <summary>Экспорт графа с утилизацией линков в формат Graphviz (DOT).</summary>
public static class DotExporter
{
    public static void Export(string path, NetworkGraph graph, ReadOnlySpan<float> utilization)
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph G {");
        sb.AppendLine("  layout=sfdp; overlap=false;");

        for (int i = 0; i < graph.LinksCount; i++)
        {
            var link = graph.GetLink(i);
            float u = i < utilization.Length ? utilization[i] : 0f;
            string color = u > 0.8f ? "red" : u > 0.5f ? "orange" : "green";
            sb.AppendLine($"  {link.From} -> {link.To} [label=\"{u:P0}\", color={color}];");
        }

        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString());
    }
}
