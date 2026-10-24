using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>The data side of <see cref="WeekCalendar"/>: the event and calendar collections, observed, and the day index.</summary>
public partial class WeekCalendar
{
    private readonly List<WeekCalendarEvent> _events = new();
    private readonly List<WeekCalendarSource> _calendars = new();
    private readonly Dictionary<string, WeekCalendarSource> _calendarById = new(StringComparer.Ordinal);
    private readonly HashSet<WeekCalendarEvent> _watchedEvents = new();
    private readonly HashSet<WeekCalendarSource> _watchedCalendars = new();
    private INotifyCollectionChanged? _observedEventsList;
    private INotifyCollectionChanged? _observedEvents;
    private INotifyCollectionChanged? _observedCalendarsList;
    private INotifyCollectionChanged? _observedCalendars;
    private WeekCalendarIndex _index = WeekCalendarIndex.Empty;
    private bool _dataAttached;
    private bool _rebuildPending;

    /// <summary>The events shown (from <see cref="EventsSource"/> or <see cref="Events"/>).</summary>
    internal IReadOnlyList<WeekCalendarEvent> EffectiveEvents
    {
        get
        {
            FlushData();
            return _events;
        }
    }

    /// <summary>The calendars listed (from <see cref="CalendarsSource"/> or <see cref="Calendars"/>).</summary>
    internal IReadOnlyList<WeekCalendarSource> EffectiveCalendars
    {
        get
        {
            FlushData();
            return _calendars;
        }
    }

    /// <summary>The events of the visible calendars indexed by day.</summary>
    internal WeekCalendarIndex Index
    {
        get
        {
            FlushData();
            return _index;
        }
    }

    /// <summary>The calendar with <paramref name="id"/>, or null.</summary>
    internal WeekCalendarSource? FindCalendar(string id) => _calendarById.TryGetValue(id, out var source) ? source : null;

    /// <summary>The calendar of <paramref name="calendarEvent"/>, or null.</summary>
    internal WeekCalendarSource? CalendarOf(WeekCalendarEvent calendarEvent) =>
        calendarEvent.CalendarId is { } id ? FindCalendar(id) : null;

    /// <summary>True when the event's calendar is shown (events of unknown calendars always are).</summary>
    internal bool IsShown(WeekCalendarEvent calendarEvent) => CalendarOf(calendarEvent)?.IsVisible ?? true;

    private void OnEventsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (EventsSource is null || ReferenceEquals(sender, _observedEvents))
        {
            ScheduleRebuild();
        }
    }

    private void OnCalendarsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (CalendarsSource is null || ReferenceEquals(sender, _observedCalendars))
        {
            ScheduleRebuild();
        }
    }

    private void OnEventsPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        WatchCollections();
        ScheduleRebuild();
    }

    /// <summary>
    /// The calendar follows its lists and the events and calendars in them only while it is in a visual tree (like its
    /// window and clock hooks): they are the app's and usually outlive the calendar. Back in a tree it reads them again.
    /// </summary>
    private void OnDataAttached()
    {
        _dataAttached = true;
        WatchCollections();
        // Read now rather than on the queued turn: what changed meanwhile was missed, and a render before that turn would
        // apply it mid-render (the swatches read the calendars while they draw).
        RebuildData();
    }

    private void OnDataDetached()
    {
        _dataAttached = false;
        WatchCollections();
        Watch(_events, _watchedEvents, OnEventChanged);
        Watch(_calendars, _watchedCalendars, OnCalendarChanged);
    }

    private void WatchCollections()
    {
        Observe(ref _observedEventsList, _dataAttached ? Events : null, OnEventsCollectionChanged);
        Observe(ref _observedEvents, _dataAttached ? EventsSource as INotifyCollectionChanged : null, OnEventsCollectionChanged);
        Observe(ref _observedCalendarsList, _dataAttached ? Calendars : null, OnCalendarsCollectionChanged);
        Observe(ref _observedCalendars, _dataAttached ? CalendarsSource as INotifyCollectionChanged : null, OnCalendarsCollectionChanged);
    }

    private static void Observe(ref INotifyCollectionChanged? observed, INotifyCollectionChanged? next, NotifyCollectionChangedEventHandler handler)
    {
        if (ReferenceEquals(observed, next))
        {
            return;
        }
        if (observed is not null)
        {
            observed.CollectionChanged -= handler;
        }
        observed = next;
        if (observed is not null)
        {
            observed.CollectionChanged += handler;
        }
    }

    /// <summary>
    /// Collection and item changes are applied together, once per dispatcher turn (a Clear followed by Adds rebuilds once).
    /// Public methods and the views apply pending changes first.
    /// </summary>
    internal void ScheduleRebuild()
    {
        if (_rebuildPending)
        {
            return;
        }
        _rebuildPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildPending)
            {
                RebuildData();
            }
        }, DispatcherPriority.Render);
    }

    /// <summary>Applies collection changes that are still waiting.</summary>
    internal void FlushData()
    {
        if (_rebuildPending)
        {
            RebuildData();
        }
    }

    private void RebuildData()
    {
        _rebuildPending = false;
        _events.Clear();
        IEnumerable events = EventsSource ?? Events ?? (IEnumerable)Array.Empty<WeekCalendarEvent>();
        foreach (var o in events)
        {
            if (o is WeekCalendarEvent e)
            {
                _events.Add(e);
            }
        }
        _calendars.Clear();
        _calendarById.Clear();
        IEnumerable calendars = CalendarsSource ?? Calendars ?? (IEnumerable)Array.Empty<WeekCalendarSource>();
        foreach (var o in calendars)
        {
            if (o is WeekCalendarSource source)
            {
                _calendars.Add(source);
                if (!string.IsNullOrEmpty(source.Id))
                {
                    _calendarById.TryAdd(source.Id, source);
                }
            }
        }
        Watch(_events, _watchedEvents, OnEventChanged);
        Watch(_calendars, _watchedCalendars, OnCalendarChanged);
        Paint.Invalidate();
        _index = WeekCalendarIndex.Build(_events, IsShown, Zone);
        OnDataRebuilt();
    }

    private void Watch<T>(List<T> items, HashSet<T> watched, EventHandler<AvaloniaPropertyChangedEventArgs> handler)
        where T : AvaloniaObject
    {
        foreach (var item in watched.Where(w => !_dataAttached || !items.Contains(w)).ToList())
        {
            item.PropertyChanged -= handler;
            watched.Remove(item);
        }
        if (!_dataAttached)
        {
            return;
        }
        foreach (var item in items)
        {
            if (watched.Add(item))
            {
                item.PropertyChanged += handler;
            }
        }
    }

    private void OnEventChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WeekCalendarEvent.TagProperty)
        {
            return;
        }
        ScheduleRebuild();
    }

    private void OnCalendarChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not WeekCalendarSource source || e.Property == WeekCalendarSource.TagProperty)
        {
            return;
        }
        if (e.Property == WeekCalendarSource.IsVisibleProperty)
        {
            // Arc shows and hides a calendar's events at once and relays the others out in the same frame.
            _index = WeekCalendarIndex.Build(_events, IsShown, Zone);
            OnDataRebuilt();
            var shown = e.GetNewValue<bool>();
            RaiseEvent(new WeekCalendarVisibilityChangedEventArgs(CalendarVisibilityChangedEvent, source, shown));
            return;
        }
        ScheduleRebuild();
    }

    /// <summary>Rebuilds the index without re-reading the collections (time zone or hours changed).</summary>
    private void ReindexNow()
    {
        if (_rebuildPending)
        {
            RebuildData();
            return;
        }
        _index = WeekCalendarIndex.Build(_events, IsShown, Zone);
        OnDataRebuilt();
    }
}
