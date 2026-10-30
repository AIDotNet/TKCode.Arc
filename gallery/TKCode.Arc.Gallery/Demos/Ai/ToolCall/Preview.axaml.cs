using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The simulated turn: calls appear and run, one fails until Retry, one waits for Allow or Deny.</summary>
public partial class ToolCallPreviewDemo : UserControl
{
    private readonly DispatcherTimer _stream = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private string[] _chunks = [];
    private int _shown;

    public ToolCallPreviewDemo()
    {
        InitializeComponent();
        // The group plays the simulation; the demo writes the transcript around it.
        Group.Simulation!.Changed += OnChanged;
        _stream.Tick += OnStream;
        DetachedFromVisualTree += (_, _) => _stream.Stop();
    }

    private void OnChanged(object? sender, ToolCallSimulationEventArgs e)
    {
        if (e.Phase == ToolCallSimulationPhase.Started)
        {
            _stream.Stop();
            Answer.IsVisible = false;
            Answer.Text = null;
            Again.IsVisible = false;
            Group.ClearValue(ToolCallGroup.IsExpandedProperty);
            Intro.Text = ToolCallDemoData.Intro;
            Intro.Opacity = 1;
            return;
        }
        if (e.Phase != ToolCallSimulationPhase.Finished)
        {
            return;
        }
        // The group folds 450ms after the last call; the answer starts streaming in once it has.
        _chunks = ToolCallDemoData.Answer(Group.Simulation!.WasDenied);
        _shown = 0;
        DispatcherTimer.RunOnce(() =>
        {
            Answer.Text = string.Empty;
            Answer.IsVisible = true;
            _stream.Start();
        }, TimeSpan.FromMilliseconds(950));
    }

    private void OnStream(object? sender, EventArgs e)
    {
        if (_shown < _chunks.Length)
        {
            Answer.Text += _chunks[_shown++];
            return;
        }
        _stream.Stop();
        Again.IsVisible = true;
    }

    private void OnRunAgain(object? sender, RoutedEventArgs e) => Group.Simulation?.Start();
}
