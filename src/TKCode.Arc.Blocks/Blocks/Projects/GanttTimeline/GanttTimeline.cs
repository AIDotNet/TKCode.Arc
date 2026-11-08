using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using AvButton = Avalonia.Controls.Button;
using AvToggle = Avalonia.Controls.Primitives.ToggleButton;

namespace TKCode.Arc;

/// <summary>
/// A project timeline: tasks grouped by phase on a calendar, with a today line, dependency arrows and a summary bar per
/// phase. Drag a bar to move it or its ends to change the dates (snapped to whole days, with a dashed snap target, a date
/// label and a spring on release); arrows re-route as bars move and turn red when a task starts before one it depends on
/// ends. Zoom glides between day, week and month around the middle of the view; folding a phase slides its rows into it.
/// Everything works from the keyboard (arrows to move between bars, Alt and Alt + Shift with arrows to change dates).
/// Sized for tens of tasks; each arrow is redrawn every frame while something moves.
/// </summary>
[TemplatePart(ChartPart, typeof(GanttChart))]
[TemplatePart(DayPart, typeof(AvToggle))]
[TemplatePart(WeekPart, typeof(AvToggle))]
[TemplatePart(MonthPart, typeof(AvToggle))]
[TemplatePart(TodayPart, typeof(AvButton))]
[TemplatePart(UndoPart, typeof(AvButton))]
[TemplatePart(ResetPart, typeof(AvButton))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(HorizontalScrollPart, typeof(ScrollBar))]
[TemplatePart(VerticalScrollPart, typeof(ScrollBar))]
[PseudoClasses(NarrowPseudoClass)]
public class GanttTimeline : TemplatedControl
{
    public const string ChartPart = "PART_Chart";
    public const string DayPart = "PART_Day";
    public const string WeekPart = "PART_Week";
    public const string MonthPart = "PART_Month";
    public const string TodayPart = "PART_Today";
    public const string UndoPart = "PART_Undo";
    public const string ResetPart = "PART_Reset";
    public const string StatusPart = "PART_Status";
    public const string SubtitlePart = "PART_Subtitle";
    public const string HorizontalScrollPart = "PART_HorizontalScroll";
    public const string VerticalScrollPart = "PART_VerticalScroll";
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>The heading in the toolbar.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<GanttTimeline, string?>(nameof(Title));

    /// <summary>The phases in display order.</summary>
    public static readonly StyledProperty<AvaloniaList<GanttGroup>> GroupsProperty =
        AvaloniaProperty.Register<GanttTimeline, AvaloniaList<GanttGroup>>(nameof(Groups));

    /// <summary>The tasks. Set once; the timeline keeps its own working copy and reports changes through <see cref="TasksChanged"/>.</summary>
    public static readonly StyledProperty<AvaloniaList<GanttTask>> TasksProperty =
        AvaloniaProperty.Register<GanttTimeline, AvaloniaList<GanttTask>>(nameof(Tasks));

    /// <summary>The day that places the today line and the initial scroll (default: the current date).</summary>
    public static readonly StyledProperty<DateOnly> TodayProperty =
        AvaloniaProperty.Register<GanttTimeline, DateOnly>(nameof(Today), DateOnly.FromDateTime(DateTime.Today));

    /// <summary>The scale: day, week or month.</summary>
    public static readonly StyledProperty<GanttZoom> ZoomProperty =
        AvaloniaProperty.Register<GanttTimeline, GanttZoom>(nameof(Zoom), GanttZoom.Week, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The ids of the folded phases.</summary>
    public static readonly StyledProperty<AvaloniaList<string>> CollapsedGroupsProperty =
        AvaloniaProperty.Register<GanttTimeline, AvaloniaList<string>>(nameof(CollapsedGroups));

    private readonly List<List<GanttTask>> _undo = [];
    private GanttChart? _chart;
    private AvToggle? _day;
    private AvToggle? _week;
    private AvToggle? _month;
    private AvButton? _todayButton;
    private AvButton? _undoButton;
    private AvButton? _resetButton;
    private TextBlock? _status;
    private TextBlock? _subtitle;
    private ScrollBar? _horizontal;
    private ScrollBar? _vertical;
    private bool _syncingScroll;
    private List<GanttTask> _initial = [];
    private List<GanttTask> _working = [];
    private bool _updating;

    static GanttTimeline()
    {
        FocusableProperty.OverrideDefaultValue<GanttTimeline>(false);
    }

    public GanttTimeline()
    {
        SetCurrentValue(GroupsProperty, []);
        SetCurrentValue(TasksProperty, []);
        SetCurrentValue(CollapsedGroupsProperty, []);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="GroupsProperty"/>
    public AvaloniaList<GanttGroup> Groups { get => GetValue(GroupsProperty); set => SetValue(GroupsProperty, value); }

    /// <inheritdoc cref="TasksProperty"/>
    public AvaloniaList<GanttTask> Tasks { get => GetValue(TasksProperty); set => SetValue(TasksProperty, value); }

    /// <inheritdoc cref="TodayProperty"/>
    public DateOnly Today { get => GetValue(TodayProperty); set => SetValue(TodayProperty, value); }

    /// <inheritdoc cref="ZoomProperty"/>
    public GanttZoom Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    /// <inheritdoc cref="CollapsedGroupsProperty"/>
    public AvaloniaList<string> CollapsedGroups { get => GetValue(CollapsedGroupsProperty); set => SetValue(CollapsedGroupsProperty, value); }

    /// <summary>Raised after a move, resize, undo or reset with every task and what changed.</summary>
    public event EventHandler<GanttTasksChangedEventArgs>? TasksChanged;

    /// <summary>Raised when the zoom changes.</summary>
    public event EventHandler? ZoomChanged;

    /// <summary>Raised when a phase is folded or unfolded.</summary>
    public event EventHandler? CollapsedChanged;

    /// <summary>The tasks as they are now (after edits).</summary>
    public IReadOnlyList<GanttTask> CurrentTasks => _working;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _chart = e.NameScope.Find<GanttChart>(ChartPart);
        _day = e.NameScope.Find<AvToggle>(DayPart);
        _week = e.NameScope.Find<AvToggle>(WeekPart);
        _month = e.NameScope.Find<AvToggle>(MonthPart);
        _todayButton = e.NameScope.Find<AvButton>(TodayPart);
        _undoButton = e.NameScope.Find<AvButton>(UndoPart);
        _resetButton = e.NameScope.Find<AvButton>(ResetPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        _subtitle = e.NameScope.Find<TextBlock>(SubtitlePart);
        _horizontal = e.NameScope.Find<ScrollBar>(HorizontalScrollPart);
        _vertical = e.NameScope.Find<ScrollBar>(VerticalScrollPart);
        if (_horizontal is not null)
        {
            _horizontal.PropertyChanged += OnScrollBarChanged;
        }
        if (_vertical is not null)
        {
            _vertical.PropertyChanged += OnScrollBarChanged;
        }
        if (_chart is not null)
        {
            _chart.ScrollChanged += SyncScrollBars;
            _chart.Edited += OnEdited;
            _chart.GroupToggled += OnGroupToggled;
            _chart.Announce += OnAnnounce;
        }
        Wire(_day, GanttZoom.Day);
        Wire(_week, GanttZoom.Week);
        Wire(_month, GanttZoom.Month);
        if (_todayButton is not null)
        {
            _todayButton.Click += OnTodayClick;
        }
        if (_undoButton is not null)
        {
            _undoButton.Click += OnUndoClick;
        }
        if (_resetButton is not null)
        {
            _resetButton.Click += OnResetClick;
        }
        LoadTasks();
        Sync(false);
    }

    private void Wire(AvToggle? button, GanttZoom zoom)
    {
        if (button is not null)
        {
            button.Click += (_, _) => Zoom = zoom;
        }
    }

    private void Unhook()
    {
        if (_horizontal is not null)
        {
            _horizontal.PropertyChanged -= OnScrollBarChanged;
        }
        if (_vertical is not null)
        {
            _vertical.PropertyChanged -= OnScrollBarChanged;
        }
        if (_chart is not null)
        {
            _chart.ScrollChanged -= SyncScrollBars;
            _chart.Edited -= OnEdited;
            _chart.GroupToggled -= OnGroupToggled;
            _chart.Announce -= OnAnnounce;
        }
        if (_todayButton is not null)
        {
            _todayButton.Click -= OnTodayClick;
        }
        if (_undoButton is not null)
        {
            _undoButton.Click -= OnUndoClick;
        }
        if (_resetButton is not null)
        {
            _resetButton.Click -= OnResetClick;
        }
    }

    private void SyncScrollBars()
    {
        if (_chart is null)
        {
            return;
        }
        _syncingScroll = true;
        try
        {
            if (_horizontal is not null)
            {
                _horizontal.Maximum = _chart.MaxScrollX;
                _horizontal.ViewportSize = _chart.ViewportWidth;
                _horizontal.Value = _chart.ScrollX;
            }
            if (_vertical is not null)
            {
                _vertical.Maximum = _chart.MaxScrollY;
                _vertical.ViewportSize = _chart.ViewHeight;
                _vertical.Value = _chart.ScrollY;
            }
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    private void OnScrollBarChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncingScroll || _chart is null || e.Property != RangeBase.ValueProperty)
        {
            return;
        }
        _chart.SetScroll(_horizontal?.Value ?? _chart.ScrollX, _vertical?.Value ?? _chart.ScrollY);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PseudoClasses.Set(NarrowPseudoClass, e.NewSize.Width < 480);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_chart is null || _updating)
        {
            return;
        }
        if (change.Property == TasksProperty || change.Property == GroupsProperty || change.Property == TodayProperty)
        {
            LoadTasks();
            Sync(false);
        }
        else if (change.Property == ZoomProperty)
        {
            _chart.SetZoom(Zoom, true);
            UpdateZoomButtons();
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == CollapsedGroupsProperty)
        {
            Sync(true);
        }
    }

    private void LoadTasks()
    {
        _initial = [.. Tasks.Select(t => t.Clone())];
        _working = [.. _initial.Select(t => t.Clone())];
        _undo.Clear();
    }

    private void Sync(bool animate)
    {
        if (_chart is null)
        {
            return;
        }
        var model = new GanttModel(Groups, _working, new HashSet<string>(CollapsedGroups));
        _chart.SetData(model, Today, animate);
        if (!animate)
        {
            _chart.SetZoom(Zoom, false);
        }
        UpdateZoomButtons();
        UpdateFooter(model);
    }

    private void UpdateZoomButtons()
    {
        if (_day is not null)
        {
            _day.IsChecked = Zoom == GanttZoom.Day;
        }
        if (_week is not null)
        {
            _week.IsChecked = Zoom == GanttZoom.Week;
        }
        if (_month is not null)
        {
            _month.IsChecked = Zoom == GanttZoom.Month;
        }
    }

    private void UpdateFooter(GanttModel model)
    {
        if (_subtitle is not null)
        {
            _subtitle.Text = ((int)Math.Round(model.OverallProgress() * 100)).ToString(CultureInfo.InvariantCulture) + "% done";
        }
        var dirty = _undo.Count > 0;
        if (_undoButton is not null)
        {
            _undoButton.IsEnabled = dirty;
        }
        if (_resetButton is not null)
        {
            _resetButton.IsEnabled = dirty;
        }
    }

    private void OnEdited(GanttTask task, DateOnly start, DateOnly end, GanttChangeKind kind)
    {
        var target = _working.FirstOrDefault(t => t.Id == task.Id);
        if (target is null)
        {
            return;
        }
        _undo.Add([.. _working.Select(t => t.Clone())]);
        target.Start = start;
        target.End = target.IsMilestone ? start : end;
        Commit(new GanttChange(target.Id, target.Start, target.End, kind), kind == GanttChangeKind.Move || kind == GanttChangeKind.Resize ? Say(target, kind) : null);
    }

    private string Say(GanttTask t, GanttChangeKind kind)
    {
        var model = new GanttModel(Groups, _working, new HashSet<string>(CollapsedGroups));
        var s = t.Name + (kind == GanttChangeKind.Move ? " moved to " : " now ")
            + t.Start.ToString("MMM d", CultureInfo.InvariantCulture) + (t.IsMilestone ? string.Empty : " – " + t.End.ToString("MMM d", CultureInfo.InvariantCulture));
        return model.HasConflict(t) ? s + ". It starts before a task it depends on ends." : s + ".";
    }

    private void Commit(GanttChange change, string? sentence)
    {
        Sync(change.Kind is GanttChangeKind.Undo or GanttChangeKind.Reset);
        if (sentence is not null && _status is not null)
        {
            _status.Text = sentence;
        }
        TasksChanged?.Invoke(this, new GanttTasksChangedEventArgs(_working, change));
    }

    private void OnGroupToggled(string id)
    {
        _updating = true;
        try
        {
            if (!CollapsedGroups.Remove(id))
            {
                CollapsedGroups.Add(id);
            }
        }
        finally
        {
            _updating = false;
        }
        Sync(true);
        CollapsedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnAnnounce(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

    private void OnTodayClick(object? sender, RoutedEventArgs e) => _chart?.ScrollToToday(true);

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        if (_undo.Count == 0)
        {
            return;
        }
        var last = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _working = last;
        Commit(new GanttChange(string.Empty, default, default, GanttChangeKind.Undo), "Last change undone.");
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _working = [.. _initial.Select(t => t.Clone())];
        _undo.Clear();
        Commit(new GanttChange(string.Empty, default, default, GanttChangeKind.Reset), "Back to the starting plan.");
    }
}
