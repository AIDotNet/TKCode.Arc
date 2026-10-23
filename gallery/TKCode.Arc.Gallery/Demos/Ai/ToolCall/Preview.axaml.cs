using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default simulated turn: calls appear and run, one fails until Retry, one waits for Allow or Deny.</summary>
public partial class ToolCallPreviewDemo : UserControl
{
    private readonly ToolCallSimulation _simulation = new();
    private readonly DispatcherTimer _stream = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private string[] _chunks = [];
    private int _shown;

    public ToolCallPreviewDemo()
    {
        InitializeComponent();
        Group.ItemsSource = _simulation.Items;
        _simulation.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToolCallSimulation.IsWorkingProperty)
            {
                Group.IsActive = _simulation.IsWorking;
            }
        };
        _simulation.Changed += OnChanged;
        _stream.Tick += OnStream;
        AttachedToVisualTree += (_, _) => Start();
        DetachedFromVisualTree += (_, _) =>
        {
            _stream.Stop();
            _simulation.Stop();
        };
    }

    private void Start()
    {
        _stream.Stop();
        Answer.IsVisible = false;
        Answer.Text = null;
        Again.IsVisible = false;
        Group.ClearValue(ToolCallGroup.IsExpandedProperty);
        Intro.Text = "Let me look at what is holding up the close.";
        Intro.Opacity = 1;
        _simulation.Start();
    }

    private void OnChanged(object? sender, ToolCallSimulationEventArgs e)
    {
        if (e.Phase != ToolCallSimulationPhase.Finished)
        {
            return;
        }
        // The group folds 450ms after the last call; the answer starts streaming in once it has.
        _chunks = _simulation.WasDenied
            ? ["Two things hold up the March close: ", "the export bug #482 and ", "job 0112, which ", "has not finished on the replica. ", "I did not post to #finance-ops."]
            : ["Two things hold up the March close: ", "the export bug #482 and ", "job 0112, which ", "has not finished on the replica. ", "I posted a note in #finance-ops."];
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

    private void OnRunAgain(object? sender, RoutedEventArgs e) => Start();
}
