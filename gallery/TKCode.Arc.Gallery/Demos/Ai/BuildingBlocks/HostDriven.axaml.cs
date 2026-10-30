using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An app drives the stage from a pretend request (a timer stands in for the network).</summary>
public partial class BuildingBlocksHostDrivenDemo : UserControl
{
    private readonly DispatcherTimer _request = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly bool _ready;

    public BuildingBlocksHostDrivenDemo()
    {
        InitializeComponent();
        Blocks.Labels = new BuildingBlocksLabels { Ready = "Your CRM is ready" };
        _request.Tick += (_, _) => Finish();
        DetachedFromVisualTree += (_, _) => _request.Stop();
        _ready = true;
    }

    private void OnGenerate(object? sender, RoutedEventArgs e) => Start();

    private void OnReplayRequested(object? sender, RoutedEventArgs e) => Start();

    private void Start()
    {
        _request.Stop();
        Blocks.Status = BuildingBlocksStatus.Building;
        Generate.IsEnabled = false;
        _request.Start();
    }

    private void Finish()
    {
        _request.Stop();
        Blocks.Status = BuildingBlocksStatus.Done;
        Generate.IsEnabled = true;
    }

    private void OnPhaseChanged(object? sender, BuildingBlocksPhaseChangedEventArgs e)
    {
        // The first phase is set while the XAML loads, before the log exists.
        if (_ready)
        {
            Log.Text = $"Phase: {e.Phase}";
        }
    }
}
