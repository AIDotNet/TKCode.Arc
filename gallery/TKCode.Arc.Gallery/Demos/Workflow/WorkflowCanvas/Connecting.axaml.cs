using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Connecting steps: a condition with "Yes" and "No" outputs, an app rule in
/// <see cref="WorkflowCanvas.ConnectionRequested"/>, a new step where a connection is dropped on the empty canvas
/// (<see cref="WorkflowCanvas.ConnectionDroppedOnCanvas"/>), and undo and redo buttons bound to the canvas's commands.
/// </summary>
public partial class WorkflowCanvasConnectingDemo : UserControl
{
    private int _added;

    public WorkflowCanvasConnectingDemo()
    {
        InitializeComponent();
        var graph = new WorkflowGraph();
        WorkflowNode Add(WorkflowNode node)
        {
            graph.Nodes.Add(node);
            return node;
        }
        // A trigger starts the workflow, so nothing connects into it.
        var request = Add(new WorkflowNode("request", "Refund requested")
        {
            Description = "Order #1042, $640", Icon = ArcIcons.Inbox, HasDefaultInput = false
        });
        var check = Add(new WorkflowNode("check", "Amount over $500?") { Description = "Condition", Icon = ArcIcons.GitBranch });
        var yes = new WorkflowPort(WorkflowPortDirection.Output, "Yes");
        var no = new WorkflowPort(WorkflowPortDirection.Output, "No");
        check.Outputs.Add(yes);
        check.Outputs.Add(no);
        var approve = Add(new WorkflowNode("approve", "Manager approval") { Description = "Finance team", Icon = ArcIcons.ShieldCheck });
        var refund = Add(new WorkflowNode("refund", "Issue refund") { Description = "Payments", Icon = ArcIcons.CreditCard });
        Add(new WorkflowNode("email", "Email the customer") { Description = "Template: refund sent", Icon = ArcIcons.Mail });
        graph.Connect(request, check);
        graph.Connect(check, yes, approve, null);
        graph.Connect(approve, refund);
        graph.Connect(check, no, refund, null);
        Canvas.Graph = graph;

        // The workflow's own checks run first (no loops, outputs to inputs, no duplicates); this is the app's rule.
        Canvas.ConnectionRequested += (_, e) =>
        {
            if (e.SourcePort == yes && e.TargetNode == refund)
            {
                e.Cancel = true;
                e.Reason = "Amounts over $500 need a manager's approval first";
                Status.Text = e.Reason;
            }
        };
        // Let go on the empty canvas: add a step there, its input under the pointer, and connect it; one undo takes both back.
        Canvas.ConnectionDroppedOnCanvas += (_, e) =>
        {
            var step = new WorkflowNode($"notify-{++_added}", "Notify the team")
            {
                Description = "Team chat", Icon = ArcIcons.MessageSquare, Position = new Point(e.Position.X - 150, e.Position.Y)
            };
            using (graph.History.BeginGroup())
            {
                graph.Nodes.Add(step);
                graph.Connect(e.SourceNode, e.SourcePort, step, null);
            }
            Status.Text = $"Added \"{step.Title}\" after {e.SourceNode.Title}";
        };
        Canvas.GraphEdited += (_, e) => Status.Text = e.Kind switch
        {
            WorkflowEditKind.Connect => $"Connected {e.Nodes[0].Title} to {e.Nodes[1].Title}",
            WorkflowEditKind.Disconnect => "Removed a connection",
            WorkflowEditKind.Undo => "Undone",
            WorkflowEditKind.Redo => "Redone",
            WorkflowEditKind.Delete => "Deleted",
            _ => "Moved"
        };
        Status.Text = "Point at a step to see its ports";
    }
}
