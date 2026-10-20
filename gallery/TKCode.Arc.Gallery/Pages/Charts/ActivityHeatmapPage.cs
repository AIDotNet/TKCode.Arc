using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class ActivityHeatmapPage
{
    /// <summary>A year of activity with a weekday rhythm, streaks and quiet weeks (deterministic).</summary>
    internal static List<ActivityDay> Year(int year, int seed)
    {
        var random = new Random(seed);
        var days = new List<ActivityDay>();
        var start = new DateOnly(year, 1, 1);
        var streak = 0;
        for (var date = start; date.Year == year; date = date.AddDays(1))
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

    public static Control Create()
    {
        var y2025 = Year(2025, 11);
        var y2024 = Year(2024, 5);
        var heatmap = new ActivityHeatmap { Label = "Contributions in 2025", Period = "2025", ItemsSource = y2025 };
        var previous = new Button { Content = "2024", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        var current = new Button { Content = "2025", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        previous.Click += (_, _) =>
        {
            heatmap.ItemsSource = y2024;
            heatmap.Period = "2024";
            heatmap.Label = "Contributions in 2024";
        };
        current.Click += (_, _) =>
        {
            heatmap.ItemsSource = y2025;
            heatmap.Period = "2025";
            heatmap.Label = "Contributions in 2025";
        };
        heatmap.Actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { previous, current } };
        var selected = GalleryUi.Text("Click a day or press Enter on it.", TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextMuted, 13);
        heatmap.DateSelected += (_, e) => selected.Text = $"Selected {e.Date:MMM d, yyyy}: {e.Count} contributions (level {e.Level})";

        var workouts = new ActivityHeatmap
        {
            Label = "Workouts, last six months",
            Period = "the last six months",
            UnitOne = "workout",
            UnitOther = "workouts",
            FirstDayOfWeek = DayOfWeek.Monday,
            Thresholds = [1, 2, 3],
            ItemsSource = Year(2025, 21).Where(d => d.Date.Month >= 5 && d.Date.Month <= 10)
                .Select(d => new ActivityDay(d.Date, Math.Min(4, d.Count / 3))).ToList(),
            MaxWidth = 520
        };
        return GalleryUi.Column(20,
            GalleryUi.Card("Contributions", "Hover or focus a day to read it; switching the year recolors the grid in a sweep.",
                GalleryUi.Column(12, heatmap, selected)),
            GalleryUi.Card("Custom unit, Monday weeks", "Fixed thresholds, Monday as the first row, and a narrow card.", workouts));
    }
}
