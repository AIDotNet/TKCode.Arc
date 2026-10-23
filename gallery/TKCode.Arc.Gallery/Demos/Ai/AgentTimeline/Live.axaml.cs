using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentTimelineLiveDemo : UserControl
{
    // Replays the sample run as if its events were arriving from a backend right now.
    private readonly AgentTimelineLiveDriver _driver = new(AgentTimelineSamples.CreateIncidentRun());

    public AgentTimelineLiveDemo()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _driver.Stop();
        Timeline.IsLive = false;
    }

    private void OnStartClick(object? sender, RoutedEventArgs e) => Start();

    private void Start()
    {
        _driver.Start(Timeline);
        Log.Text = "Live: events arrive as they happen.";
    }

    private void OnCompleted(object? sender, RoutedEventArgs e)
    {
        Log.Text = "The run ended; the timeline settled on its summary.";
    }
}
