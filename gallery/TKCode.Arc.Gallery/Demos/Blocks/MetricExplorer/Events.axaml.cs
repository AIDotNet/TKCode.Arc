using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricExplorerEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public MetricExplorerEventsDemo()
    {
        InitializeComponent();
        foreach (var metric in MetricExplorerDemoData.Workspace())
        {
            metric.EndDate = MetricExplorerDemoData.Today;
            Explorer.Metrics.Add(metric);
        }

        OpenTeams.Click += (_, _) => Explorer.Open(1);
        Longer.Click += (_, _) => Explorer.SelectedRangeIndex = 2;
        CloseChart.Click += (_, _) => Explorer.Close();

        Explorer.MetricOpened += (_, e) => Write($"Opened: {e.Metric?.Title}");
        Explorer.MetricClosed += (_, e) => Write($"Closed: {e.Metric?.Title}");
        Explorer.SelectedMetricChanged += (_, e) => Write($"Metric: {e.Metric?.Title}");
        Explorer.SelectedRangeChanged += (_, e) => Write($"Range: {e.Range?.Label}");
        Explorer.ScrubChanged += (_, e) => Write(e.Date is { } date
            ? $"Day: {date:MMM d} = {e.Value:N2} (a period earlier {e.PreviousValue:N2})"
            : "CloseChartion cleared");
    }

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 5)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
