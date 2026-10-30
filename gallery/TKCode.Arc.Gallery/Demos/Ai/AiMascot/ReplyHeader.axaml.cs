using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiMascotReplyHeaderDemo : UserControl
{
    private const string Answer =
        "The nightly sync times out because the export query scans the whole events table. Add an index on created_at and page the export in batches of 5,000 rows.";

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private int _ticks;
    private int _shown;

    public AiMascotReplyHeaderDemo()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => OnTick();
        AttachedToVisualTree += (_, _) => Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void OnAsk(object? sender, RoutedEventArgs e) => Start();

    private void Start()
    {
        _ticks = 0;
        _shown = 0;
        Reply.Text = string.Empty;
        SetPhase(AiMascotStatus.Thinking, "Thinking");
        _timer.Start();
    }

    // Thinking for 1.6s, then the words stream while the mascot reads along, then done.
    private void OnTick()
    {
        _ticks++;
        if (_ticks < 40)
        {
            return;
        }
        if (_shown < Answer.Length)
        {
            if (_shown == 0)
            {
                SetPhase(AiMascotStatus.Writing, "Writing");
            }
            _shown = Math.Min(Answer.Length, _shown + 3);
            Reply.Text = Answer[.._shown];
            return;
        }
        SetPhase(AiMascotStatus.Done, "Done");
        _timer.Stop();
    }

    private void SetPhase(AiMascotStatus status, string text)
    {
        Mascot.Status = status;
        Phase.Text = text;
    }
}
