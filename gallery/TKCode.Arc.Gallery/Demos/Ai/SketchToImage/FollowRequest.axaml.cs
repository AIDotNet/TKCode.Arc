using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Maps a simulated backend's progress to Status, the way an app follows a real request.</summary>
public partial class SketchToImageFollowRequestDemo : UserControl
{
    // Queued, then early, middle and late steps, then the image is ready: seconds after the request starts (invented).
    private static readonly (double At, SketchToImageStatus Status)[] s_steps =
    [
        (0.6, SketchToImageStatus.Sketching),
        (4.0, SketchToImageStatus.Blocking),
        (6.4, SketchToImageStatus.Refining),
        (9.5, SketchToImageStatus.Done),
    ];

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private DateTime _started;
    private int _next;

    public SketchToImageFollowRequestDemo()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Card.StageChanged += (_, e) => Log.Text = $"Stage: {e.Stage}";
        Card.Completed += (_, _) => Log.Text = "Image ready.";
        Card.ReplayRequested += (_, _) => Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void OnGenerate(object? sender, RoutedEventArgs e) => Start();

    private void Start()
    {
        Card.Status = SketchToImageStatus.Idle;
        _started = DateTime.UtcNow;
        _next = 0;
        _timer.Start();
        Log.Text = "Queued…";
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _started).TotalSeconds;
        while (_next < s_steps.Length && elapsed >= s_steps[_next].At)
        {
            Card.Status = s_steps[_next].Status;
            _next++;
        }
        if (_next == s_steps.Length)
        {
            _timer.Stop();
        }
    }
}
