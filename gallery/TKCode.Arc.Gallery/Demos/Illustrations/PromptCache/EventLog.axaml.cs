using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class PromptCacheEventLogDemo : UserControl
{
    private readonly List<string> _lines = new();

    public PromptCacheEventLogDemo()
    {
        InitializeComponent();
    }

    private void OnRequestCompleted(object? sender, PromptCacheRequestCompletedEventArgs e)
    {
        var r = e.Result;
        var kind = r.Kind switch
        {
            PromptCacheRequestKind.Hit => "hit",
            PromptCacheRequestKind.Partial => $"partial hit at {r.BreakSegmentId}",
            _ => r.IsCold ? "cold write" : "write"
        };
        Add(string.Create(CultureInfo.InvariantCulture,
            $"#{r.Index + 1} at {r.Time:0}s: {kind}, {r.CachedTokens:N0} cached, {r.ProcessedTokens:N0} processed, ${r.Cost:0.0000} (${r.UncachedCost:0.0000} uncached)"));
    }

    private void OnExpired(object? sender, RoutedEventArgs e) => Add("The cache expired.");

    private void Add(string line)
    {
        _lines.Add(line);
        if (_lines.Count > 6)
        {
            _lines.RemoveAt(0);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }

    private void OnHalfSpeedClick(object? sender, RoutedEventArgs e) => Cache.Speed = 0.5;

    private void OnNormalSpeedClick(object? sender, RoutedEventArgs e) => Cache.Speed = 1;

    private void OnDoubleSpeedClick(object? sender, RoutedEventArgs e) => Cache.Speed = 2;
}
