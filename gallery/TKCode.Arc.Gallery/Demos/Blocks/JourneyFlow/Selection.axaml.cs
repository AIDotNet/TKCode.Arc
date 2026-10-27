using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class JourneyFlowSelectionDemo : UserControl
{
    public JourneyFlowSelectionDemo()
    {
        InitializeComponent();
        Journey.Stages.AddRange(JourneyFlowDemoData.Stages());
        Journey.Nodes.AddRange(JourneyFlowDemoData.Nodes());
        Journey.Segments.AddRange(JourneyFlowDemoData.Segments());

        SelectHooked.Click += (_, _) => Journey.SelectNode("hooked");
        ShowSocial.Click += (_, _) => Journey.SelectedSegment = Journey.Segments.First(s => s.Key == "social");
        Clear.Click += (_, _) => Journey.ClearSelection();

        Journey.SelectedNodeChanged += (_, e) =>
            Log.Text = e.NewNode is { } node ? $"Selected {node.Header}" : $"Cleared {e.OldNode?.Header}";
        Journey.SelectedSegmentChanged += (_, e) =>
            Log.Text = $"Segment {e.NewSegment?.Header}: {Journey.Visitors:N0} visits, {Journey.GoalTotal:N0} paid";
        Journey.TraceChanged += (_, e) =>
        {
            if (e.Node is { } node)
            {
                var cohort = Journey.GetCohort(node);
                Log.Text = $"Tracing {node.Header}: {cohort.Values.Sum():N0} learner-steps across {cohort.Count(c => c.Value > 0)} flows";
            }
            else if (e.Link is { } link)
            {
                Log.Text = $"Tracing {link.SourceKey} → {link.TargetKey}: {link.Value:N0} learners";
            }
        };
    }
}
