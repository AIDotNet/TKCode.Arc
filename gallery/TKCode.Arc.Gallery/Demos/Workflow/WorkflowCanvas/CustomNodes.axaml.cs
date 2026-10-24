using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// A support workflow whose webhook and email steps have their own card content (<see cref="WorkflowCanvas.NodeTemplates"/>,
/// chosen by <see cref="WorkflowNode.Kind"/>); the email address is bound to the step's description.
/// </summary>
public partial class WorkflowCanvasCustomNodesDemo : UserControl
{
    public WorkflowCanvasCustomNodesDemo()
    {
        InitializeComponent();
        var graph = new WorkflowGraph();
        WorkflowNode Add(WorkflowNode node)
        {
            graph.Nodes.Add(node);
            return node;
        }
        var ticket = Add(new WorkflowNode("ticket", "New support ticket")
        {
            Kind = "webhook", Description = "POST /hooks/support", HasDefaultInput = false
        });
        var classify = Add(new WorkflowNode("classify", "Classify the ticket") { Description = "Topic and urgency", Icon = ArcIcons.Tag });
        var urgent = Add(new WorkflowNode("urgent", "Urgent?") { Description = "Condition", Icon = ArcIcons.GitBranch });
        var yes = new WorkflowPort(WorkflowPortDirection.Output, "Yes");
        var no = new WorkflowPort(WorkflowPortDirection.Output, "No");
        urgent.Outputs.Add(yes);
        urgent.Outputs.Add(no);
        var page = Add(new WorkflowNode("page", "Page the on-call engineer") { Description = "PagerDuty", Icon = ArcIcons.Bell });
        var email = Add(new WorkflowNode("email", "Email the customer") { Kind = "email", Description = "ada@example.com" });
        graph.Connect(ticket, classify);
        graph.Connect(classify, urgent);
        graph.Connect(urgent, yes, page, null);
        graph.Connect(urgent, no, email, null);
        Canvas.Graph = graph;
    }
}
