using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A short automation with three actions, finished, on a phone-sized canvas.</summary>
public partial class WorkflowCanvasNarrowDemo : UserControl
{
    public WorkflowCanvasNarrowDemo()
    {
        InitializeComponent();
        var graph = new WorkflowGraph();
        var signup = new WorkflowNode("signup", "New signup") { Description = "Web app", Icon = ArcIcons.User, State = WorkflowNodeState.Done };
        var enrich = new WorkflowNode("enrich", "Enrich profile")
        {
            Description = "Company data", Result = "Brightline Labs, 212 people", Duration = TimeSpan.FromMilliseconds(240),
            Icon = ArcIcons.Search, State = WorkflowNodeState.Done
        };
        var welcome = new WorkflowNode("welcome", "Welcome")
        {
            Description = "Email", Result = "Sent", Duration = TimeSpan.FromMilliseconds(90),
            Icon = ArcIcons.Mail, Appearance = WorkflowNodeAppearance.Compact, State = WorkflowNodeState.Done
        };
        var crm = new WorkflowNode("crm", "CRM")
        {
            Description = "New lead", Icon = ArcIcons.Users, Appearance = WorkflowNodeAppearance.Compact, State = WorkflowNodeState.Running
        };
        var slack = new WorkflowNode("chat", "Chat")
        {
            Description = "#signups", Icon = ArcIcons.MessageSquare, Appearance = WorkflowNodeAppearance.Compact, State = WorkflowNodeState.Pending
        };
        foreach (var node in new[] { signup, enrich, welcome, crm, slack })
        {
            graph.Nodes.Add(node);
        }
        graph.Connect(signup, enrich);
        graph.Connect(enrich, welcome);
        graph.Connect(enrich, crm);
        graph.Connect(enrich, slack);
        Canvas.Graph = graph;
    }
}
