using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueExplorerEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public RevenueExplorerEventsDemo()
    {
        InitializeComponent();
        Revenue.OpeningMrr = RevenueExplorerDemoData.Opening;
        Revenue.Months.AddRange(RevenueExplorerDemoData.Standard());

        Quarter.Click += (_, _) => Revenue.ApplyPreset(3);
        Churn.Click += (_, _) => Revenue.IsChurnVisible = !Revenue.IsChurnVisible;
        Compare.Click += (_, _) => Revenue.IsCompareEnabled = !Revenue.IsCompareEnabled;
        Inspect.Click += (_, _) => Revenue.ScrubIndex = Revenue.RangeEnd;

        Revenue.RangeChanged += (_, e) => Write($"Range: months {e.Start} to {e.End} ({e.RangeSource})");
        Revenue.SeriesVisibilityChanged += (_, e) => Write($"Series: {e.Series} {(e.IsVisible ? "on" : "off")}");
        Revenue.CompareChanged += (_, e) => Write($"Compare: {(e.IsCompared ? "on" : "off")}");
        Revenue.ScrubChanged += (_, e) => Write(e.Month is { } m
            ? $"Inspecting {m.Month:MMM yyyy}: net new {m.NetNew:N0}"
            : "Readout closed");
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
