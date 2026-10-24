using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// A refund workflow to edit. The status line follows <see cref="WorkflowCanvas.SelectionChanged"/> and
/// <see cref="WorkflowCanvas.GraphEdited"/>, which is where an app would save.
/// </summary>
public partial class WorkflowCanvasEditingDemo : UserControl
{
    public WorkflowCanvasEditingDemo()
    {
        InitializeComponent();
        var graph = new WorkflowGraph();
        WorkflowNode Add(WorkflowNode node)
        {
            graph.Nodes.Add(node);
            return node;
        }
        var request = Add(new WorkflowNode("request", "Refund requested") { Description = "Support form", Icon = ArcIcons.Inbox });
        var order = Add(new WorkflowNode("order", "Find the order") { Description = "Payments", Icon = ArcIcons.Search });
        var check = Add(new WorkflowNode("check", "Amount over $500?") { Description = "Condition", Icon = ArcIcons.GitBranch });
        var approve = Add(new WorkflowNode("approve", "Manager approval")
        {
            Description = "Locked by policy", Icon = ArcIcons.Lock, IsLocked = true, Appearance = WorkflowNodeAppearance.Compact
        });
        var refund = Add(new WorkflowNode("refund", "Issue refund") { Description = "Payments", Icon = ArcIcons.CreditCard, Appearance = WorkflowNodeAppearance.Compact });
        var notify = Add(new WorkflowNode("notify", "Email customer") { Description = "Template", Icon = ArcIcons.Mail, Appearance = WorkflowNodeAppearance.Compact });
        graph.Connect(request, order);
        graph.Connect(order, check);
        graph.Connect(check, approve);
        graph.Connect(check, refund);
        graph.Connect(approve, refund);
        graph.Connect(refund, notify);
        Canvas.Graph = graph;

        Status.Text = "Nothing selected";
        Canvas.SelectionChanged += (_, _) => Status.Text = Canvas.SelectedNodes.Count switch
        {
            0 => "Nothing selected",
            1 => $"Selected: {Canvas.SelectedNodes[0].Title}",
            var n => $"{n} steps selected"
        };
        Canvas.GraphEdited += (_, e) => Status.Text = e.Kind switch
        {
            WorkflowEditKind.Move => $"Moved {Count(e.Nodes.Count, "step")}",
            WorkflowEditKind.Delete when e.Nodes.Count > 0 => $"Deleted {Count(e.Nodes.Count, "step")}",
            WorkflowEditKind.Delete => $"Deleted {Count(e.Edges.Count, "connection")}",
            WorkflowEditKind.Connect => $"Connected {e.Nodes[0].Title} to {e.Nodes[1].Title}",
            WorkflowEditKind.Disconnect => "Removed a connection",
            WorkflowEditKind.Undo => "Undone",
            _ => "Redone"
        };
        ArrangeButton.Click += (_, _) => Canvas.ArrangeNodes();
    }

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";
}
