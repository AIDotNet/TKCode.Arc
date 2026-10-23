using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>How a range change is shown.</summary>
internal enum WeekCalendarTransition
{
    /// <summary>The new days replace the old ones at once.</summary>
    None,

    /// <summary>The panes slide one width toward the direction of travel.</summary>
    Slide,

    /// <summary>The old panes fade out where they are while the new ones fade in.</summary>
    Fade,
}

/// <summary>Range, cursor and view state of <see cref="WeekCalendar"/>, and the public navigation methods.</summary>
public partial class WeekCalendar
{
    private WeekCalendarDays _range;
    private bool _compact;
    private bool _userDate;
    private bool _settingDate;
    private DispatcherTimer? _minuteTimer;

    /// <summary>The clock when <see cref="Now"/> is null (tests replace it).</summary>
    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>The zone the grid shows.</summary>
    internal TimeZoneInfo Zone => TimeZone ?? TimeZoneInfo.Local;

    /// <summary>The culture of dates and times.</summary>
    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    /// <summary>Now: <see cref="Now"/> or the clock.</summary>
    internal DateTimeOffset CurrentNow => Now ?? Clock.GetUtcNow();

    /// <summary>Now on the wall clock of <see cref="Zone"/>.</summary>
    internal DateTime NowWall => WeekCalendarTime.ToWall(CurrentNow, Zone);

    /// <summary>Today in <see cref="Zone"/>.</summary>
    internal DateOnly Today => DateOnly.FromDateTime(NowWall);

    /// <summary>The geometry of the hours shown.</summary>
    internal WeekCalendarTimeGrid TimeGrid => new(StartHour, EndHour, HourHeight);

    /// <summary>The days shown.</summary>
    internal WeekCalendarDays Range => _range;

    /// <summary>True below the compact width.</summary>
    internal bool IsCompact => _compact;

    private void InitializeState()
    {
        var today = Today;
        _settingDate = true;
        SetCurrentValue(DateProperty, today);
        _settingDate = false;
        _range = WeekCalendarRange.For(View, today, FirstDayOfWeek);
        EffectiveView = WeekCalendarRange.Effective(View, _compact);
        UpdateRangeState();
    }

    // Public methods ------------------------------------------------------------------------------------------------------

    /// <summary>Shows the range around today with today focused, and scrolls the current time into view (Today, the T key).</summary>
    public void GoToToday()
    {
        FlushData();
        // Today disables itself once today is shown; a focused Today hands the focus to today's cell instead of losing it.
        var fromButton = _todayButton?.IsFocused == true;
        var visible = fromButton && _todayButton!.Classes.Contains(":focus-visible");
        var today = Today;
        var range = WeekCalendarRange.Settle(EffectiveView, _range, today, FirstDayOfWeek);
        Navigate(range, WeekCalendarRange.Clamp(range, today), WeekCalendarRangeChangeReason.Today);
        ScrollToNow();
        if (fromButton)
        {
            _gridView?.FocusDay(Date, visible ? NavigationMethod.Directional : NavigationMethod.Pointer);
        }
    }

    /// <summary>Focuses <paramref name="date"/>, moving to the range around it (sliding with <paramref name="animate"/>).</summary>
    public void GoTo(DateOnly date, bool animate = true)
    {
        FlushData();
        var range = WeekCalendarRange.Settle(EffectiveView, _range, date, FirstDayOfWeek);
        Navigate(range, WeekCalendarRange.Clamp(range, date), WeekCalendarRangeChangeReason.Programmatic, animate: animate);
    }

    /// <summary>Shows the next range: a week later (a day, three days in those views).</summary>
    public void Next() => Step(+1, WeekCalendarRangeChangeReason.Step);

    /// <summary>Shows the previous range.</summary>
    public void Previous() => Step(-1, WeekCalendarRangeChangeReason.Step);

    /// <summary>Scrolls the grid so <paramref name="time"/> is at the top (jumps unless <paramref name="animate"/>).</summary>
    public void ScrollToTime(TimeOnly time, bool animate = false) => _gridView?.ScrollToMinute(time.ToTimeSpan().TotalMinutes, 0, animate);

    /// <summary>Moves keyboard focus to <paramref name="date"/>'s cell, moving to the range around it first.</summary>
    public void FocusDay(DateOnly date)
    {
        GoTo(date);
        _gridView?.FocusDay(Date, NavigationMethod.Directional);
    }

    private void Step(int direction, WeekCalendarRangeChangeReason reason)
    {
        FlushData();
        var (range, cursor) = WeekCalendarRange.Step(EffectiveView, _range, Date, direction);
        Navigate(range, cursor, reason, direction);
    }

    /// <summary>Puts the current time line at a third of the visible hours (clamped), when today is shown.</summary>
    internal void ScrollToNow()
    {
        if (!_range.Contains(Today))
        {
            return;
        }
        _gridView?.ScrollToMinute(NowWall.TimeOfDay.TotalMinutes, 1.0 / 3.0, animate: false);
    }

    // The core move --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Shows <paramref name="range"/> with <paramref name="cursor"/> focused. A different range slides (Full motion, same
    /// length) or crossfades (Reduced motion, a different length); the title, the Today button, the month and the
    /// announcement change at once. When keyboard focus was in the grid it moves to the cursor's cell in the new range
    /// (Arc drops it; we keep it).
    /// </summary>
    internal void Navigate(WeekCalendarDays range, DateOnly cursor, WeekCalendarRangeChangeReason reason, int? direction = null,
                           bool animate = true, bool viewSwitch = false)
    {
        var old = _range;
        var focusInGrid = _gridView?.HasFocusWithin == true;
        var focusMethod = focusInGrid && _gridView!.FocusVisible ? NavigationMethod.Directional : NavigationMethod.Pointer;
        cursor = WeekCalendarRange.Clamp(range, cursor);
        _range = range;
        _settingDate = true;
        SetCurrentValue(DateProperty, cursor);
        _settingDate = false;
        if (old == range)
        {
            if (viewSwitch)
            {
                // The week and the agenda show the same days: only the view host changes.
                _gridView?.Show(range, cursor, WeekCalendarTransition.None, 0);
                _agendaView?.Show(range, WeekCalendarTransition.None, 0);
                UpdateRangeState();
            }
            _gridView?.SetCursor(cursor);
            _miniMonth?.Refresh();
            if (focusInGrid)
            {
                _gridView!.FocusDay(cursor, focusMethod);
            }
            return;
        }
        OnEditingRangeChanging();
        var travel = direction ?? WeekCalendarRange.Direction(old.Start, range.Start);
        var transition = WeekCalendarTransition.None;
        if (animate && IsAttachedToVisualTree())
        {
            var full = Motion.ArcMotion.IsFull(this);
            var reduced = Motion.ArcMotion.GetEffectiveLevel(this) == Motion.ArcMotionLevel.Reduced;
            if (viewSwitch || old.Count != range.Count || travel == 0)
            {
                transition = full || reduced ? WeekCalendarTransition.Fade : WeekCalendarTransition.None;
            }
            else
            {
                transition = full ? WeekCalendarTransition.Slide : reduced ? WeekCalendarTransition.Fade : WeekCalendarTransition.None;
            }
        }
        _gridView?.Show(range, cursor, transition, travel);
        _agendaView?.Show(range, transition, travel);
        UpdateRangeState();
        if (focusInGrid)
        {
            _gridView!.FocusDay(cursor, focusMethod);
        }
        if (!viewSwitch)
        {
            Announce(Labels.RangeAnnouncement(RangeTitle));
        }
        RaiseEvent(new WeekCalendarRangeChangedEventArgs(RangeChangedEvent, old.Start, old.End, range.Start, range.End,
            viewSwitch ? 0 : travel, reason));
    }

    private bool IsAttachedToVisualTree() => VisualRoot is not null;

    /// <summary>The cursor moves to <paramref name="day"/> inside the range (a header day, a cell clicked).</summary>
    internal void SetCursor(DateOnly day, bool focusCell, NavigationMethod method = NavigationMethod.Pointer)
    {
        if (!_range.Contains(day))
        {
            GoTo(day);
        }
        else
        {
            _settingDate = true;
            SetCurrentValue(DateProperty, day);
            _settingDate = false;
            _gridView?.SetCursor(day);
        }
        if (focusCell)
        {
            _gridView?.FocusDay(day, method);
        }
    }

    /// <summary>The arrow keys on a day cell or an event: the cursor moves a day, past the edge the next range slides in.</summary>
    internal void MoveCursor(int delta, DateOnly from)
    {
        var (range, cursor, travel) = WeekCalendarRange.MoveCursor(EffectiveView, _range, from, delta);
        if (travel == 0)
        {
            SetCursor(cursor, focusCell: true, NavigationMethod.Directional);
            return;
        }
        Navigate(range, cursor, WeekCalendarRangeChangeReason.Keyboard, travel);
        _gridView?.FocusDay(cursor, NavigationMethod.Directional);
    }

    /// <summary>A day picked in the month: the range around it, sliding by the order of the two ranges.</summary>
    internal void PickDay(DateOnly day)
    {
        var range = WeekCalendarRange.Settle(EffectiveView, _range, day, FirstDayOfWeek);
        Navigate(range, day, WeekCalendarRangeChangeReason.MiniMonth);
    }

    /// <summary>The D / W keys: the day view of the cursor day or the week around it.</summary>
    internal void SwitchView(WeekCalendarView view)
    {
        var focus = _gridView?.HasFocusWithin == true;
        SetCurrentValue(ViewProperty, view);
        if (focus)
        {
            _gridView?.FocusDay(Date, NavigationMethod.Directional);
        }
    }

    // View -----------------------------------------------------------------------------------------------------------------

    private void OnViewPropertyChanged(WeekCalendarView old, WeekCalendarView view)
    {
        ApplyEffectiveView(WeekCalendarRangeChangeReason.ViewSwitch, animate: true);
        RaiseEvent(new WeekCalendarViewChangedEventArgs(ViewChangedEvent, old, view, EffectiveView));
    }

    /// <summary>Recomputes the view shown (a view switch or a width change) and the days it shows around the cursor.</summary>
    private void ApplyEffectiveView(WeekCalendarRangeChangeReason reason, bool animate)
    {
        var previous = EffectiveView;
        var effective = WeekCalendarRange.Effective(View, _compact);
        UpdateViewOptions();
        if (effective == previous)
        {
            return;
        }
        EffectiveView = effective;
        var range = WeekCalendarRange.For(effective, Date, FirstDayOfWeek);
        var wasAgenda = previous == WeekCalendarView.Agenda;
        var isAgenda = effective == WeekCalendarView.Agenda;
        if (wasAgenda != isAgenda)
        {
            SwapViews(isAgenda, animate);
        }
        Navigate(range, Date, reason, 0, animate, viewSwitch: true);
        UpdateStepNames();
        if (reason == WeekCalendarRangeChangeReason.ViewSwitch)
        {
            Announce(effective == WeekCalendarView.Day
                ? Labels.DayViewAnnouncement(RangeTitle)
                : Labels.ViewAnnouncement(ViewName(effective), RangeTitle));
        }
    }

    internal string ViewName(WeekCalendarView view) => view switch
    {
        WeekCalendarView.WorkWeek => Labels.WorkWeekView,
        WeekCalendarView.Day => Labels.DayView,
        WeekCalendarView.ThreeDay => Labels.ThreeDayView,
        WeekCalendarView.Agenda => Labels.AgendaView,
        _ => Labels.WeekView,
    };

    // Derived state --------------------------------------------------------------------------------------------------------

    private void UpdateRangeState()
    {
        RangeStart = _range.Start;
        RangeEnd = _range.End;
        RangeTitle = WeekCalendarTime.RangeTitle(_range.Start, _range.End, EffectiveCulture);
        if (_titleText is not null)
        {
            _titleText.Text = RangeTitle;
        }
        if (_todayButton is not null)
        {
            _todayButton.IsEnabled = !WeekCalendarRange.IsTodayShown(EffectiveView, _range, Today, FirstDayOfWeek);
        }
        _miniMonth?.Refresh();
        UpdateStepNames();
    }

    private void OnDatePropertyChanged()
    {
        if (_settingDate)
        {
            return;
        }
        _userDate = true;
        FlushData();
        var date = Date;
        var range = WeekCalendarRange.Settle(EffectiveView, _range, date, FirstDayOfWeek);
        Navigate(range, date, WeekCalendarRangeChangeReason.Programmatic);
    }

    private void OnNowPropertyChanged()
    {
        if (!_userDate && _range.Count > 0)
        {
            // The default date follows a fixed now (demos and tests set Now after the control is built).
            var today = Today;
            var range = WeekCalendarRange.For(EffectiveView, today, FirstDayOfWeek);
            Navigate(range, today, WeekCalendarRangeChangeReason.Programmatic, animate: false);
        }
        OnNowTick();
        UpdateMinuteTimer();
    }

    /// <summary>A minute passed (or <see cref="Now"/> changed): the current time line, the past events, Today.</summary>
    internal void OnNowTick()
    {
        _gridView?.OnNowChanged();
        _agendaView?.OnNowChanged();
        if (_todayButton is not null)
        {
            _todayButton.IsEnabled = !WeekCalendarRange.IsTodayShown(EffectiveView, _range, Today, FirstDayOfWeek);
        }
    }

    private void UpdateMinuteTimer()
    {
        var run = Now is null && VisualRoot is not null;
        if (!run)
        {
            _minuteTimer?.Stop();
            return;
        }
        _minuteTimer ??= new DispatcherTimer(DispatcherPriority.Background);
        _minuteTimer.Tick -= OnMinuteTimerTick;
        _minuteTimer.Tick += OnMinuteTimerTick;
        var now = CurrentNow;
        var next = 60 - now.Second - now.Millisecond / 1000.0;
        _minuteTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.05, next));
        _minuteTimer.Start();
    }

    private void OnMinuteTimerTick(object? sender, EventArgs e)
    {
        _minuteTimer?.Stop();
        OnNowTick();
        UpdateMinuteTimer();
    }

    internal void Announce(string text) => _announcer?.Announce(text);

    private void OnHoursChanged()
    {
        ReindexNow();
    }

    private void OnZoneOrCultureChanged()
    {
        ReindexNow();
        if (!_userDate)
        {
            // The default date is today in the calendar's zone, which may not be the computer's today.
            var today = Today;
            Navigate(WeekCalendarRange.For(EffectiveView, today, FirstDayOfWeek), today, WeekCalendarRangeChangeReason.Programmatic,
                animate: false);
        }
        UpdateRangeState();
        UpdateViewOptions();
    }

    /// <summary>The data changed: every live pane redraws its days in place (no motion, as Arc).</summary>
    private void OnDataRebuilt()
    {
        _gridView?.Refresh();
        _agendaView?.Refresh();
        _sidebarList?.Refresh();
        _popoverList?.Refresh();
        _swatches?.Refresh();
        _miniMonth?.Refresh();
        PseudoClasses.Set(NoCalendarsPseudoClass, _calendars.Count == 0);
        UpdateCalendarsVisibility();
        _editor?.Refresh();
    }
}
