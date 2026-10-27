using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageForecastPreviewDemo : UserControl
{
    public UsageForecastPreviewDemo()
    {
        InitializeComponent();
        Forecast.PeriodStart = UsageForecastDemoData.Start;
        Forecast.Metrics.AddRange(UsageForecastDemoData.Metrics());
    }
}
