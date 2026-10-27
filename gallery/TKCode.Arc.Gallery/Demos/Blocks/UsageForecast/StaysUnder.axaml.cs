using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageForecastStaysUnderDemo : UserControl
{
    public UsageForecastStaysUnderDemo()
    {
        InitializeComponent();
        Forecast.PeriodStart = UsageForecastDemoData.Start;
        var events = UsageForecastDemoData.Events();
        events.Budget = 40;
        Forecast.Metrics.Add(events);
    }
}
