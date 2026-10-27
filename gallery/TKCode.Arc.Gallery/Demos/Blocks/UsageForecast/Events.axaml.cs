using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageForecastEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public UsageForecastEventsDemo()
    {
        InitializeComponent();
        Forecast.PeriodStart = UsageForecastDemoData.Start;
        Forecast.Metrics.AddRange(UsageForecastDemoData.Metrics());
        Raise.Click += (_, _) => Forecast.SetBudget(30);
        Today.Click += (_, _) => Forecast.InspectedDay = 21;
        Clear.Click += (_, _) => Forecast.InspectedDay = null;
        Forecast.BudgetChanged += (_, e) =>
        {
            if (e.IsCommitted)
            {
                Write($"Budget saved: {e.Metric.Label} {e.OldValue:0.#} → {e.NewValue:0.#}");
            }
        };
        Forecast.AlertThresholdToggled += (_, e) => Write($"Alert {e.Threshold.Percent:0}%: {(e.IsEnabled ? "on" : "off")} ({e.Threshold.Status})");
        Forecast.MetricChanged += (_, e) => Write($"Metric: {e.Metric?.Label}");
        Forecast.InspectedDayChanged += (_, e) => Write(e.Day is { } day ? $"Inspecting day {day}" : "Card hidden");
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
