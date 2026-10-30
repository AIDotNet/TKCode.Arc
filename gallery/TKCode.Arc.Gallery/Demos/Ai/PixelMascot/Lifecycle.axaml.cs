using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class PixelMascotLifecycleDemo : UserControl
{
    private readonly DispatcherTimer _timer = new();
    private readonly Queue<(TimeSpan After, PixelMascotStatus Status, string Caption)> _script = new();

    public PixelMascotLifecycleDemo()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Next();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void OnSend(object? sender, RoutedEventArgs e) => Play(fails: false);

    private void OnFail(object? sender, RoutedEventArgs e) => Play(fails: true);

    // A simulated request: 1.5s to the first token, 2.5s of streaming, then done (or an error) and idle 2s later.
    private void Play(bool fails)
    {
        _timer.Stop();
        _script.Clear();
        _script.Enqueue((TimeSpan.Zero, PixelMascotStatus.Thinking, "Request sent"));
        _script.Enqueue((TimeSpan.FromSeconds(1.5), PixelMascotStatus.Writing, "Streaming the answer"));
        _script.Enqueue(fails
            ? (TimeSpan.FromSeconds(1.2), PixelMascotStatus.Error, "The request failed")
            : (TimeSpan.FromSeconds(2.5), PixelMascotStatus.Done, "Finished"));
        _script.Enqueue((TimeSpan.FromSeconds(fails ? 3 : 2), PixelMascotStatus.Idle, "Ready"));
        Next();
    }

    private void Next()
    {
        _timer.Stop();
        if (!_script.TryDequeue(out var step))
        {
            return;
        }
        Mascot.Status = step.Status;
        Caption.Text = step.Caption;
        if (_script.TryPeek(out var next))
        {
            _timer.Interval = next.After;
            _timer.Start();
        }
    }
}
