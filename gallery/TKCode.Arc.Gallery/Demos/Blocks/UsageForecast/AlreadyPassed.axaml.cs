using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageForecastAlreadyPassedDemo : UserControl
{
    public UsageForecastAlreadyPassedDemo()
    {
        InitializeComponent();
        Forecast.PeriodStart = UsageForecastDemoData.Start;
        var events = UsageForecastDemoData.Events();
        events.Budget = 12;
        Forecast.Metrics.Add(events);
        Forecast.AlertThresholds.Add(new UsageAlertThreshold { Percent = 50, IsEnabled = true });
        Forecast.AlertThresholds.Add(new UsageAlertThreshold { Percent = 80, IsEnabled = true });
        Forecast.AlertThresholds.Add(new UsageAlertThreshold { Percent = 100, IsEnabled = true });
    }
}
