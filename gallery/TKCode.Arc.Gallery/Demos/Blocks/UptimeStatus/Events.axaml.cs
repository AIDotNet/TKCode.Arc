using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UptimeStatusEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public UptimeStatusEventsDemo()
    {
        InitializeComponent();
        Status.Today = UptimeStatusDemoData.Now;
        Status.UpdatedAt = UptimeStatusDemoData.UpdatedAt;
        Status.Services.AddRange(UptimeStatusDemoData.Services());
        Status.Incidents.AddRange(UptimeStatusDemoData.Incidents());

        ChartIdentity.Click += (_, _) => Status.SelectedService = Status.Services.First(s => s.Key == "identity");
        OpenAll.Click += (_, _) =>
        {
            foreach (var incident in Status.Incidents)
            {
                incident.IsExpanded = true;
            }
        };
        Clear.Click += (_, _) => Status.ClearInspection();

        Status.SelectedServiceChanged += (_, e) => Write($"Charted: {e.Service?.Name}");
        Status.DayInspected += (_, e) => Write(e.Day is { } day
            ? $"Day: {e.Service.Name} {day.Date:MMM d}, {day.Status}, {day.Uptime:P2}, {day.Incidents.Count} incident(s)"
            : $"Day: {e.Service.Name} inspection ended");
        Status.PointInspected += (_, e) => Write(e.Sample is { } sample
            ? $"Sample: {e.Service?.Name} {sample.Time:HH:mm}, p50 {sample.P50:N0} ms, p95 {sample.P95:N0} ms"
            : "Sample: inspection ended");
        Status.IncidentExpandedChanged += (_, e) => Write($"{(e.IsExpanded ? "Opened" : "Closed")}: {e.Incident.Title}");
    }

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 6)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
