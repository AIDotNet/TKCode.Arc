using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Streams invented results and a cited answer into the cards, as an app would.</summary>
public partial class SourceCardsStreamingDemo : UserControl
{
    // A word or an [n] citation per tick.
    private static readonly string[] s_answer =
    [
        "Keep", "each", "function", "instance", "to", "one", "or", "two", "connections", "and", "put", "a", "pooler",
        "in", "front", "of", "the", "database", "[1]", "[4].", "Transaction", "mode", "lets", "a", "few", "dozen",
        "server", "connections", "serve", "thousands", "of", "clients", "[3],", "but", "turn", "off", "named",
        "prepared", "statements", "in", "the", "driver", "[6].", "Size", "the", "pool", "from", "the", "server's",
        "cores,", "not", "from", "the", "traffic", "[2]", "[5]."
    ];

    private readonly AvaloniaList<SourceCardsSource> _sources = [];
    private readonly SourceCardsSource[] _results = SourceCardsDemoData.Sources()[..5];
    private DispatcherTimer? _timer;
    private int _step;

    public SourceCardsStreamingDemo()
    {
        InitializeComponent();
        Cards.Sources = _sources;
        Cards.Query = SourceCardsDemoData.Query;
        AttachedToVisualTree += (_, _) => Start();
        DetachedFromVisualTree += (_, _) => _timer?.Stop();
    }

    private void OnReplay(object? sender, RoutedEventArgs e) => Start();

    private void Start()
    {
        _timer?.Stop();
        _sources.Clear();
        Answer.Inlines?.Clear();
        Cards.IsExpanded = false;
        Cards.Duration = null;
        Cards.Status = SourceCardsStatus.Searching;
        _step = 0;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        if (_sources.Count < _results.Length)
        {
            _sources.Add(_results[_sources.Count]);
            return;
        }
        if (Cards.Status == SourceCardsStatus.Searching)
        {
            // The search is over when the model starts answering.
            Cards.Duration = TimeSpan.FromMilliseconds(2100);
            Cards.Status = SourceCardsStatus.Done;
            _timer!.Interval = TimeSpan.FromMilliseconds(60);
            return;
        }
        if (_step >= s_answer.Length)
        {
            _timer?.Stop();
            return;
        }
        var token = s_answer[_step++];
        var inlines = Answer.Inlines ??= [];
        if (token.StartsWith('[') && token.IndexOf(']') is var close and > 1 && int.TryParse(token[1..close], out var n))
        {
            inlines.Add(new InlineUIContainer(new SourceCitation { Number = n }) { BaselineAlignment = BaselineAlignment.Center });
            inlines.Add(new Run(token[(close + 1)..] + " "));
        }
        else
        {
            inlines.Add(new Run(token + " "));
        }
    }
}
