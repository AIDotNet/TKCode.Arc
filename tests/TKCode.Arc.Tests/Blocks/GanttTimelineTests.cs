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
}
