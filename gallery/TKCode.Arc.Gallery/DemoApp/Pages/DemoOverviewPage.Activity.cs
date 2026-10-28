using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.DemoApp;

public sealed partial class DemoOverviewPage
{
    private readonly List<ActivityDay> _reviews = OverviewData.Activity(reviews: true);
    private readonly List<ActivityDay> _handoffs = OverviewData.Activity(reviews: false);

    /// <summary>
    /// Activity: a year of design reviews or handoffs as a heatmap, with three stats beside it (from 940) and a note on the
    /// selected day under both.
    /// </summary>
    private Control BuildActivity()
    {
        var type = Segmented("Activity type", "reviews", true, ("reviews", "Reviews"), ("handoffs", "Handoffs"));
        var heatmap = new ActivityHeatmap
        {
            Period = "the last 12 months", FirstDayOfWeek = DayOfWeek.Monday, SelectedDate = new DateOnly(2026, 9, 17), Actions = type
        };
        var recent = Counter(0, size: 18);
        var streak = Counter(0, " days", size: 18);
        var busiest = Rise(Swapped(string.Empty, ArcDesignTokenKind.Foreground, 18, FontWeight.Medium));
        OverviewFact[] stats = [Fact("Last 30 days", recent), Fact("Longest streak", streak), Fact("Busiest day", busiest)];
        var statsGrid = new Grid();
        var statsBox = new Border { Child = statsGrid };
        ArcTokens.Bind(statsBox, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var note = Rise(Swapped(string.Empty));
        var noteBox = new Border { Child = note, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12, 0, 0) };
        ArcTokens.Bind(noteBox, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        Avalonia.Automation.AutomationProperties.SetLiveSetting(note, Avalonia.Automation.AutomationLiveSetting.Polite);

        var reviews = true;
        void ShowNote()
        {
            string text;
            if (heatmap.SelectedDate is not { } date)
            {
                text = "Select a day to see what the studio reviewed.";
            }
            else
            {
                var count = (int)((reviews ? _reviews : _handoffs).FirstOrDefault(day => day.Date == date)?.Count ?? 0);
                var when = date.ToString("dddd, MMMM d", OverviewData.En);
                text = count == 0
                    ? $"{when}: a quiet day, nothing was {(reviews ? "reviewed" : "handed off")}."
                    : $"{when}: {count} {Unit(count)}, mostly on {OverviewData.DayProject(date)}.";
            }
            note.Content = Swapped(text);
        }
        string Unit(int count) => reviews ? (count == 1 ? "review" : "reviews") : count == 1 ? "handoff" : "handoffs";
        void ShowType()
        {
            var days = reviews ? _reviews : _handoffs;
            heatmap.ItemsSource = days;
            heatmap.Label = reviews ? "reviews per day" : "handoffs per day";
            heatmap.UnitOne = reviews ? "review" : "handoff";
            heatmap.UnitOther = reviews ? "reviews" : "handoffs";
            heatmap.Thresholds = reviews ? [2, 4, 7] : [1, 2, 3];
            recent.Value = days.TakeLast(30).Sum(day => day.Count);
            streak.Value = LongestStreak(days);
            busiest.Content = Swapped(BusiestDay(days), ArcDesignTokenKind.Foreground, 18, FontWeight.Medium);
            ShowNote();
        }
        type.SelectionChanged += (_, _) =>
        {
            reviews = type.SelectedValue as string != "handoffs";
            ShowType();
        };
        heatmap.PropertyChanged += (_, e) =>
        {
            if (e.Property == ActivityHeatmap.SelectedDateProperty)
            {
                ShowNote();
            }
        };
        ShowType();

        var grid = new Grid { ColumnSpacing = 40, RowSpacing = 16 };
        grid.Children.Add(heatmap);
        grid.Children.Add(statsBox);
        grid.Children.Add(noteBox);
        var wide = (bool?)null;
        OnWidth(width =>
        {
            var next = width >= SplitFrom;
            if (next == wide)
            {
                return;
            }
            wide = next;
            grid.ColumnDefinitions = new ColumnDefinitions(next ? "*,168" : "*");
            grid.RowDefinitions = new RowDefinitions(next ? "Auto,Auto" : "Auto,Auto,Auto");
            Grid.SetColumn(statsBox, next ? 1 : 0);
            Grid.SetRow(statsBox, next ? 0 : 1);
            Grid.SetRow(noteBox, next ? 1 : 2);
            Grid.SetColumnSpan(noteBox, next ? 2 : 1);
            statsBox.BorderThickness = next ? new Thickness(1, 0, 0, 0) : default;
            statsBox.Padding = next ? new Thickness(24, 4, 0, 0) : default;
            OverviewFact.Layout(statsGrid, stats, next ? OverviewFactsMode.Stack : OverviewFactsMode.Columns);
            foreach (var stat in stats)
            {
                // A row of three needs no rule above it here; the panel's head does that job.
                stat.Padding = next ? stat.Padding : new Thickness(stat == stats[0] ? 0 : 16, 0, 0, 0);
            }
        });

        var panel = DemoUi.Card(grid, new Thickness(20, 16, 20, 20));
        return Section(SectionHead("Activity", Subtitle("Design reviews and handoffs over the last 12 months")), panel);
    }

    private static int LongestStreak(IEnumerable<ActivityDay> days)
    {
        int best = 0, run = 0;
        foreach (var day in days)
        {
            run = day.Count > 0 ? run + 1 : 0;
            best = Math.Max(best, run);
        }
        return best;
    }

    private static string BusiestDay(IEnumerable<ActivityDay> days)
    {
        var totals = new double[7];
        foreach (var day in days)
        {
            totals[(int)day.Date.DayOfWeek] += day.Count;
        }
        return ((DayOfWeek)Array.IndexOf(totals, totals.Max())).ToString();
    }
}
