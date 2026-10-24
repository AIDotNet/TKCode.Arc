using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// A large generated workflow (ranks of 8 steps, each fed by one or two steps of the rank before) with the zoom bar and the
/// minimap, shown from its start. It starts at 200 steps so the page opens quickly; 500 is the size measured on a desktop.
/// </summary>
public partial class WorkflowCanvasLargeGraphDemo : UserControl
{
    public WorkflowCanvasLargeGraphDemo()
    {
        InitializeComponent();
        Show(200);
    }

    private void OnCountChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Count?.SelectedValue is string value && int.TryParse(value, out var count))
        {
            Show(count);
        }
    }

    private void Show(int count)
    {
        var graph = Generate(count);
        Canvas.Graph = graph;
        // Start at the first step, once the canvas has arranged the workflow.
        void ShowStart(object? sender, EventArgs e)
        {
            Canvas.LayoutUpdated -= ShowStart;
            Canvas.ScrollIntoView(graph.Nodes[0], animate: false);
        }
        Canvas.LayoutUpdated += ShowStart;
        Status.Text = $"{count} steps, {graph.Edges.Count} connections";
    }

    private static WorkflowGraph Generate(int count)
    {
        // Seeded: the same workflow every time.
        var random = new Random(7);
        var graph = new WorkflowGraph();
        var kinds = new[]
        {
            ("Webhook", ArcIcons.Zap), ("Postgres", ArcIcons.Database), ("Team chat", ArcIcons.MessageSquare),
            ("Payments", ArcIcons.CreditCard), ("Email", ArcIcons.Mail)
        };
        var ranks = new List<List<WorkflowNode>>();
        for (var i = 0; i < count; i++)
        {
            if (i % 8 == 0)
            {
                ranks.Add([]);
            }
            var (description, icon) = kinds[i % kinds.Length];
            var node = new WorkflowNode($"step-{i + 1}", $"Step {i + 1}") { Description = description, Icon = icon };
            graph.Nodes.Add(node);
            ranks[^1].Add(node);
        }
        for (var r = 1; r < ranks.Count; r++)
        {
            var before = ranks[r - 1];
            foreach (var node in ranks[r])
            {
                var first = before[random.Next(before.Count)];
                graph.Connect(first, node);
                var second = before[random.Next(before.Count)];
                if (random.NextDouble() < 0.6 && second != first)
                {
                    graph.Connect(second, node);
                }
            }
        }
        return graph;
    }
}
