using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>One build in the deploy queue demo (invented).</summary>
public sealed record StretchRefreshDemoBuild(string Name, string Branch, string Stage);

public partial class StretchRefreshCustomRowsDemo : UserControl
{
    private readonly ObservableCollection<StretchRefreshDemoBuild> _builds = new()
    {
        new("Build 512", "main · 4 min ago", "Queued"),
        new("Build 511", "release/2.3 · 9 min ago", "Testing"),
        new("Build 510", "main · 21 min ago", "Live")
    };

    private readonly ObservableCollection<string> _log = new();
    private int _next = 513;

    public StretchRefreshCustomRowsDemo()
    {
        InitializeComponent();
        Log.ItemsSource = _log;
        Feed.ItemsSource = _builds;
        Feed.ItemKeySelector = item => ((StretchRefreshDemoBuild)item).Name;
        Feed.RefreshHandler = async ct =>
        {
            await Task.Delay(900, ct);
            _builds.Insert(0, new StretchRefreshDemoBuild($"Build {_next++}", "main · just now", "Queued"));
            Feed.Subtitle = $"{_builds.Count} builds waiting";
            return "1 new build";
        };
        Feed.RefreshStarted += (_, e) => Add($"Started by {e.Trigger}");
        Feed.RefreshCompleted += (_, e) => Add(e.IsFailed ? "Failed" : $"Result: {e.ResultText}");
    }

    private void Add(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > 8)
        {
            _log.RemoveAt(_log.Count - 1);
        }
    }
}
