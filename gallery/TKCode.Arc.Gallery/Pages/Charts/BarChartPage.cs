using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class BarChartPage
{
    public static Control Create()
    {
        var chart = new BarChart { Label = "Active minutes", Unit = "min", CategoryLabel = "Day" };
        void Week()
        {
            var days = ChartsADemoData.Days(7);
            var values = ChartsADemoData.Daily(7, 46, 30, 3);
            chart.Period = "Sep 24–30, 2026";
            chart.ItemsSource = days.Select((day, i) => new ChartPoint(day, values[i])
            {
                Label = day.ToString("dddd, MMM d", System.Globalization.CultureInfo.GetCultureInfo("en-US")),
                AxisLabel = day.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
            }).ToArray();
        }
        void Month()
        {
            var days = ChartsADemoData.Days(30);
            var values = ChartsADemoData.Daily(30, 40, 34, 9);
            chart.Period = "September 2026";
            chart.ItemsSource = days.Select((day, i) => new ChartPoint(day, values[i])).ToArray();
        }
        Week();
        var buttons = GalleryUi.Row(Button("Week", Week), Button("Month", Month));

        var quiet = new BarChart
        {
            Label = "Deploys",
            Period = "Last 12 weeks",
            AverageLabel = "Weekly average",
            ValueLabel = "Deploys",
            CategoryLabel = "Week",
            ShowAverage = false,
            PlotHeight = 140,
            ItemsSource = Enumerable.Range(0, 12).Select(i => new ChartPoint($"W{i + 27}", 4 + (i * 7 % 9))).ToArray()
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Week and month", "Scrub the bars (pointer or arrow keys): the headline reads the bar, the rest step back. Switch ranges: kept days morph, new ones grow beside them.",
                GalleryUi.Column(16, buttons, chart)),
            GalleryUi.Card("Without the average line", "Categories as strings, a shorter plot.", quiet));
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        button.Click += (_, _) => action();
        return button;
    }
}
