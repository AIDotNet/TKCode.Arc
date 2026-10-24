using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Scatter places every step by hand (so the canvas stops arranging them); Arrange hands them back to the canvas, which
/// arranges the workflow and glides the cards into place.
/// </summary>
public partial class WorkflowCanvasArrangeDemo : UserControl
{
    private readonly WorkflowGraph _graph = new();
    private readonly Random _random = new(7);

    public WorkflowCanvasArrangeDemo()
    {
        InitializeComponent();
        var issue = Add(new WorkflowNode("issue", "Issue opened") { Description = "Bug report", Icon = ArcIcons.Bug });
        var triage = Add(new WorkflowNode("triage", "Triage") { Description = "Label and priority", Icon = ArcIcons.Tag });
        var repro = Add(new WorkflowNode("repro", "Reproduce") { Description = "Test environment", Icon = ArcIcons.Terminal, Appearance = WorkflowNodeAppearance.Compact });
        var assign = Add(new WorkflowNode("assign", "Assign owner") { Description = "Team rotation", Icon = ArcIcons.User, Appearance = WorkflowNodeAppearance.Compact });
        var fix = Add(new WorkflowNode("fix", "Fix and review") { Description = "Pull request", Icon = ArcIcons.GitBranch });
        var release = Add(new WorkflowNode("release", "Ship in next release") { Description = "Release notes", Icon = ArcIcons.Package });
        _graph.Connect(issue, triage);
        _graph.Connect(triage, repro);
        _graph.Connect(triage, assign);
        _graph.Connect(repro, fix);
        _graph.Connect(assign, fix);
        _graph.Connect(fix, release);
        Canvas.Graph = _graph;
        ScatterButton.Click += (_, _) => Scatter();
        ArrangeButton.Click += (_, _) => Canvas.ArrangeNodes();
    }

    private WorkflowNode Add(WorkflowNode node)
    {
        _graph.Nodes.Add(node);
        return node;
    }

    private void Scatter()
    {
        foreach (var node in _graph.Nodes)
        {
            node.Position = new Point(_random.Next(0, 520), _random.Next(0, 520));
        }
    }
}
