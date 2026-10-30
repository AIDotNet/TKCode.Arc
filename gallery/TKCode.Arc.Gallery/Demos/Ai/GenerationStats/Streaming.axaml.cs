using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// GenerationStats fed the way a streaming handler feeds it. A timer stands in for the model: it answers after 450ms, then
/// sends chunks of a few tokens at about 90 tokens per second.
/// </summary>
public partial class GenerationStatsStreamingDemo : UserControl
{
    private const long Target = 320;
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new(3);
    private DateTimeOffset _startedAt;
    private double _tokens;
    private bool _fails;

    public GenerationStatsStreamingDemo()
    {
        InitializeComponent();
        Stats.Model = GenerationStatsDemoData.Sonnet;
        Stats.Pricing = GenerationStatsDemoData.SonnetPricing;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, OnTick);
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void OnSend(object? sender, RoutedEventArgs e) => Send(fails: false);

    private void OnSendAndFail(object? sender, RoutedEventArgs e) => Send(fails: true);

    private void OnRetry(object? sender, RoutedEventArgs e) => Send(fails: false);

    private void Send(bool fails)
    {
        // When the request leaves: the start time, the prompt's usage, then Waiting.
        _fails = fails;
        _tokens = 0;
        _startedAt = DateTimeOffset.Now;
        Stats.StartedAt = _startedAt;
        Stats.ErrorText = null;
        Stats.Usage = new GenerationUsage { InputTokens = 2_460, CachedTokens = 1_792 };
        Stats.Status = GenerationStatus.Waiting;
        _timer.Start();
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        if (Stats.Status is GenerationStatus.Waiting or GenerationStatus.Streaming)
        {
            _timer.Stop();
            Stats.Usage = Stats.Usage! with { Duration = DateTimeOffset.Now - _startedAt };
            Stats.Status = GenerationStatus.Stopped;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = DateTimeOffset.Now - _startedAt;
        var usage = Stats.Usage!;
        if (Stats.Status == GenerationStatus.Waiting)
        {
            if (elapsed.TotalMilliseconds >= 450)
            {
                // The first token: set the time to it, then stream.
                Stats.Usage = usage with { Ttft = elapsed, OutputTokens = 0 };
                Stats.Status = GenerationStatus.Streaming;
            }
            return;
        }
        _tokens += 90 * 0.05 * (0.9 + 0.2 * _random.NextDouble());
        var output = Math.Min(Target, (long)_tokens);
        if (output - (usage.OutputTokens ?? 0) < 4 && output < Target)
        {
            return;
        }
        if (_fails && output >= Target / 2)
        {
            _timer.Stop();
            Stats.Usage = usage with { OutputTokens = output, Duration = elapsed };
            Stats.ErrorText = "The connection dropped before the model finished.";
            Stats.Status = GenerationStatus.Error;
            return;
        }
        if (output >= Target)
        {
            // The last token: the duration, then Done.
            _timer.Stop();
            Stats.Usage = usage with { OutputTokens = Target, Duration = elapsed };
            Stats.Status = GenerationStatus.Done;
            return;
        }
        Stats.Usage = usage with { OutputTokens = output };
    }
}
