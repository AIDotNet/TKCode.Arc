using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks;

public class GanttTimelineTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    private static GanttModel Model(params GanttTask[] tasks) =>
        new([new GanttGroup("a", "A"), new GanttGroup("b", "B")], tasks, new HashSet<string>());

    [Fact]
    public void Rows_list_each_group_then_its_tasks_and_skip_folded_groups()
    {
        var tasks = new[]
        {
            new GanttTask("1", "One", "a", Monday, Monday.AddDays(2)),
            new GanttTask("2", "Two", "b", Monday, Monday.AddDays(1)),
        };
        var open = Model(tasks);
        Assert.Equal(4, open.Rows.Count);
        var folded = new GanttModel(open.Groups, tasks, new HashSet<string> { "a" });
        Assert.Equal(3, folded.Rows.Count);
        Assert.True(folded.Rows[0].IsCollapsed);
        Assert.Equal(1, folded.Rows[0].GroupTaskCount);
    }

    [Fact]
    public void A_task_that_starts_before_its_dependency_ends_conflicts()
    {
        var a = new GanttTask("a", "A", "a", Monday, Monday.AddDays(4));
        var ok = new GanttTask("b", "B", "a", Monday.AddDays(5), Monday.AddDays(6)) { DependsOn = ["a"] };
        var bad = new GanttTask("c", "C", "a", Monday.AddDays(4), Monday.AddDays(6)) { DependsOn = ["a"] };
        var model = Model(a, ok, bad);
        Assert.False(model.HasConflict(ok));
        Assert.True(model.HasConflict(bad));
        Assert.False(model.HasConflict(a));
    }

    [Fact]
    public void Summary_spans_the_group_and_weights_progress_by_days()
    {
        var model = Model(
            new GanttTask("1", "One", "a", Monday, Monday.AddDays(1), 1),
            new GanttTask("2", "Two", "a", Monday.AddDays(2), Monday.AddDays(7), 0));
        var s = model.Summary(model.Groups[0])!.Value;
        Assert.Equal(Monday, s.Start);
        Assert.Equal(Monday.AddDays(7), s.End);
        Assert.Equal(2.0 / 8.0, s.Progress, 6);
        Assert.Null(model.Summary(model.Groups[1]));
    }

    [Fact]
    public void Range_covers_every_task_in_whole_weeks()
    {
        var model = Model(new GanttTask("1", "One", "a", Monday.AddDays(10), Monday.AddDays(20)));
        var (first, last) = model.Range(Monday);
        Assert.Equal(DayOfWeek.Monday, first.DayOfWeek);
        Assert.True(first <= Monday.AddDays(-7));
        Assert.True(last >= Monday.AddDays(20));
    }

    [Fact]
    public void Day_widths_follow_the_zoom_and_a_milestone_spans_one_day()
    {
        Assert.Equal(36, GanttModel.DayWidth(GanttZoom.Day));
        Assert.Equal(14, GanttModel.DayWidth(GanttZoom.Week));
        Assert.Equal(5, GanttModel.DayWidth(GanttZoom.Month));
        Assert.Equal(1, GanttModel.Span(new GanttTask { Start = Monday, End = Monday.AddDays(9), IsMilestone = true }));
        Assert.Equal(10, GanttModel.Span(new GanttTask { Start = Monday, End = Monday.AddDays(9) }));
    }

    [Fact]
    public void The_rubber_band_resists_and_never_passes_its_limit()
    {
        Assert.Equal(0, GanttModel.Resist(0));
        Assert.True(GanttModel.Resist(10) < 10);
        Assert.True(GanttModel.Resist(1000) < 28.0001);
    }

    [Fact]
    public void Links_attach_to_folded_phases_and_skip_those_inside_one()
    {
        var tasks = new[]
        {
            new GanttTask("1", "One", "a", Monday, Monday.AddDays(1)),
            new GanttTask("2", "Two", "a", Monday.AddDays(2), Monday.AddDays(3)) { DependsOn = ["1"] },
            new GanttTask("3", "Three", "b", Monday.AddDays(4), Monday.AddDays(5)) { DependsOn = ["2"] },
        };
        var groups = new[] { new GanttGroup("a", "A"), new GanttGroup("b", "B") };
        Assert.Equal(2, new GanttModel(groups, tasks, new HashSet<string>()).Links.Count);
        var link = Assert.Single(new GanttModel(groups, tasks, new HashSet<string> { "a" }).Links);
        Assert.Equal("3", link.Task.Id);
        Assert.Equal("a", link.FoldedDependency?.Id);
        Assert.Null(link.FoldedTask);
    }

    private static (Window Window, GanttTimeline Timeline, GanttChart Chart) Show(double width, double? height, double windowHeight = 800)
    {
        var timeline = new GanttTimeline { Today = Monday, Width = width };
        if (height is { } h)
        {
            timeline.Height = h;
        }
        timeline.Groups.Add(new GanttGroup("a", "A"));
        for (var i = 0; i < 20; i++)
        {
            timeline.Tasks.Add(new GanttTask("t" + i, "Task " + i, "a", Monday.AddDays(i * 7), Monday.AddDays(i * 7 + 5)));
        }
        var window = new Window { Content = new ScrollViewer { Content = timeline }, Width = width, Height = windowHeight };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, timeline, timeline.GetVisualDescendants().OfType<GanttChart>().Single());
    }

    [AvaloniaTheory]
    [InlineData(420.0)]
    [InlineData(560.0)]
    public void The_frame_stays_between_the_toolbar_and_the_footer_in_a_short_block(double height)
    {
        var (window, timeline, chart) = Show(900, height);
        var buttons = timeline.GetVisualDescendants().OfType<Avalonia.Controls.Button>().ToList();
        var today = buttons.Single(b => b.Name == GanttTimeline.TodayPart);
        var undo = buttons.Single(b => b.Name == GanttTimeline.UndoPart);
        var chartTop = chart.TranslatePoint(default, timeline)!.Value.Y;
        var chartBottom = chartTop + chart.Bounds.Height;
        Assert.True(today.TranslatePoint(new Avalonia.Point(0, today.Bounds.Height), timeline)!.Value.Y <= chartTop);
        Assert.True(undo.TranslatePoint(default, timeline)!.Value.Y >= chartBottom);
        Assert.True(undo.TranslatePoint(new Avalonia.Point(0, undo.Bounds.Height), timeline)!.Value.Y <= height);
        window.Close();
    }

    [AvaloniaFact]
    public void Without_a_height_the_chart_takes_its_default_height()
    {
        // A window shorter than the block: in a taller one the ScrollViewer stretches the block to fill it.
        var (window, _, chart) = Show(900, null, windowHeight: 300);
        Assert.Equal(chart.DefaultHeight, chart.Bounds.Height, 1);
        window.Close();
    }

    [AvaloniaFact]
    public void The_scroll_bars_follow_the_chart_and_scroll_it()
    {
        var (window, timeline, chart) = Show(900, 560);
        var bars = timeline.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().ToList();
        var horizontal = bars.Single(b => b.Name == GanttTimeline.HorizontalScrollPart);
        var vertical = bars.Single(b => b.Name == GanttTimeline.VerticalScrollPart);
        Assert.Equal(chart.MaxScrollX, horizontal.Maximum, 3);
        Assert.Equal(chart.MaxScrollY, vertical.Maximum, 3);
        Assert.Equal(chart.ScrollX, horizontal.Value, 3);
        Assert.True(horizontal.Maximum > 0);
        vertical.Value = 40;
        Assert.Equal(40, chart.ScrollY, 3);
        chart.SetScroll(100, 0);
        Assert.Equal(100, horizontal.Value, 3);
        Assert.Equal(0, vertical.Value, 3);
        window.Close();
    }

    [AvaloniaFact]
    public void Dates_in_the_status_line_match_its_english_text_in_any_culture()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN");
        try
        {
            var (window, timeline, chart) = Show(900, 560);
            chart.Focus();
            chart.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.Right,
                KeyModifiers = Avalonia.Input.KeyModifiers.Alt,
            });
            var status = timeline.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == GanttTimeline.StatusPart);
            Assert.Contains("Oct 6", status.Text);
            window.Close();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = culture;
        }
    }

    [AvaloniaFact]
    public void The_control_reports_a_keyboard_move_and_undoes_it()
    {
        var timeline = new GanttTimeline { Today = Monday, Width = 900, Height = 560 };
        timeline.Groups.Add(new GanttGroup("a", "A"));
        timeline.Tasks.Add(new GanttTask("1", "One", "a", Monday, Monday.AddDays(3), 0.5));
        var window = new Window { Content = timeline, Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        GanttTasksChangedEventArgs? seen = null;
        timeline.TasksChanged += (_, e) => seen = e;
        var chart = timeline.GetVisualDescendants().OfType<GanttChart>().Single();
        chart.Focus();
        chart.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.Right,
            KeyModifiers = Avalonia.Input.KeyModifiers.Alt,
        });
        Assert.NotNull(seen);
        Assert.Equal(GanttChangeKind.Move, seen!.Change.Kind);
        Assert.Equal(Monday.AddDays(1), seen.Change.Start);
        Assert.Equal(Monday.AddDays(1), timeline.CurrentTasks[0].Start);
        Assert.Equal(Monday, timeline.Tasks[0].Start);
        window.Close();
    }

    [Fact]
    public void A_row_knows_its_place_and_a_summary_is_worked_out_once()
    {
        var tasks = new[]
        {
            new GanttTask("1", "One", "a", Monday, Monday.AddDays(1)),
            new GanttTask("2", "Two", "b", Monday, Monday.AddDays(1)),
        };
        var model = Model(tasks);
        Assert.Equal(0, model.GroupRows["a"]);
        Assert.Equal(1, model.TaskRows["1"]);
        Assert.Equal(2, model.GroupRows["b"]);
        Assert.Equal(3, model.TaskRows["2"]);
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = model.Summary(model.Groups[0]);
        var first = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        _ = model.Summary(model.Groups[0]);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < first);
    }
}
