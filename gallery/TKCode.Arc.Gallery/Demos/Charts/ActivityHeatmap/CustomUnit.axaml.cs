using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActivityHeatmapCustomUnitDemo : UserControl
{
    public ActivityHeatmapCustomUnitDemo()
    {
        InitializeComponent();

        // Six months of workouts, fewer on weekends (deterministic).
        var random = new Random(21);
        var days = new List<ActivityDay>();
        for (var date = new DateOnly(2025, 5, 1); date.Month <= 10; date = date.AddDays(1))
        {
            var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var rest = random.NextDouble() < (weekend ? 0.6 : 0.25);
            days.Add(new ActivityDay(date, rest ? 0 : random.Next(1, 5)));
        }
        Heatmap.ItemsSource = days;

        // Fixed bounds for levels one to three; a count above the last bound is level four.
        Heatmap.Thresholds = [1, 2, 3];
    }
}
