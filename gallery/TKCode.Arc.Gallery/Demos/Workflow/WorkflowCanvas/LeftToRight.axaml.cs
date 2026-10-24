using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A support ticket triage flow laid out left to right, its run stopped on the reply step.</summary>
public partial class WorkflowCanvasLeftToRightDemo : UserControl
{
    public WorkflowCanvasLeftToRightDemo()
    {
        InitializeComponent();
        var graph = new WorkflowGraph();
        var ticket = Add(graph, new WorkflowNode("ticket", "New ticket")
        {
            Description = "Support inbox", Icon = ArcIcons.Inbox, State = WorkflowNodeState.Done
        });
        var classify = Add(graph, new WorkflowNode("classify", "Classify request")
        {
            Description = "Topic and urgency", Result = "Billing, high", Duration = TimeSpan.FromMilliseconds(640),
            Icon = ArcIcons.Sparkles, State = WorkflowNodeState.Done
        });
        var reply = Add(graph, new WorkflowNode("reply", "Draft a reply")
        {
            Description = "From the help center", Icon = ArcIcons.Mail, State = WorkflowNodeState.Running
        });
        var assign = Add(graph, new WorkflowNode("assign", "Assign to billing")
        {
            Description = "Round robin", Result = "Assigned to Priya", Duration = TimeSpan.FromMilliseconds(35),
            Icon = ArcIcons.Users, State = WorkflowNodeState.Done
        });
        graph.Connect(ticket, classify);
        graph.Connect(classify, reply);
        graph.Connect(classify, assign);
        Canvas.Graph = graph;
    }

    private static WorkflowNode Add(WorkflowGraph graph, WorkflowNode node)
    {
        graph.Nodes.Add(node);
        return node;
    }
}
