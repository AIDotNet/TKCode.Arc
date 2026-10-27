using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageForecastWordsDemo : UserControl
{
    public UsageForecastWordsDemo()
    {
        InitializeComponent();
        Forecast.PeriodStart = UsageForecastDemoData.Start;
        Forecast.Labels = new UsageForecastLabels
        {
            CrossHeadlineFormat = "Budget reached around {0}",
            CrossDetailFormat = "{0} remain; between {1} and {2}.",
            CrossDetailPlainFormat = "{0} remain.",
            PassedHeadlineFormat = "Budget passed on {0}",
            PassedDetailFormat = "{0}% over, heading for {1} by {2}.",
            UnderHeadline = "Within budget this cycle",
            UnderDetailFormat = "Heading for {0} by {1} ({2}% of budget).",
            UsedLabel = "So far",
            ForecastLabelFormat = "By {0}",
            OverageLabel = "Extra cost",
            UnderBudgetText = "Nothing extra",
            NotifyAtText = "Warn me at",
            DaysFormat = "{0} days",
            BudgetText = "Limit"
        };
        Forecast.Metrics.AddRange(UsageForecastDemoData.Metrics());
    }
}
