using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActivityHeatmapContributionsDemo : UserControl
{
    private readonly List<ActivityDay> _days2024 = Year(2024, 5);
    private readonly List<ActivityDay> _days2025 = Year(2025, 11);

    public ActivityHeatmapContributionsDemo()
    {
        InitializeComponent();
        Heatmap.ItemsSource = _days2025;
    }

    private void On2024Click(object? sender, RoutedEventArgs e) => Show(2024, _days2024);

    private void On2025Click(object? sender, RoutedEventArgs e) => Show(2025, _days2025);

    // Replace the days, the period and the label together; the grid recolors in a sweep.
    private void Show(int year, List<ActivityDay> days)
    {
        Heatmap.ItemsSource = days;
        Heatmap.Period = year.ToString();
        Heatmap.Label = $"Contributions in {year}";
    }

    private void OnDateSelected(object? sender, ActivityHeatmapDateSelectedEventArgs e)
        => Selected.Text = $"Selected {e.Date:MMM d, yyyy}: {e.Count} contributions (level {e.Level})";

    /// <summary>A year of activity with a weekday rhythm, streaks and quiet weeks (deterministic).</summary>
    private static List<ActivityDay> Year(int year, int seed)
    {
        var random = new Random(seed);
        var days = new List<ActivityDay>();
        var streak = 0;
        for (var date = new DateOnly(year, 1, 1); date.Year == year; date = date.AddDays(1))
        {
            if (streak <= 0 && random.NextDouble() < 0.08)
            {
                streak = random.Next(3, 12);
            }
            var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var quiet = date.DayOfYear % 61 < 6;
            var busy = streak-- > 0;
            var count = quiet ? 0 : random.NextDouble() < (weekend ? 0.55 : 0.18) ? 0 : random.Next(1, busy ? 14 : 7);
            days.Add(new ActivityDay(date, count));
        }
        return days;
    }
}
