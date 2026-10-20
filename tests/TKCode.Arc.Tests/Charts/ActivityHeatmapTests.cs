using Avalonia;
using System.Globalization;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Charts;

public class ActivityHeatmapTests
{
    /// <summary>2025 starts on a Wednesday; count = day of month (so the busiest day has 31).</summary>
    private static List<ActivityDay> Year2025()
    {
        var days = new List<ActivityDay>();
        for (var date = new DateOnly(2025, 1, 1); date.Year == 2025; date = date.AddDays(1))
        {
            days.Add(new ActivityDay(date, date.Day));
        }
        return days;
    }

    [AvaloniaFact]
    public void Builds_A_Calendar_Of_Weeks_With_Quarter_Levels()
    {
        var model = ActivityHeatmapModel.Build(Year2025(), DayOfWeek.Sunday, null, CultureInfo.GetCultureInfo("en-US"));
        model.Length.ShouldBe(365);
        model.Lead.ShouldBe(3);
        model.Weeks.ShouldBe(53);
        model.Thresholds.ShouldBe([8, 16, 24]);
        model.Levels[0].ShouldBe(1);
        model.Levels[30].ShouldBe(4);
        model.Months[0].Label.ShouldBe("Jan");
        model.Months.Count.ShouldBe(12);
        var monday = ActivityHeatmapModel.Build(Year2025(), DayOfWeek.Monday, [1, 2, 3], CultureInfo.GetCultureInfo("en-US"));
        monday.Lead.ShouldBe(2);
        monday.Levels[2].ShouldBe(3);
        monday.Levels[3].ShouldBe(4);
    }

    [AvaloniaFact]
    public void Summary_Uses_Total_Unit_And_Period()
    {
        var heatmap = new ActivityHeatmap { Period = "2025", ItemsSource = Year2025() };
        var window = TestHelpers.Show(heatmap);
        try
        {
            heatmap.Total.ShouldBe(Year2025().Sum(d => d.Count));
            heatmap.Summary.ShouldBe("5,738 contributions in 2025");
            heatmap.UnitOne = "workout";
            heatmap.UnitOther = "workouts";
            heatmap.Summary.ShouldBe("5,738 workouts in 2025");
            heatmap.Legend.InvalidateText();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_By_Day_And_Week_And_Selects()
    {
        var heatmap = new ActivityHeatmap { Period = "2025", ItemsSource = Year2025() };
        ActivityHeatmapDateSelectedEventArgs? selected = null;
        heatmap.DateSelected += (_, e) => selected = e;
        var window = TestHelpers.Show(heatmap);
        try
        {
            heatmap.Grid.Focus(NavigationMethod.Tab);
            heatmap.Grid.FocusIndex.ShouldBe(364);
            heatmap.Grid.IsTooltipOpen.ShouldBeTrue();
            heatmap.Grid.Reading.ShouldBe("31 contributions, Wednesday, December 31, 2025");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            heatmap.Grid.FocusIndex.ShouldBe(363);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            heatmap.Grid.FocusIndex.ShouldBe(356);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            heatmap.Grid.FocusIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            heatmap.SelectedDate.ShouldBe(new DateOnly(2025, 1, 1));
            selected!.Count.ShouldBe(1);
            selected.Level.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            heatmap.Grid.IsTooltipOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Grid_Scrolls_To_The_Newest_Week()
    {
        var heatmap = new ActivityHeatmap { Period = "2025", ItemsSource = Year2025() };
        var window = TestHelpers.Show(heatmap);
        try
        {
            // 400px window: squares bottom out at 9px, so the grid starts scrolled to its end.
            heatmap.Grid.ScrollOffset.ShouldBeGreaterThan(0);
            heatmap.Grid.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            heatmap.Grid.ScrollOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_A_Square_Opens_The_Tooltip()
    {
        var heatmap = new ActivityHeatmap { Period = "2025", ItemsSource = Year2025(), Width = 860 };
        var window = TestHelpers.Show(heatmap);
        try
        {
            window.Width = 900;
            Dispatcher.UIThread.RunJobs();
            var grid = heatmap.Grid;
            // First column, fourth row: January 1 (the range starts on a Wednesday).
            var cell = heatmap.CellSizeFor(grid.Bounds.Width);
            var point = new Avalonia.Point(heatmap.WeekdayColumn + heatmap.CellGap + cell / 2, 24 + 3 * (cell + heatmap.CellGap) + cell / 2);
            window.MouseMove(grid.TranslatePoint(point, window)!.Value);
            Dispatcher.UIThread.RunJobs();
            grid.TooltipIndex.ShouldBe(0);
            grid.IsTooltipOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Legend_Previews_And_Pins_A_Level()
    {
        var heatmap = new ActivityHeatmap { Period = "2025", ItemsSource = Year2025() };
        var window = TestHelpers.Show(heatmap);
        try
        {
            heatmap.Legend.Focus(NavigationMethod.Tab);
            heatmap.Highlight.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            heatmap.Highlight.ShouldBe(1);
            heatmap.Legend.Caption.ShouldBe("96 days");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            heatmap.PinnedLevel.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            heatmap.Highlight.ShouldBe(1);
            heatmap.RangeText(1, false).ShouldBe("1 to 8 contributions");
            heatmap.RangeText(4, true).ShouldBe("25+ contributions");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reads_Days_Through_Selectors()
    {
        var raw = new[] { ("2025-03-01", 4.0), ("2025-03-03", 2.0) };
        var heatmap = new ActivityHeatmap
        {
            ItemsSource = raw,
            DateSelector = o => DateOnly.ParseExact((((string, double))o).Item1, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            CountSelector = o => (((string, double))o).Item2
        };
        var window = TestHelpers.Show(heatmap);
        try
        {
            heatmap.Model.Length.ShouldBe(3);
            heatmap.Model.Counts.ShouldBe([4, 0, 2]);
            heatmap.Total.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }
}
