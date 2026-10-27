using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChoroplethExplorerEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public ChoroplethExplorerEventsDemo()
    {
        InitializeComponent();
        Explorer.Areas.Add(ChoroplethExplorerDemoData.Area("us"));
        Write("Choose a state, a metric or a county.");
    }

    private void OnOpenCalifornia(object? sender, RoutedEventArgs e) => Explorer.ZoomTo("CA");

    private void OnOpenFlorida(object? sender, RoutedEventArgs e) => Explorer.ZoomTo("FL");

    private void OnZoomOut(object? sender, RoutedEventArgs e) => Explorer.ZoomOut();

    private void OnRegionOpened(object? sender, ChoroplethRegionOpenedEventArgs e) =>
        Write(e.Region is { } region ? $"RegionOpened: {region.Name} ({region.Subregions.Count} counties)" : "RegionOpened: zoomed out");

    private void OnMetricChanged(object? sender, ChoroplethSelectionChangedEventArgs e) => Write($"MetricChanged: {e.OldKey} → {e.NewKey}");

    private void OnSubregionPinned(object? sender, ChoroplethSubregionPinnedEventArgs e) =>
        Write(e.Subregion is { } county ? $"SubregionPinned: {county.Name}" : "SubregionPinned: released");

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
