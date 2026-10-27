using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageForecastFlatDemo : UserControl
{
    public UsageForecastFlatDemo()
    {
        InitializeComponent();
        Forecast.PeriodStart = UsageForecastDemoData.Start;
        Forecast.Metrics.AddRange(UsageForecastDemoData.Metrics());
    }
}
