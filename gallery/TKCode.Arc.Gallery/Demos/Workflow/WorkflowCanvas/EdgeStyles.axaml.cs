using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Switches the canvas's <see cref="WorkflowCanvas.EdgeStyle"/> from the segmented control.</summary>
public partial class WorkflowCanvasEdgeStylesDemo : UserControl
{
    public WorkflowCanvasEdgeStylesDemo()
    {
        InitializeComponent();
        var graph = new WorkflowGraph();
        var form = new WorkflowNode("form", "Form submitted") { Description = "Contact sales", Icon = ArcIcons.Mail };
        var score = new WorkflowNode("score", "Score the lead") { Description = "Company size and intent", Icon = ArcIcons.Sparkles };
        var crm = new WorkflowNode("crm", "Add to CRM") { Description = "New lead", Icon = ArcIcons.Users, Appearance = WorkflowNodeAppearance.Compact };
        var call = new WorkflowNode("call", "Book a call") { Description = "Sales team", Icon = ArcIcons.Phone, Appearance = WorkflowNodeAppearance.Compact };
        var nurture = new WorkflowNode("nurture", "Start emails") { Description = "Nurture sequence", Icon = ArcIcons.Send, Appearance = WorkflowNodeAppearance.Compact };
        foreach (var node in new[] { form, score, crm, call, nurture })
        {
            graph.Nodes.Add(node);
        }
        graph.Connect(form, score);
        graph.Connect(score, crm);
        graph.Connect(score, call);
        graph.Connect(score, nurture);
        Canvas.Graph = graph;
        StylePicker.SelectionChanged += (_, _) => Canvas.EdgeStyle = StylePicker.SelectedValue switch
        {
            "Step" => WorkflowEdgeStyle.Step,
            "Straight" => WorkflowEdgeStyle.Straight,
            _ => WorkflowEdgeStyle.Bezier
        };
    }
}
