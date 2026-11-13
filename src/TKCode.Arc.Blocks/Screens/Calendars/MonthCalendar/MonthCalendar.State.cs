using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Which pane the day panel shows.</summary>
internal enum MonthPane
{
    Day,
    Event,
    Form,
}

/// <summary>The new event form's values.</summary>
internal sealed class MonthDraft
{
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool AllDay { get; set; }
    public TimeOnly StartTime { get; set; } = new(10, 0);
    public TimeOnly EndTime { get; set; } = new(11, 0);
    public string Location { get; set; } = string.Empty;
    public bool Video { get; set; }
    public HashSet<string> Guests { get; } = new();
    public string? CalendarId { get; set; }
    public string? TitleError { get; set; }
    public string? Error { get; set; }
}

/// <summary>The one undoable change (Arc keeps one level, offered by the toast).</summary>
internal sealed record MonthUndo(MonthUndoKind Kind, MonthCalendarEvent Event, MonthCalendarEvent? Previous, int Index);

internal enum MonthUndoKind
{
    Moved,
    Deleted,
    Created,
}

public partial class MonthCalendar
{
    private readonly Dictionary<string, MonthToneBrushes> _tones = new();
    private Panel? _root;
    private ScreenShell? _shell;
    private TextBlock? _announcer;
    private MonthPane _pane = MonthPane.Day;
    private MonthCalendarEvent? _openEvent;
    private MonthDraft? _draft;
    private MonthUndo? _undo;
    private DateOnly _focusDate;
    private bool _suppressMonthEvent;
    private bool _built;
    private string _sidebarSearch = string.Empty;
    private readonly HashSet<MonthCalendarEvent> _hooked = new();

    static MonthCalendar()
    {
        FocusableProperty.OverrideDefaultValue<MonthCalendar>(false);
    }

    /// <summary>Creates the screen on today's month with today selected.</summary>
    public MonthCalendar()
    {
        var today = ArcToday.Today;
        SetCurrentValue(MonthProperty, new DateOnly(today.Year, today.Month, 1));
        SetCurrentValue(SelectedDateProperty, today);
        _focusDate = today;
        Events.CollectionChanged += OnEventsChanged;
        Calendars.CollectionChanged += OnSourcesChanged;
        People.CollectionChanged += (_, _) => RefreshAll();
        AddHandler(KeyDownEvent, OnRootKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);
        // A shortcut that moves the focus into a text box (/ and N) must not also type its character there.
        AddHandler(TextInputEvent, (_, e) =>
        {
            if (_swallowText)
            {
                _swallowText = false;
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    // Public operations ------------------------------------------------------------------------------------------------

    /// <summary>The date treated as today.</summary>
    public DateOnly EffectiveToday => Today ?? ArcToday.Today;

    /// <summary>Shows the month of <paramref name="date"/> (with the month change motion).</summary>
    public void GoToMonth(DateOnly date) => Month = date;

    /// <summary>Shows today's month and selects today.</summary>
    public void GoToToday()
    {
        var today = EffectiveToday;
        Month = today;
        SelectDay(today, openPanel: false);
        _focusDate = today;
    }

    /// <summary>Opens <paramref name="calendarEvent"/> in the day panel (selecting its first day in the month).</summary>
    public void OpenEvent(MonthCalendarEvent calendarEvent)
    {
        var day = calendarEvent.StartDate;
        if (SelectedDate >= calendarEvent.StartDate && SelectedDate <= calendarEvent.LastDate)
        {
            day = SelectedDate;
        }
        SetCurrentValue(SelectedDateProperty, day);
        _openEvent = calendarEvent;
        _draft = null;
        _pane = MonthPane.Event;
        ShowPanelIfSheet();
        RaiseEvent(new MonthCalendarEventEventArgs(EventInvokedEvent, calendarEvent));
        RefreshGrid();
        RefreshPanel(crossfade: true);
    }

    /// <summary>Opens the new event form for <paramref name="start"/> (to <paramref name="end"/>, all day, when a span).</summary>
    public void OpenNewEvent(DateOnly start, DateOnly? end = null)
    {
        var last = end is { } e && e > start ? e : start;
        _draft = new MonthDraft
        {
            Start = start,
            End = last,
            AllDay = last > start,
            CalendarId = Calendars.FirstOrDefault(c => c.IsVisible)?.Id,
        };
        _openEvent = null;
        _pane = MonthPane.Form;
        SetCurrentValue(SelectedDateProperty, start);
        ShowPanelIfSheet();
        if (!IsDayPanelOpen && _shell is { IsPanelDocked: true })
        {
            IsDayPanelOpen = true;
        }
        RefreshGrid();
        RefreshPanel(crossfade: true);
        FocusFormTitle();
    }

    // Data -------------------------------------------------------------------------------------------------------------

    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    internal MonthCalendarSource? SourceOf(MonthCalendarEvent e) =>
        e.CalendarId is null ? null : Calendars.FirstOrDefault(c => c.Id == e.CalendarId);

    internal MonthCalendarPerson? PersonOf(string? id) => id is null ? null : People.FirstOrDefault(p => p.Id == id);

    internal MonthCalendarPerson? CurrentUser => PersonOf(CurrentUserId);

    internal ScreenPaint Paint => _shell?.Paint ?? ScreenPaint.For(this);

    internal MonthToneBrushes ToneOf(MonthCalendarEvent e) => ToneOf(SourceOf(e));

    internal MonthToneBrushes ToneOf(MonthCalendarSource? source)
    {
        var key = source is null ? "\u0000" : source.Id + "|" + source.Tone + "|" + source.CustomColor;
        if (!_tones.TryGetValue(key, out var brushes))
        {
            var paint = Paint;
            brushes = new MonthToneBrushes(MonthCalendarPalette.BaseOf(source, paint.IsDark), paint);
            _tones[key] = brushes;
        }
        return brushes;
    }

    internal static Avalonia.Media.Geometry IconOf(MonthCalendarSource? source) => source?.Icon ?? ArcIcons.Calendar;

    /// <summary>Events that may show at all in the current view (visible, on a shown calendar, the teammate's).</summary>
    internal IEnumerable<MonthCalendarEvent> ViewEvents()
    {
        foreach (var e in Events)
        {
            if (!e.IsVisible)
            {
                continue;
            }
            var source = SourceOf(e);
            if (source is not null && !source.IsShown)
            {
                continue;
            }
            if (View == MonthCalendarView.Teammate && TeammateId is { } mate)
            {
                if (e.OrganizerId != mate && e.Attendees.All(a => a.PersonId != mate))
                {
                    continue;
                }
                if (e.Rsvp == MonthCalendarRsvp.Pending)
                {
                    continue;
                }
            }
            yield return e;
        }
    }

    /// <summary>True when the filter text is empty or the title contains it.</summary>
    internal bool Matches(MonthCalendarEvent e)
    {
        var filter = FilterText?.Trim();
        return string.IsNullOrEmpty(filter) || e.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }

    internal bool IsFiltering => !string.IsNullOrWhiteSpace(FilterText);

    /// <summary>The events of the shown month per calendar (the sidebar counts); hidden calendars still count.</summary>
    internal int MonthCountOf(MonthCalendarSource source)
    {
        var first = Month;
        var last = first.AddMonths(1).AddDays(-1);
        var count = 0;
        foreach (var e in Events)
        {
            if (!e.IsVisible || e.CalendarId != source.Id || e.LastDate < first || e.StartDate > last)
            {
                continue;
            }
            if (View == MonthCalendarView.Teammate && TeammateId is { } mate
                && e.OrganizerId != mate && e.Attendees.All(a => a.PersonId != mate))
            {
                continue;
            }
            count++;
        }
        return count;
    }

    /// <summary>The events in the shown month on shown calendars (the header count) and how many of them match the filter.</summary>
    internal (int Total, int Matching) MonthTotals()
    {
        var first = Month;
        var last = first.AddMonths(1).AddDays(-1);
        var total = 0;
        var matching = 0;
        foreach (var e in ViewEvents())
        {
            if (e.LastDate < first || e.StartDate > last)
            {
                continue;
            }
            total++;
            if (Matches(e))
            {
                matching++;
            }
        }
        return (total, matching);
    }

    internal IEnumerable<MonthCalendarEvent> PendingInvitations() =>
        Events.Where(e => e.IsVisible && e.Rsvp == MonthCalendarRsvp.Pending)
              .OrderBy(e => e.StartDate).ThenBy(e => e.StartTime ?? TimeOnly.MinValue);

    internal MonthDaySummary SummaryOf(DateOnly day) =>
        MonthCalendarModel.Summarize(day, ViewEvents(), WorkDayStart, WorkDayEnd, MinimumFreeGap);

    // Change handling --------------------------------------------------------------------------------------------------

    private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Hook();
        if (_openEvent is not null && !Events.Contains(_openEvent))
        {
            _openEvent = null;
            if (_pane == MonthPane.Event)
            {
                _pane = MonthPane.Day;
            }
        }
        RefreshAll();
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var s in Calendars)
        {
            s.PropertyChanged -= OnSourcePropertyChanged;
            s.PropertyChanged += OnSourcePropertyChanged;
        }
        _tones.Clear();
        RefreshAll();
    }

    private void OnSourcePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MonthCalendarSource.ToneProperty || e.Property == MonthCalendarSource.CustomColorProperty)
        {
            _tones.Clear();
        }
        RefreshAll();
    }

    private void Hook()
    {
        foreach (var e in Events)
        {
            if (_hooked.Add(e))
            {
                e.PropertyChanged += OnEventPropertyChanged;
            }
        }
        _hooked.RemoveWhere(e =>
        {
            if (Events.Contains(e))
            {
                return false;
            }
            e.PropertyChanged -= OnEventPropertyChanged;
            return true;
        });
    }

    private bool _refreshQueued;

    private void OnEventPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_refreshQueued)
        {
            return;
        }
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            RefreshAll();
        }, DispatcherPriority.Background);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == MonthProperty)
        {
            var (oldValue, newValue) = change.GetOldAndNewValue<DateOnly>();
            if (oldValue != newValue)
            {
                if (!_suppressMonthEvent)
                {
                    RaiseEvent(new MonthCalendarDateChangedEventArgs(MonthChangedEvent, oldValue, newValue));
                }
                OnMonthChanged(oldValue, newValue);
            }
        }
        else if (p == SelectedDateProperty)
        {
            var (oldValue, newValue) = change.GetOldAndNewValue<DateOnly>();
            if (oldValue != newValue)
            {
                RaiseEvent(new MonthCalendarDateChangedEventArgs(SelectedDateChangedEvent, oldValue, newValue));
                if (_built)
                {
                    RefreshGrid();
                    RefreshPanel(crossfade: false);
                }
            }
        }
        else if (p == FilterTextProperty)
        {
            RefreshGrid();
            RefreshHeader();
            RefreshLists();
        }
        else if (p == IsDayPanelOpenProperty)
        {
            if (_shell is not null)
            {
                _shell.IsPanelOpen = IsDayPanelOpen;
            }
            RefreshHeader();
        }
        else if (p == DayPanelWidthProperty)
        {
            if (_shell is not null)
            {
                _shell.PanelWidth = DayPanelWidth;
            }
        }
        else if (p == IsSidebarCollapsedProperty)
        {
            if (_shell is not null)
            {
                _shell.IsSidebarCollapsed = IsSidebarCollapsed;
            }
            RefreshSidebar();
        }
        else if (p == ViewProperty || p == TeammateIdProperty)
        {
            RefreshAll();
        }
        else if (p == FirstDayOfWeekProperty || p == ShowWeekendsProperty || p == CultureProperty || p == LabelsProperty
                 || p == TodayProperty || p == CurrentUserIdProperty || p == WorkspaceNameProperty || p == WorkDayStartProperty
                 || p == WorkDayEndProperty || p == MinimumFreeGapProperty)
        {
            RefreshAll();
        }
    }

    private void OnMonthChanged(DateOnly oldMonth, DateOnly newMonth)
    {
        if (!_built)
        {
            return;
        }
        // Keep the keyboard day in the new month (same day of the month when it was outside).
        if (_focusDate.Year != newMonth.Year || _focusDate.Month != newMonth.Month)
        {
            var (first, weeks) = MonthCalendarModel.GridOf(newMonth, FirstDayOfWeek);
            if (_focusDate < first || _focusDate > first.AddDays(weeks * 7 - 1))
            {
                var day = Math.Min(_focusDate.Day, DateTime.DaysInMonth(newMonth.Year, newMonth.Month));
                _focusDate = new DateOnly(newMonth.Year, newMonth.Month, day);
            }
        }
        MonthChangedVisual(newMonth > oldMonth ? 1 : -1);
        RefreshSidebar();
        RefreshHeader();
    }

    // Actions ----------------------------------------------------------------------------------------------------------

    internal void SelectDay(DateOnly day, bool openPanel)
    {
        _focusDate = day;
        if (_pane != MonthPane.Day)
        {
            _pane = MonthPane.Day;
            _openEvent = null;
            _draft = null;
            SetCurrentValue(SelectedDateProperty, day);
            RefreshGrid();
            RefreshPanel(crossfade: true);
        }
        else
        {
            SetCurrentValue(SelectedDateProperty, day);
        }
        if (openPanel)
        {
            ShowPanelIfSheet();
        }
    }

    internal void ShowDayOf(DateOnly day)
    {
        if (_shell is { IsPanelDocked: true } && !IsDayPanelOpen)
        {
            IsDayPanelOpen = true;
        }
        SelectDay(day, openPanel: true);
    }

    internal void BackToDay()
    {
        if (_pane == MonthPane.Day)
        {
            return;
        }
        _pane = MonthPane.Day;
        _openEvent = null;
        _draft = null;
        RefreshGrid();
        RefreshPanel(crossfade: true);
    }

    private void ShowPanelIfSheet()
    {
        if (_shell is { IsPanelDocked: false })
        {
            _shell.IsPanelSheetOpen = true;
        }
    }

    internal void StepMonth(int delta) => Month = Month.AddMonths(delta);

    internal void Announce(string text)
    {
        if (_announcer is not null)
        {
            _announcer.Text = text;
            AutomationProperties.SetName(_announcer, text);
        }
    }

    private void Toast(string text, string? action, Action? onAction)
    {
        Announce(text);
        _shell?.ShowToast(text, action, onAction, Labels.Dismiss);
    }

    internal string ShortDate(DateOnly d) => MonthCalendarModel.Format(d, Labels.ShortDateFormat, EffectiveCulture);

    internal string MonthDay(DateOnly d) => MonthCalendarModel.Format(d, Labels.MonthDayFormat, EffectiveCulture);

    internal string LongDate(DateOnly d) => MonthCalendarModel.Format(d, Labels.LongDateFormat, EffectiveCulture);

    internal string RowTime(TimeOnly t) => MonthCalendarModel.RowTime(t, EffectiveCulture);

    /// <summary>Moves an event so it starts on <paramref name="newStart"/> (keeping its length and times).</summary>
    internal void MoveEvent(MonthCalendarEvent e, DateOnly newStart)
    {
        if (!e.IsEnabled || newStart == e.StartDate)
        {
            return;
        }
        var previous = e.Clone();
        var length = e.LastDate.DayNumber - e.StartDate.DayNumber;
        e.StartDate = newStart;
        if (e.EndDate is not null || length > 0)
        {
            e.EndDate = length > 0 ? newStart.AddDays(length) : null;
        }
        _undo = new MonthUndo(MonthUndoKind.Moved, e, previous, -1);
        RaiseEvent(new MonthCalendarEventChangedEventArgs(EventChangedEvent, e, previous));
        RefreshAll();
        Toast(string.Format(EffectiveCulture, Labels.MovedTo, e.Title, ShortDate(newStart)), Labels.Undo, Undo);
    }

    /// <summary>Deletes an event (the toast offers Undo).</summary>
    internal void DeleteEvent(MonthCalendarEvent e)
    {
        if (!e.IsEnabled)
        {
            return;
        }
        var index = Events.IndexOf(e);
        if (index < 0)
        {
            return;
        }
        _undo = new MonthUndo(MonthUndoKind.Deleted, e, null, index);
        if (ReferenceEquals(_openEvent, e))
        {
            _openEvent = null;
            _pane = MonthPane.Day;
            RefreshPanel(crossfade: true);
        }
        Events.RemoveAt(index);
        RaiseEvent(new MonthCalendarEventEventArgs(EventDeletedEvent, e));
        Toast(string.Format(EffectiveCulture, Labels.Deleted, e.Title), Labels.Undo, Undo);
    }

    /// <summary>Reverts the last move, delete or focus block.</summary>
    internal void Undo()
    {
        var undo = _undo;
        _undo = null;
        if (undo is null)
        {
            return;
        }
        switch (undo.Kind)
        {
            case MonthUndoKind.Moved when undo.Previous is not null:
            {
                var before = undo.Event.Clone();
                undo.Event.StartDate = undo.Previous.StartDate;
                undo.Event.EndDate = undo.Previous.EndDate;
                RaiseEvent(new MonthCalendarEventChangedEventArgs(EventChangedEvent, undo.Event, before));
                Announce(string.Format(EffectiveCulture, Labels.Restored, undo.Event.Title));
                RefreshAll();
                break;
            }
            case MonthUndoKind.Deleted:
            {
                var creating = new MonthCalendarEventCreatingEventArgs(EventCreatingEvent, undo.Event);
                RaiseEvent(creating);
                if (!string.IsNullOrEmpty(creating.ErrorMessage))
                {
                    Announce(creating.ErrorMessage);
                    return;
                }
                Events.Insert(Math.Min(undo.Index, Events.Count), undo.Event);
                RaiseEvent(new MonthCalendarEventEventArgs(EventCreatedEvent, undo.Event));
                Announce(string.Format(EffectiveCulture, Labels.Restored, undo.Event.Title));
                break;
            }
            case MonthUndoKind.Created:
            {
                if (Events.Remove(undo.Event))
                {
                    RaiseEvent(new MonthCalendarEventEventArgs(EventDeletedEvent, undo.Event));
                }
                break;
            }
        }
    }

    /// <summary>The user answers an invitation.</summary>
    internal void Reply(MonthCalendarEvent e, MonthCalendarRsvp reply)
    {
        if (!e.IsEnabled || e.Rsvp == reply)
        {
            return;
        }
        var previous = e.Clone();
        var old = e.Rsvp;
        e.Rsvp = reply;
        if (CurrentUserId is { } me)
        {
            var mine = e.Attendees.FirstOrDefault(a => a.PersonId == me);
            if (mine is not null)
            {
                mine.Status = reply;
            }
        }
        RaiseEvent(new MonthCalendarRsvpChangedEventArgs(RsvpChangedEvent, e, old, reply));
        RaiseEvent(new MonthCalendarEventChangedEventArgs(EventChangedEvent, e, previous));
        RefreshAll();
        var text = reply switch
        {
            MonthCalendarRsvp.Yes => string.Format(EffectiveCulture, Labels.Accepted, e.Title),
            MonthCalendarRsvp.Maybe => string.Format(EffectiveCulture, Labels.RepliedMaybe, e.Title),
            _ => string.Format(EffectiveCulture, Labels.DeclinedToast, e.Title),
        };
        Toast(text, reply == MonthCalendarRsvp.No ? null : Labels.View, reply == MonthCalendarRsvp.No ? null : () => OpenEventInMonth(e));
    }

    internal void OpenEventInMonth(MonthCalendarEvent e)
    {
        if (View is MonthCalendarView.Upcoming or MonthCalendarView.Invitations)
        {
            View = MonthCalendarView.Calendar;
        }
        _suppressMonthEvent = false;
        Month = e.StartDate;
        OpenEvent(e);
    }

    /// <summary>Puts an event on another calendar.</summary>
    internal void Recalendar(MonthCalendarEvent e, string calendarId)
    {
        if (!e.IsEnabled || e.CalendarId == calendarId)
        {
            return;
        }
        var previous = e.Clone();
        e.CalendarId = calendarId;
        RaiseEvent(new MonthCalendarEventChangedEventArgs(EventChangedEvent, e, previous));
        RefreshAll();
        var name = Calendars.FirstOrDefault(c => c.Id == calendarId)?.Name ?? calendarId;
        Announce(string.Format(EffectiveCulture, Labels.MovedToCalendar, e.Title, name));
    }

    /// <summary>Adds an event made by the user (refused when <see cref="EventCreating"/> sets an error).</summary>
    internal string? Create(MonthCalendarEvent e)
    {
        var creating = new MonthCalendarEventCreatingEventArgs(EventCreatingEvent, e);
        RaiseEvent(creating);
        if (!string.IsNullOrEmpty(creating.ErrorMessage))
        {
            return creating.ErrorMessage;
        }
        Events.Add(e);
        RaiseEvent(new MonthCalendarEventEventArgs(EventCreatedEvent, e));
        return null;
    }

    internal void BlockFocusTime(DateOnly day, TimeOnly start, TimeOnly end)
    {
        var calendar = Calendars.FirstOrDefault(c => c.IsVisible);
        var e = new MonthCalendarEvent(NewId(), Labels.FocusTime, calendar?.Id, day, start, end);
        if (Create(e) is { } error)
        {
            Announce(error);
            return;
        }
        _undo = new MonthUndo(MonthUndoKind.Created, e, null, Events.Count - 1);
        Toast(string.Format(EffectiveCulture, Labels.Blocked, RowTime(start), RowTime(end)), Labels.Undo, Undo);
    }

    internal void SaveDraft()
    {
        var d = _draft;
        if (d is null)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(d.Title))
        {
            d.TitleError = Labels.AddTitle;
            RefreshPanel(crossfade: false);
            FocusFormTitle();
            return;
        }
        if (!d.AllDay && d.EndTime <= d.StartTime)
        {
            d.Error = Labels.EndBeforeStart;
            RefreshPanel(crossfade: false);
            return;
        }
        var e = new MonthCalendarEvent(NewId(), d.Title.Trim(), d.CalendarId, d.Start, d.AllDay ? null : d.StartTime,
            d.AllDay ? null : d.EndTime)
        {
            EndDate = d.End > d.Start ? d.End : null,
            Location = string.IsNullOrWhiteSpace(d.Location) ? null : d.Location.Trim(),
            OrganizerId = CurrentUserId,
        };
        if (d.Video)
        {
            e.VideoProvider = Labels.DefaultVideoProvider;
            e.VideoUrl = "https://video.example/" + e.Id;
        }
        if (d.Guests.Count > 0 && CurrentUserId is { } me)
        {
            e.Attendees.Add(new MonthCalendarAttendee(me, MonthCalendarRsvp.Yes));
        }
        foreach (var g in d.Guests)
        {
            e.Attendees.Add(new MonthCalendarAttendee(g, MonthCalendarRsvp.Pending));
        }
        if (Create(e) is { } error)
        {
            d.Error = error;
            RefreshPanel(crossfade: false);
            return;
        }
        _draft = null;
        _openEvent = e;
        _pane = MonthPane.Event;
        RefreshAll(crossfadePanel: true);
        Toast(string.Format(EffectiveCulture, Labels.Added, e.Title, ShortDate(e.StartDate)), null, null);
    }

    internal void CancelDraft()
    {
        _draft = null;
        BackToDay();
    }

    private static int s_ids;

    private static string NewId() => "new-" + Interlocked.Increment(ref s_ids).ToString(CultureInfo.InvariantCulture);

    internal void SetCalendarShown(MonthCalendarSource source, bool shown)
    {
        if (!source.IsEnabled)
        {
            return;
        }
        source.IsShown = shown;
    }

    internal void ShowOnly(MonthCalendarSource source)
    {
        var onlyThis = Calendars.Where(c => c.IsVisible).All(c => c.IsShown == ReferenceEquals(c, source));
        foreach (var c in Calendars)
        {
            if (c.IsEnabled)
            {
                c.IsShown = onlyThis || ReferenceEquals(c, source);
            }
        }
    }

    // Keyboard ---------------------------------------------------------------------------------------------------------

    private static bool IsTextInput(object? source) => source is TextBox;

    private void OnRootKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.RoutedEvent is null)
        {
            return;
        }
        if (e.Route == Avalonia.Interactivity.RoutingStrategies.Tunnel)
        {
            _swallowText = false;
            // Ctrl/Cmd + K works everywhere.
            if (e.Key == Key.K && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
            {
                FocusSidebarSearch();
                e.Handled = true;
            }
            return;
        }
        if (IsTextInput(e.Source))
        {
            return;
        }
        if (_shell?.IsDialogOpen == true)
        {
            return;
        }
        var mods = e.KeyModifiers;
        if (mods != KeyModifiers.None && mods != KeyModifiers.Shift)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.T when mods == KeyModifiers.None:
                GoToToday();
                FocusGridDay();
                break;
            case Key.N when mods == KeyModifiers.None:
                OpenNewEvent(SelectedDate);
                break;
            case Key.Oem2 when mods == KeyModifiers.None:
            case Key.Divide:
                FocusFilter();
                break;
            case Key.PageUp:
                StepMonth(-1);
                FocusGridDay();
                break;
            case Key.PageDown:
                StepMonth(1);
                FocusGridDay();
                break;
            case Key.Escape:
                if (_shell is { IsSidebarSheetOpen: true })
                {
                    _shell.IsSidebarSheetOpen = false;
                }
                else if (_pane != MonthPane.Day)
                {
                    BackToDay();
                }
                else if (_shell is { IsPanelDocked: false, IsPanelSheetOpen: true })
                {
                    _shell.IsPanelSheetOpen = false;
                }
                else
                {
                    return;
                }
                break;
            default:
                return;
        }
        _swallowText = e.Key is Key.N or Key.Oem2 or Key.Divide or Key.T;
        e.Handled = true;
    }

    private bool _swallowText;

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!e.Handled && e.Text == "?" && !IsTextInput(e.Source) && _shell?.IsDialogOpen != true)
        {
            ShowShortcuts();
            e.Handled = true;
        }
    }
}
