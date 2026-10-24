using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A run that already happened: every step gets its final state, and the canvas shows it without playing it.</summary>
public partial class WorkflowCanvasFinishedRunDemo : UserControl
{
    public WorkflowCanvasFinishedRunDemo()
    {
        InitializeComponent();
        var graph = new WorkflowGraph();
        var push = Add(graph, new WorkflowNode("push", "Push to main")
        {
            Description = "web-app", Icon = ArcIcons.GitBranch, State = WorkflowNodeState.Done
        });
        var build = Add(graph, new WorkflowNode("build", "Build and test")
        {
            Description = "1,204 tests", Result = "1,204 passed", Duration = TimeSpan.FromSeconds(84),
            Icon = ArcIcons.Package, State = WorkflowNodeState.Done
        });
        var check = Add(graph, new WorkflowNode("check", "Changes the database?")
        {
            Description = "Condition", Result = "No, deploy as is", Duration = TimeSpan.FromMilliseconds(3),
            Icon = ArcIcons.Database, State = WorkflowNodeState.Done
        });
        var migrate = Add(graph, new WorkflowNode("migrate", "Run migrations")
        {
            Description = "Postgres", Icon = ArcIcons.Database, Appearance = WorkflowNodeAppearance.Compact,
            State = WorkflowNodeState.Skipped
        });
        var deploy = Add(graph, new WorkflowNode("deploy", "Deploy to staging")
        {
            Description = "3 regions", Error = "Health check failed in eu-west",
            Icon = ArcIcons.Cloud, Appearance = WorkflowNodeAppearance.Compact, State = WorkflowNodeState.Failed
        });
        var notify = Add(graph, new WorkflowNode("notify", "Notify the team")
        {
            Description = "Team chat", Result = "Sent in 140 ms", Duration = TimeSpan.FromMilliseconds(140),
            Icon = ArcIcons.MessageSquare, Appearance = WorkflowNodeAppearance.Compact, State = WorkflowNodeState.Done
        });
        graph.Connect(push, build);
        graph.Connect(build, check);
        graph.Connect(check, migrate);
        graph.Connect(check, deploy);
        graph.Connect(check, notify);
        Canvas.Graph = graph;
    }

    private static WorkflowNode Add(WorkflowGraph graph, WorkflowNode node)
    {
        graph.Nodes.Add(node);
        return node;
    }
}
