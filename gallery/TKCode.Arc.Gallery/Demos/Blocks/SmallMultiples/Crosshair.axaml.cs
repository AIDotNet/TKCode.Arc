using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SmallMultiplesCrosshairDemo : UserControl
{
    public SmallMultiplesCrosshairDemo()
    {
        InitializeComponent();
        var months = SmallMultiplesDemoData.Months();
        Chart.Title = SmallMultiplesDemoData.Title;
        Chart.Description = SmallMultiplesDemoData.Description;
        Chart.Periods.AddRange(months);
        Chart.Series.AddRange(SmallMultiplesDemoData.Markets());

        Chart.CrosshairIndexChanged += (_, e) =>
            Status.Text = e.Index is { } i ? $"Crosshair on {months[i].Long}" : "Crosshair hidden";
        First.Click += (_, _) => Chart.SetCrosshairIndex(0);
        YearAgo.Click += (_, _) => Chart.SetCrosshairIndex(months.Length - 13);
        Clear.Click += (_, _) => Chart.SetCrosshairIndex(null);
    }
}
