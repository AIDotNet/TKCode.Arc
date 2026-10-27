using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class HorizonChartEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public HorizonChartEventsDemo()
    {
        InitializeComponent();
        Chart.Days = HorizonChartDemoData.OrderDays();
        Chart.Series.AddRange(HorizonChartDemoData.OrderSeries());
        Chart.SortChanged += (_, e) => Write($"Sort: {e.Sort}");
        Chart.BandsChanged += (_, e) => Write($"Bands: {e.Bands}");
        Chart.DayChanged += (_, e) => Write(e.Day is { } day ? $"Day {e.Index}: {day.ShortText}" : "Crosshair cleared");
        FirstDay.Click += (_, _) => Chart.SelectDay(0);
        LastWeek.Click += (_, _) => Chart.SelectDay(Chart.Days!.Count - 7);
        Clear.Click += (_, _) => Chart.ClearSelection();
    }

    private void Write(string line)
    {
        _lines.Add(line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(0);
        }
        Log.Text = string.Join("  ·  ", _lines);
    }
}
