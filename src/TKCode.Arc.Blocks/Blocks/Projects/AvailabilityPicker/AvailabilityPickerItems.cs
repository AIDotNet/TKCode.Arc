using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>Which time an <see cref="AvailabilityPicker"/> selects by itself after the day changes.</summary>
public enum AvailabilitySlotAutoSelect
{
    /// <summary>The first time that suits every attendee; the first selectable time when there is none (the default).</summary>
    FirstEveryoneFree,

    /// <summary>The first selectable time of the day, busy or not.</summary>
    First,

    /// <summary>Nothing: the reader picks a time.</summary>
    None
}

/// <summary>
/// One proposed meeting time on an <see cref="AvailabilityDay"/>: when it starts and ends, and which attendees are busy
/// then. A time with busy attendees can still be chosen (the footer says who has to move a meeting); set
/// <see cref="IsEnabled"/> to false for a time that cannot be booked at all.
/// </summary>
public sealed class AvailabilitySlot : AvaloniaObject
{
    /// <summary>The start, as a time of day.</summary>
    public static readonly StyledProperty<TimeSpan> StartProperty =
        AvaloniaProperty.Register<AvailabilitySlot, TimeSpan>(nameof(Start));

    /// <summary>The end, as a time of day; null ends <see cref="AvailabilityPicker.Duration"/> after the start.</summary>
    public static readonly StyledProperty<TimeSpan?> EndProperty =
        AvaloniaProperty.Register<AvailabilitySlot, TimeSpan?>(nameof(End));

    /// <summary>The attendees who are busy at this time; the picker draws a grey mark for each of them.</summary>
    public static readonly StyledProperty<AvaloniaList<ProjectPerson>> BusyAttendeesProperty =
        AvaloniaProperty.Register<AvailabilitySlot, AvaloniaList<ProjectPerson>>(nameof(BusyAttendees));

    /// <summary>A line shown under the time instead of the generated one ("Everyone's free", "Noor is busy").</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<AvailabilitySlot, string?>(nameof(Note));

    /// <summary>False shows the time dimmed and keeps it from being chosen (already taken, in the past).</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<AvailabilitySlot, bool>(nameof(IsEnabled), true);

    /// <summary>Any object the app keeps with the time (a booking id, the calendar's own slot).</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<AvailabilitySlot, object?>(nameof(Tag));

    /// <summary>True while nobody in <see cref="BusyAttendees"/> is busy.</summary>
    public static readonly DirectProperty<AvailabilitySlot, bool> IsEveryoneFreeProperty =
        AvaloniaProperty.RegisterDirect<AvailabilitySlot, bool>(nameof(IsEveryoneFree), o => o.IsEveryoneFree);

    private AvaloniaList<ProjectPerson>? _observed;
    private bool _isEveryoneFree = true;

    /// <summary>Creates an empty time.</summary>
    public AvailabilitySlot()
    {
        SetCurrentValue(BusyAttendeesProperty, new AvaloniaList<ProjectPerson>());
    }

    /// <summary>Creates a time starting at <paramref name="start"/> with the given busy attendees.</summary>
    /// <param name="start">The start, as a time of day.</param>
    /// <param name="busy">The attendees who are busy then.</param>
    public AvailabilitySlot(TimeSpan start, params ProjectPerson[] busy) : this()
    {
        Start = start;
        BusyAttendees.AddRange(busy);
    }

    /// <inheritdoc cref="StartProperty"/>
    public TimeSpan Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <inheritdoc cref="EndProperty"/>
    public TimeSpan? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <inheritdoc cref="BusyAttendeesProperty"/>
    [Content]
    public AvaloniaList<ProjectPerson> BusyAttendees { get => GetValue(BusyAttendeesProperty); set => SetValue(BusyAttendeesProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <inheritdoc cref="IsEveryoneFreeProperty"/>
    public bool IsEveryoneFree { get => _isEveryoneFree; private set => SetAndRaise(IsEveryoneFreeProperty, ref _isEveryoneFree, value); }

    /// <summary>Raised when anything the picker shows for this time changes (a property or the busy list).</summary>
    internal event EventHandler? Changed;

    /// <summary>True when <paramref name="person"/> is in <see cref="BusyAttendees"/>.</summary>
    public bool IsBusy(ProjectPerson person) => BusyAttendees?.Contains(person) == true;

    /// <inheritdoc />
    public override string ToString() => Start.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BusyAttendeesProperty)
        {
            if (_observed is not null)
            {
                _observed.CollectionChanged -= OnBusyChanged;
            }
            _observed = BusyAttendees;
            if (_observed is not null)
            {
                _observed.CollectionChanged += OnBusyChanged;
            }
            IsEveryoneFree = _observed is not { Count: > 0 };
        }
        if (change.Property != IsEveryoneFreeProperty)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnBusyChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsEveryoneFree = _observed is not { Count: > 0 };
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// One day of an <see cref="AvailabilityPicker"/>: a day card in the strip, and the times shown when it is selected
/// (sorted by start).
/// </summary>
public sealed class AvailabilityDay : AvaloniaObject
{
    /// <summary>The date: the card shows its short weekday and day number, the heading and the footer its long form.</summary>
    public static readonly StyledProperty<DateOnly> DateProperty =
        AvaloniaProperty.Register<AvailabilityDay, DateOnly>(nameof(Date));

    /// <summary>The proposed times of the day, in any order (they are shown sorted by start).</summary>
    public static readonly StyledProperty<AvaloniaList<AvailabilitySlot>> SlotsProperty =
        AvaloniaProperty.Register<AvailabilityDay, AvaloniaList<AvailabilitySlot>>(nameof(Slots));

    /// <summary>Any object the app keeps with the day.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<AvailabilityDay, object?>(nameof(Tag));

    /// <summary>How many selectable times suit every attendee (the card's "3 open").</summary>
    public static readonly DirectProperty<AvailabilityDay, int> OpenCountProperty =
        AvaloniaProperty.RegisterDirect<AvailabilityDay, int>(nameof(OpenCount), o => o.OpenCount);

    private readonly HashSet<AvailabilitySlot> _watched = new();
    private AvaloniaList<AvailabilitySlot>? _observed;
    private int _openCount;

    /// <summary>Creates an empty day.</summary>
    public AvailabilityDay()
    {
        SetCurrentValue(SlotsProperty, new AvaloniaList<AvailabilitySlot>());
    }

    /// <summary>Creates a day with its times.</summary>
    /// <param name="date">The date.</param>
    /// <param name="slots">The proposed times.</param>
    public AvailabilityDay(DateOnly date, params AvailabilitySlot[] slots) : this()
    {
        Date = date;
        Slots.AddRange(slots);
    }

    /// <inheritdoc cref="DateProperty"/>
    public DateOnly Date { get => GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <inheritdoc cref="SlotsProperty"/>
    [Content]
    public AvaloniaList<AvailabilitySlot> Slots { get => GetValue(SlotsProperty); set => SetValue(SlotsProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <inheritdoc cref="OpenCountProperty"/>
    public int OpenCount { get => _openCount; private set => SetAndRaise(OpenCountProperty, ref _openCount, value); }

    /// <summary>Raised when anything the picker shows for this day changes (the date, the times or one of them).</summary>
    internal event EventHandler? Changed;

    /// <summary>The times sorted by start (ties keep their order).</summary>
    internal List<AvailabilitySlot> SortedSlots()
    {
        var list = Slots?.ToList() ?? new List<AvailabilitySlot>();
        return list.Select((slot, index) => (slot, index)).OrderBy(p => p.slot.Start).ThenBy(p => p.index).Select(p => p.slot).ToList();
    }

    /// <inheritdoc />
    public override string ToString() => Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SlotsProperty)
        {
            if (_observed is not null)
            {
                _observed.CollectionChanged -= OnSlotsChanged;
            }
            _observed = Slots;
            if (_observed is not null)
            {
                _observed.CollectionChanged += OnSlotsChanged;
            }
            Rewatch();
        }
        if (change.Property != OpenCountProperty)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSlotsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rewatch();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Rewatch()
    {
        var current = _observed?.ToHashSet() ?? new HashSet<AvailabilitySlot>();
        foreach (var slot in _watched.Where(s => !current.Contains(s)).ToList())
        {
            slot.Changed -= OnSlotChanged;
            _watched.Remove(slot);
        }
        foreach (var slot in current)
        {
            if (_watched.Add(slot))
            {
                slot.Changed += OnSlotChanged;
            }
        }
        Recount();
    }

    private void OnSlotChanged(object? sender, EventArgs e)
    {
        Recount();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Recount() => OpenCount = _observed?.Count(s => s.IsEnabled && s.IsEveryoneFree) ?? 0;
}

/// <summary>Arguments of <see cref="AvailabilityPicker.SelectedDayChanged"/>.</summary>
public sealed class AvailabilityDayChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AvailabilityDayChangedEventArgs(RoutedEvent routedEvent, AvailabilityDay? oldDay, AvailabilityDay? newDay, int direction,
        AvailabilitySlot? autoSelectedSlot, bool isUserInitiated) : base(routedEvent)
    {
        OldDay = oldDay;
        NewDay = newDay;
        Direction = direction;
        AutoSelectedSlot = autoSelectedSlot;
        IsUserInitiated = isUserInitiated;
    }

    /// <summary>The day selected before.</summary>
    public AvailabilityDay? OldDay { get; }

    /// <summary>The day selected now.</summary>
    public AvailabilityDay? NewDay { get; }

    /// <summary>1 when the new day comes later in <see cref="AvailabilityPicker.Days"/>, -1 when earlier, 0 when either is missing.</summary>
    public int Direction { get; }

    /// <summary>The time the picker selected by itself for the new day (<see cref="AvailabilityPicker.AutoSelectSlot"/>), or null.</summary>
    public AvailabilitySlot? AutoSelectedSlot { get; }

    /// <summary>True when the reader chose the day (pointer or keyboard); false when code or a binding set it.</summary>
    public bool IsUserInitiated { get; }
}

/// <summary>Arguments of <see cref="AvailabilityPicker.SelectedSlotChanged"/>.</summary>
public sealed class AvailabilitySlotChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AvailabilitySlotChangedEventArgs(RoutedEvent routedEvent, AvailabilityDay? day, AvailabilitySlot? oldSlot, AvailabilitySlot? newSlot,
        bool isUserInitiated) : base(routedEvent)
    {
        Day = day;
        OldSlot = oldSlot;
        NewSlot = newSlot;
        IsUserInitiated = isUserInitiated;
    }

    /// <summary>The selected day when the time changed.</summary>
    public AvailabilityDay? Day { get; }

    /// <summary>The time selected before.</summary>
    public AvailabilitySlot? OldSlot { get; }

    /// <summary>The time selected now.</summary>
    public AvailabilitySlot? NewSlot { get; }

    /// <summary>True when the reader chose the time, or chose a day and the time came with it; false for code and bindings.</summary>
    public bool IsUserInitiated { get; }
}

/// <summary>
/// A selection of an <see cref="AvailabilityPicker"/> as it is confirmed or undone: the day and time, their real start and
/// end, who has to move a meeting, and the texts the footer shows.
/// </summary>
public class AvailabilitySelectionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AvailabilitySelectionEventArgs(RoutedEvent routedEvent, AvailabilityDay day, AvailabilitySlot slot, DateTime start, DateTime end,
        IReadOnlyList<ProjectPerson> conflicts, string displayDate, string displayTime, bool isUserInitiated) : base(routedEvent)
    {
        Day = day;
        Slot = slot;
        Start = start;
        End = end;
        Conflicts = conflicts;
        DisplayDate = displayDate;
        DisplayTime = displayTime;
        IsUserInitiated = isUserInitiated;
    }

    /// <summary>The selected day.</summary>
    public AvailabilityDay Day { get; }

    /// <summary>The selected time.</summary>
    public AvailabilitySlot Slot { get; }

    /// <summary>The start: the day's date at the time's start (unspecified kind: the picker's own time zone).</summary>
    public DateTime Start { get; }

    /// <summary>The end: the day's date at the time's end.</summary>
    public DateTime End { get; }

    /// <summary>The attendees who are busy at this time and will need to move a meeting.</summary>
    public IReadOnlyList<ProjectPerson> Conflicts { get; }

    /// <summary>The date as the footer shows it ("Monday, 9 March").</summary>
    public string DisplayDate { get; }

    /// <summary>The time range as the footer shows it ("11:00 – 11:30").</summary>
    public string DisplayTime { get; }

    /// <summary>True when the reader's action caused it (the confirm button, or a new choice that undid a confirmation).</summary>
    public bool IsUserInitiated { get; }
}

/// <summary>Arguments of <see cref="AvailabilityPicker.ConfirmRequested"/>: set <see cref="Cancel"/> to keep the selection unconfirmed.</summary>
public sealed class AvailabilityConfirmRequestedEventArgs : AvailabilitySelectionEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AvailabilityConfirmRequestedEventArgs(RoutedEvent routedEvent, AvailabilityDay day, AvailabilitySlot slot, DateTime start, DateTime end,
        IReadOnlyList<ProjectPerson> conflicts, string displayDate, string displayTime, bool isUserInitiated)
        : base(routedEvent, day, slot, start, end, conflicts, displayDate, displayTime, isUserInitiated)
    {
    }

    /// <summary>True keeps the selection unconfirmed (for example while a booking check fails).</summary>
    public bool Cancel { get; set; }
}
