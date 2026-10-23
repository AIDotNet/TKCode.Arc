using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentTimelineOwnRunDemo : UserControl
{
    public AgentTimelineOwnRunDemo()
    {
        InitializeComponent();
    }

    private void OnSpanSelected(object? sender, AgentTimelineSpanSelectedEventArgs e)
    {
        Log.Text = e.Span is { } span
            ? $"Pinned: {span.Label} ({span.AgentId}, {span.Start:0.0}s to {span.End:0.0}s)"
            : "Unpinned.";
    }

    private void OnCompleted(object? sender, RoutedEventArgs e)
    {
        var summary = Timeline.Summary;
        Log.Text = $"Replay finished: {summary.Tokens:N0} tokens, ${summary.Cost:0.00}.";
    }
}
