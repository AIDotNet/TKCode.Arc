using System.Collections;
using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class AvailabilityPicker
{
    private readonly List<AvailabilityDay> _days = new();
    private readonly List<ProjectPerson> _attendees = new();
    private readonly HashSet<AvailabilityDay> _watchedDays = new();
    private readonly HashSet<ProjectPerson> _watchedPeople = new();
    private INotifyCollectionChanged? _observedDays;
    private INotifyCollectionChanged? _observedDaysSource;
    private INotifyCollectionChanged? _observedPeople;
    private INotifyCollectionChanged? _observedPeopleSource;
    private bool _dataAttached;
    private bool _dataPending;
    private bool _templated;
    private bool _initialized;
    private bool _applying;
    private bool _suppressMotion;
    private bool _userAction;
    private bool _announceConfirm;
    private bool _compact;
    private bool _narrow;
    private (AvailabilityDay Day, AvailabilitySlot Slot)? _confirmed;
    private AvailabilitySelectionEventArgs? _lastConfirmed;
    private AvailabilityDay? _shownDay;
    private List<AvailabilitySlot> _shownSlots = new();

    private TextBlock? _title;
    private TextBlock? _subtitle;
    private ProjectsAvatarStack? _avatars;
    private AvailabilityDayStrip? _strip;
    private AvailabilityTextSwap? _dayTitle;
    private TextBlock? _freeLegend;
    private TextBlock? _busyLegend;
    private AvailabilitySlotSwap? _slots;
    private AvailabilityTextSwap? _selectionLabel;
    private AvailabilityTextSwap? _selectionValue;
    private Button? _confirm;
    private ProjectsAnnouncer? _announcer;

    // Parts and state for tests -------------------------------------------------------------------------------------------

    internal IReadOnlyList<AvailabilityDay> EffectiveDays
    {
        get
        {
            FlushData();
            return _days;
        }
    }

    internal IReadOnlyList<ProjectPerson> EffectiveAttendees
    {
        get
        {
            FlushData();
            return _attendees;
        }
    }

    internal AvailabilityDayStrip? DayStrip => _strip;
    internal AvailabilitySlotSwap? SlotSwap => _slots;
    internal ProjectsAnnouncer? Announcer => _announcer;
    internal Button? ConfirmButton => _confirm;
    internal ProjectsAvatarStack? AvatarStack => _avatars;
    internal AvailabilityTextSwap? DayTitle => _dayTitle;
    internal AvailabilityTextSwap? SelectionLabel => _selectionLabel;
    internal AvailabilityTextSwap? SelectionValue => _selectionValue;

    /// <summary>The time rows of the list in layout.</summary>
    internal IReadOnlyList<AvailabilitySlotButton> SlotButtons =>
        (_slots?.Current as AvailabilitySlotGrid)?.Buttons.ToList() ?? new List<AvailabilitySlotButton>();

    protected override AutomationPeer OnCreateAutomationPeer() => new AvailabilityGroupPeer(this, AutomationControlType.Group);

    // Data ------------------------------------------------------------------------------------------------------------------

    private void OnDaysChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleData();

    private void OnPeopleChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleData();

    private void OnDayContentChanged(object? sender, EventArgs e) => ScheduleData();

    private void OnPersonChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => ScheduleData();

    /// <summary>
    /// Data changes are applied together once per dispatcher turn, so rebuilding a day's times (clear, then add) keeps the
    /// selection when the same time comes back. Public methods apply pending changes first.
    /// </summary>
    private void ScheduleData()
    {
        if (_dataPending)
        {
            return;
        }
        _dataPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_dataPending)
            {
                FlushData();
            }
        }, DispatcherPriority.Render);
    }

    /// <summary>Applies data changes that are still waiting.</summary>
    internal void FlushData()
    {
        if (!_dataPending)
        {
            return;
        }
        _dataPending = false;
        RebuildLists();
        if (_templated)
        {
            SyncView();
        }
    }

    private void RebuildLists()
    {
        _days.Clear();
        IEnumerable days = DaysSource ?? Days ?? (IEnumerable)Array.Empty<AvailabilityDay>();
        foreach (var item in days)
        {
            if (item is AvailabilityDay day && !_days.Contains(day))
            {
                _days.Add(day);
            }
        }

        _attendees.Clear();
        IEnumerable people = AttendeesSource ?? Attendees ?? (IEnumerable)Array.Empty<ProjectPerson>();
        foreach (var item in people)
        {
            if (item is ProjectPerson person)
            {
                _attendees.Add(person);
            }
        }
        WatchItems();
    }

    /// <summary>
    /// The picker follows its lists and the days and people in them only while it is in a visual tree: they are the app's
    /// and usually outlive the picker. Back in a tree it reads them again (it missed what changed meanwhile).
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _dataAttached = true;
        WatchCollections();
        WatchItems();
        ScheduleData();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _dataAttached = false;
        WatchCollections();
        WatchItems();
    }

    private void WatchCollections()
    {
        Observe(ref _observedDays, _dataAttached ? Days : null, OnDaysChanged);
        Observe(ref _observedDaysSource, _dataAttached ? DaysSource as INotifyCollectionChanged : null, OnDaysChanged);
        Observe(ref _observedPeople, _dataAttached ? Attendees : null, OnPeopleChanged);
        Observe(ref _observedPeopleSource, _dataAttached ? AttendeesSource as INotifyCollectionChanged : null, OnPeopleChanged);
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

    private void WatchItems()
    {
        foreach (var day in _watchedDays.Where(d => !_dataAttached || !_days.Contains(d)).ToList())
        {
            day.Changed -= OnDayContentChanged;
            _watchedDays.Remove(day);
        }
        foreach (var person in _watchedPeople.Where(p => !_dataAttached || !_attendees.Contains(p)).ToList())
        {
            person.PropertyChanged -= OnPersonChanged;
            _watchedPeople.Remove(person);
        }
        if (!_dataAttached)
        {
            return;
        }
        foreach (var day in _days)
        {
            if (_watchedDays.Add(day))
            {
                day.Changed += OnDayContentChanged;
            }
        }
        foreach (var person in _attendees)
        {
            if (_watchedPeople.Add(person))
            {
                person.PropertyChanged += OnPersonChanged;
            }
        }
    }

    private AvailabilityDay? DayOf(AvailabilitySlot? slot)
    {
        if (slot is null)
        {
            return null;
        }
        foreach (var day in _days)
        {
            if (day.Slots?.Contains(slot) == true)
            {
                return day;
            }
        }
        return null;
    }

    private static bool Has(AvailabilityDay? day, AvailabilitySlot? slot) => day is not null && slot is not null && day.Slots?.Contains(slot) == true;

    /// <summary>The time chosen for <paramref name="day"/> by <see cref="AutoSelectSlot"/>.</summary>
    internal AvailabilitySlot? PickSlot(AvailabilityDay? day)
    {
        if (day is null)
        {
            return null;
        }
        var sorted = day.SortedSlots().Where(s => s.IsEnabled).ToList();
        return AutoSelectSlot switch
        {
            AvailabilitySlotAutoSelect.None => null,
            AvailabilitySlotAutoSelect.First => sorted.FirstOrDefault(),
            _ => sorted.FirstOrDefault(s => s.IsEveryoneFree) ?? sorted.FirstOrDefault()
        };
    }

    // Property changes ------------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == DaysProperty || property == DaysSourceProperty || property == AttendeesProperty
            || property == AttendeesSourceProperty)
        {
            WatchCollections();
            ScheduleData();
        }
        else if (property == SelectedDayProperty)
        {
            OnSelectedDayChanged(change.GetOldValue<AvailabilityDay?>(), change.GetNewValue<AvailabilityDay?>());
        }
        else if (property == SelectedSlotProperty)
        {
            OnSelectedSlotChanged(change.GetOldValue<AvailabilitySlot?>(), change.GetNewValue<AvailabilitySlot?>());
        }
        else if (property == IsConfirmedProperty)
        {
            OnIsConfirmedChanged(change.GetNewValue<bool>());
        }
        else if (property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (property == AutoSelectSlotProperty || property == CompactWidthProperty || property == NarrowWidthProperty)
        {
            InvalidateMeasure();
        }
        else if (IsTextProperty(property) && _templated)
        {
            UpdateAllTexts();
        }
    }

    private static bool IsTextProperty(AvaloniaProperty property) =>
        property == TitleProperty || property == SubtitleProperty || property == DurationProperty || property == TimeZoneNameProperty
        || property == ConfirmTextProperty || property == ConfirmedTextProperty || property == EveryoneFreeTextProperty
        || property == BusyFormatProperty || property == BusyManyFormatProperty || property == ConflictFormatProperty
        || property == ConfirmedLabelTextProperty || property == NoSlotTextProperty || property == NoSlotsTextProperty
        || property == OpenCountFormatProperty || property == FreeLegendTextProperty || property == BusyLegendTextProperty
        || property == AndWordProperty || property == DurationFormatProperty || property == SubtitleFormatProperty
        || property == DayNameFormatProperty || property == DayNumberFormatProperty || property == DayLabelFormatProperty
        || property == TimeFormatProperty || property == RangeFormatProperty || property == SelectionFormatProperty
        || property == AttendeesAutomationFormatProperty || property == WeekAutomationFormatProperty
        || property == DayAutomationFormatProperty || property == TimesAutomationFormatProperty
        || property == SlotAutomationFormatProperty;

    private void OnSelectedDayChanged(AvailabilityDay? old, AvailabilityDay? day)
    {
        if (_applying || !_templated)
        {
            return;
        }
        if (_dataPending)
        {
            // Read the latest days now; the view catches up with the rest of the data change in its own turn.
            RebuildLists();
        }
        if (day is null || !_days.Contains(day))
        {
            // Not one of the days (yet): the first day (or the selected time's) is selected when the data settles.
            ScheduleData();
            return;
        }
        Commit(old, SelectedSlot, autoSelect: !Has(day, SelectedSlot));
    }

    private void OnSelectedSlotChanged(AvailabilitySlot? old, AvailabilitySlot? slot)
    {
        if (_applying || !_templated)
        {
            return;
        }
        if (_dataPending)
        {
            RebuildLists();
        }
        var oldDay = SelectedDay;
        var owner = DayOf(slot);
        if (owner is not null && !ReferenceEquals(owner, oldDay))
        {
            // A time of another day: that day comes with it.
            _applying = true;
            try
            {
                SetCurrentValue(SelectedDayProperty, owner);
            }
            finally
            {
                _applying = false;
            }
        }
        Commit(oldDay, old, autoSelect: false);
    }

    /// <summary>
    /// Applies a new selection: picks a time when asked, undoes a booking, moves the highlight and the list, updates the
    /// texts and raises the events (day, then time, then the undone booking).
    /// </summary>
    private void Commit(AvailabilityDay? oldDay, AvailabilitySlot? oldSlot, bool autoSelect, bool undoBooking = true)
    {
        var user = _userAction;
        var day = SelectedDay;
        AvailabilitySlot? auto = null;
        undoBooking &= IsConfirmed;
        var undone = undoBooking ? _confirmed : null;
        _applying = true;
        try
        {
            if (autoSelect)
            {
                auto = PickSlot(day);
                SetCurrentValue(SelectedSlotProperty, auto);
            }
            if (undoBooking)
            {
                SetCurrentValue(IsConfirmedProperty, false);
            }
        }
        finally
        {
            _applying = false;
        }
        if (undoBooking)
        {
            _confirmed = null;
        }
        var slot = SelectedSlot;
        var dayChanged = !ReferenceEquals(oldDay, day);
        var slotChanged = !ReferenceEquals(oldSlot, slot);
        var direction = 0;
        if (dayChanged)
        {
            var from = oldDay is null ? -1 : _days.IndexOf(oldDay);
            var to = day is null ? -1 : _days.IndexOf(day);
            direction = from < 0 || to < 0 ? 0 : Math.Sign(to - from);
            var animate = !_suppressMotion;
            _strip?.Select(to, animate);
            ShowSlots(day, direction, animate);
        }
        else
        {
            SyncSlotChecks();
        }
        if (undone is not null)
        {
            _announcer?.Clear();
        }
        UpdateSelectionTexts();
        UpdateConfirmButton();
        if (dayChanged)
        {
            RaiseEvent(new AvailabilityDayChangedEventArgs(SelectedDayChangedEvent, oldDay, day, direction, auto, user));
        }
        if (slotChanged)
        {
            RaiseEvent(new AvailabilitySlotChangedEventArgs(SelectedSlotChangedEvent, day, oldSlot, slot, user));
        }
        if (undone is { } was)
        {
            RaiseEvent(Selection(ConfirmationResetEvent, was.Day, was.Slot, user));
        }
    }

    // Confirmation -----------------------------------------------------------------------------------------------------------

    private bool RequestConfirm(bool user)
    {
        FlushData();
        if (IsConfirmed || SelectedDay is not { } day || SelectedSlot is not { } slot || !slot.IsEnabled || !Has(day, slot))
        {
            return false;
        }
        var request = new AvailabilityConfirmRequestedEventArgs(ConfirmRequestedEvent, day, slot, StartOf(day, slot), EndOf(day, slot),
            slot.BusyAttendees.ToList(), DayLabel(day), RangeOf(day, slot), user);
        RaiseEvent(request);
        if (request.Cancel)
        {
            return false;
        }
        var wasUser = _userAction;
        _userAction = user;
        _announceConfirm = true;
        try
        {
            SetCurrentValue(IsConfirmedProperty, true);
        }
        finally
        {
            _userAction = wasUser;
            _announceConfirm = false;
        }
        if (!IsConfirmed)
        {
            return false;
        }
        if (ConfirmCommand is { } command && _lastConfirmed is { } args && command.CanExecute(args))
        {
            command.Execute(args);
        }
        return true;
    }

    private void OnIsConfirmedChanged(bool confirmed)
    {
        if (_applying || !_templated)
        {
            return;
        }
        FlushData();
        if (confirmed)
        {
            if (SelectedDay is not { } day || SelectedSlot is not { } slot || !Has(day, slot))
            {
                // Nothing to book.
                _applying = true;
                try
                {
                    SetCurrentValue(IsConfirmedProperty, false);
                }
                finally
                {
                    _applying = false;
                }
                return;
            }
            _confirmed = (day, slot);
            UpdateSelectionTexts();
            UpdateConfirmButton();
            var args = Selection(ConfirmedEvent, day, slot, _userAction);
            _lastConfirmed = args;
            if (_announceConfirm)
            {
                _announcer?.Announce(ProjectsText.Format(ConfirmedAnnouncementFormat, DayLabel(day),
                    TimeText(StartOf(day, slot)), TimeText(EndOf(day, slot))));
            }
            RaiseEvent(args);
        }
        else
        {
            var undone = _confirmed;
            _confirmed = null;
            _announcer?.Clear();
            UpdateSelectionTexts();
            UpdateConfirmButton();
            if (undone is { } was)
            {
                RaiseEvent(Selection(ConfirmationResetEvent, was.Day, was.Slot, _userAction));
            }
        }
    }

    private AvailabilitySelectionEventArgs Selection(RoutedEvent routedEvent, AvailabilityDay day, AvailabilitySlot slot, bool user) =>
        new(routedEvent, day, slot, StartOf(day, slot), EndOf(day, slot), slot.BusyAttendees.ToList(), DayLabel(day), RangeOf(day, slot), user);

    // Template --------------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_confirm is not null)
        {
            _confirm.Click -= OnConfirmClick;
        }
        if (_strip is not null)
        {
            foreach (var button in _strip.Buttons)
            {
                button.Click -= OnDayClick;
            }
        }
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _subtitle = e.NameScope.Find<TextBlock>(SubtitlePart);
        _avatars = e.NameScope.Find<ProjectsAvatarStack>(AttendeesPart);
        _strip = e.NameScope.Find<AvailabilityDayStrip>(DayStripPart);
        _dayTitle = e.NameScope.Find<AvailabilityTextSwap>(DayTitlePart);
        _freeLegend = e.NameScope.Find<TextBlock>(FreeLegendPart);
        _busyLegend = e.NameScope.Find<TextBlock>(BusyLegendPart);
        _slots = e.NameScope.Find<AvailabilitySlotSwap>(SlotsPart);
        _selectionLabel = e.NameScope.Find<AvailabilityTextSwap>(SelectionLabelPart);
        _selectionValue = e.NameScope.Find<AvailabilityTextSwap>(SelectionValuePart);
        _confirm = e.NameScope.Find<Button>(ConfirmPart);
        _announcer = e.NameScope.Find<ProjectsAnnouncer>(AnnouncerPart);
        if (_confirm is not null)
        {
            _confirm.Click += OnConfirmClick;
        }
        _templated = true;
        _shownDay = null;
        _shownSlots = new List<AvailabilitySlot>();
        _dataPending = false;
        RebuildLists();
        SyncView();
    }

    /// <summary>Brings every part up to date with the data and the selection, without motion.</summary>
    private void SyncView()
    {
        var suppress = _suppressMotion;
        _suppressMotion = true;
        try
        {
            SyncStrip();
            EnsureSelection();
            SyncShownSlots();
            ValidateConfirmation();
            UpdateAllTexts();
        }
        finally
        {
            _suppressMotion = suppress;
        }
    }

    private void SyncStrip()
    {
        if (_strip is null)
        {
            return;
        }
        if (!_strip.Buttons.Select(b => b.Day).SequenceEqual(_days))
        {
            foreach (var old in _strip.Buttons)
            {
                old.Click -= OnDayClick;
            }
            var buttons = _days.Select(day =>
            {
                var button = new AvailabilityDayButton(day);
                button.Click += OnDayClick;
                button.SetSizeClass(_compact, _narrow);
                return button;
            }).ToList();
            _strip.SetButtons(buttons);
        }
        _strip.Select(SelectedDay is { } day ? _days.IndexOf(day) : -1, animate: false);
    }

    /// <summary>
    /// Keeps a valid selection: the first time (or after the data changed) a day that is not in <see cref="Days"/> gives way
    /// to the selected time's day or the first day, and a missing time is picked by <see cref="AutoSelectSlot"/>.
    /// </summary>
    private void EnsureSelection()
    {
        if (_days.Count == 0)
        {
            return;
        }
        var day = SelectedDay;
        var slot = SelectedSlot;
        var dayValid = day is not null && _days.Contains(day);
        var owner = DayOf(slot);
        var target = dayValid ? day! : owner ?? _days[0];
        if (!_initialized && owner is not null)
        {
            target = owner;
        }
        var needsSlot = slot is null ? !_initialized : !Has(target, slot);
        // The first selection keeps a booking the app set before the picker was shown; later ones (data changed) undo it.
        var undo = _initialized;
        _initialized = true;
        if (!ReferenceEquals(target, day))
        {
            _applying = true;
            try
            {
                SetCurrentValue(SelectedDayProperty, target);
            }
            finally
            {
                _applying = false;
            }
            Commit(day, slot, needsSlot, undo);
        }
        else if (needsSlot)
        {
            Commit(day, slot, autoSelect: true, undo);
        }
    }

    private void ValidateConfirmation()
    {
        if (!IsConfirmed)
        {
            return;
        }
        if (SelectedDay is { } day && SelectedSlot is { } slot && Has(day, slot))
        {
            if (_confirmed is null || !ReferenceEquals(_confirmed.Value.Slot, slot))
            {
                // Set before the picker was shown: take it as it is (nothing to announce).
                _confirmed = (day, slot);
            }
            return;
        }
        // The booked time is gone.
        var undone = _confirmed;
        _applying = true;
        try
        {
            SetCurrentValue(IsConfirmedProperty, false);
        }
        finally
        {
            _applying = false;
        }
        _confirmed = null;
        _announcer?.Clear();
        if (undone is { } was)
        {
            RaiseEvent(Selection(ConfirmationResetEvent, was.Day, was.Slot, false));
        }
    }

    // Times -----------------------------------------------------------------------------------------------------------------

    private void ShowSlots(AvailabilityDay? day, int direction, bool animate)
    {
        if (_slots is null)
        {
            return;
        }
        var sorted = day?.SortedSlots() ?? new List<AvailabilitySlot>();
        var grid = BuildGrid(day, sorted);
        _shownDay = day;
        _shownSlots = sorted;
        _slots.Show(grid, direction, animate);
    }

    private void SyncShownSlots()
    {
        if (_slots is null)
        {
            return;
        }
        var day = SelectedDay is { } selected && _days.Contains(selected) ? selected : null;
        if (_slots.Current is null || !ReferenceEquals(_shownDay, day))
        {
            ShowSlots(day, 0, animate: false);
            return;
        }
        var sorted = day?.SortedSlots() ?? new List<AvailabilitySlot>();
        if (!sorted.SequenceEqual(_shownSlots))
        {
            _shownSlots = sorted;
            _slots.Replace(BuildGrid(day, sorted));
        }
    }

    private AvailabilitySlotGrid BuildGrid(AvailabilityDay? day, List<AvailabilitySlot> sorted)
    {
        var grid = new AvailabilitySlotGrid(day) { Columns = _compact ? 1 : 2, Gap = _slots?.ItemGap ?? 8 };
        if (day is null)
        {
            return grid;
        }
        AutomationProperties.SetName(grid, ProjectsText.Format(TimesAutomationFormat, DateOf(day)));
        if (sorted.Count == 0)
        {
            grid.Children.Add(new AvailabilityEmptyNote { Text = NoSlotsText });
            return grid;
        }
        foreach (var slot in sorted)
        {
            var button = new AvailabilitySlotButton(slot);
            button.Click += OnSlotClick;
            UpdateSlotButton(button, day);
            grid.Children.Add(button);
        }
        return grid;
    }

    private void UpdateSlotButton(AvailabilitySlotButton button, AvailabilityDay day)
    {
        var slot = button.Slot;
        var note = NoteOf(slot);
        button.TimeText = TimeText(StartOf(day, slot));
        button.NoteText = note;
        button.IsBusy = !slot.IsEveryoneFree;
        button.IsEnabled = slot.IsEnabled;
        button.BusyMarks = _attendees.Select(slot.IsBusy).ToArray();
        button.IsChecked = ReferenceEquals(slot, SelectedSlot);
        AutomationProperties.SetName(button, ProjectsText.Format(SlotAutomationFormat, button.TimeText, TimeText(EndOf(day, slot)), note));
    }

    private void SyncSlotChecks()
    {
        foreach (var button in SlotButtons)
        {
            button.IsChecked = ReferenceEquals(button.Slot, SelectedSlot);
        }
    }

    // Input -----------------------------------------------------------------------------------------------------------------

    private void OnDayClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not AvailabilityDayButton button)
        {
            return;
        }
        FlushData();
        _userAction = true;
        try
        {
            if (!ReferenceEquals(button.Day, SelectedDay) && _days.Contains(button.Day))
            {
                SetCurrentValue(SelectedDayProperty, button.Day);
            }
        }
        finally
        {
            _userAction = false;
        }
        // The card does not toggle itself; keep its state with the selection.
        button.IsChecked = ReferenceEquals(button.Day, SelectedDay);
    }

    private void OnSlotClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not AvailabilitySlotButton button)
        {
            return;
        }
        FlushData();
        _userAction = true;
        try
        {
            if (button.Slot.IsEnabled && !ReferenceEquals(button.Slot, SelectedSlot) && DayOf(button.Slot) is not null)
            {
                SetCurrentValue(SelectedSlotProperty, button.Slot);
            }
        }
        finally
        {
            _userAction = false;
        }
        button.IsChecked = ReferenceEquals(button.Slot, SelectedSlot);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        // Once booked the button stays focusable but does nothing (aria-disabled).
        if (!IsConfirmed)
        {
            RequestConfirm(user: true);
        }
    }

    // Breakpoints -------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        // Size classes follow the width offered to the picker and are set before its parts measure, so the columns, paddings
        // and the stage height change in the same pass (the stage's height spring sees a passive change and jumps).
        UpdateBreakpoints(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        // An unbounded width (a horizontal stack) is wide.
        var compact = !double.IsInfinity(width) && width < CompactWidth;
        var narrow = !double.IsInfinity(width) && width <= NarrowWidth;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (compact == _compact && narrow == _narrow)
        {
            return;
        }
        _compact = compact;
        _narrow = narrow;
        if (_strip is not null)
        {
            foreach (var button in _strip.Buttons)
            {
                button.SetSizeClass(compact, narrow);
            }
        }
        if (_slots is not null)
        {
            foreach (var grid in _slots.Leaving.Append(_slots.Current).OfType<AvailabilitySlotGrid>())
            {
                grid.Columns = compact ? 1 : 2;
            }
        }
    }

    // Texts -----------------------------------------------------------------------------------------------------------------

    private void UpdateAllTexts()
    {
        UpdateHeader();
        if (_strip is not null)
        {
            foreach (var button in _strip.Buttons)
            {
                UpdateDayButton(button);
            }
            AutomationProperties.SetName(_strip, _days.Count > 0 ? ProjectsText.Format(WeekAutomationFormat, DateOf(_days[0])) : string.Empty);
        }
        if (_shownDay is { } day && _slots?.Current is AvailabilitySlotGrid grid)
        {
            AutomationProperties.SetName(grid, ProjectsText.Format(TimesAutomationFormat, DateOf(day)));
            foreach (var button in grid.Buttons)
            {
                UpdateSlotButton(button, day);
            }
            foreach (var empty in grid.Children.OfType<AvailabilityEmptyNote>())
            {
                empty.Text = NoSlotsText;
            }
        }
        UpdateSelectionTexts();
        UpdateConfirmButton();
    }

    private void UpdateHeader()
    {
        AutomationProperties.SetName(this, Title ?? string.Empty);
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (_subtitle is not null)
        {
            var text = SubtitleText();
            _subtitle.Text = text;
            _subtitle.IsVisible = !string.IsNullOrEmpty(text);
        }
        if (_avatars is not null)
        {
            _avatars.People = _attendees.ToList();
            _avatars.Label = ProjectsText.Format(AttendeesAutomationFormat, ProjectsText.JoinNames(_attendees.Select(p => p.Name), AndWord));
            _avatars.IsVisible = _attendees.Count > 0;
        }
        if (_freeLegend is not null)
        {
            _freeLegend.Text = FreeLegendText;
        }
        if (_busyLegend is not null)
        {
            _busyLegend.Text = BusyLegendText;
        }
    }

    private void UpdateDayButton(AvailabilityDayButton button)
    {
        var date = DateOf(button.Day);
        button.DayName = ProjectsText.Format(DayNameFormat, date);
        button.DayNumber = ProjectsText.Format(DayNumberFormat, date);
        button.OpenText = ProjectsText.Format(OpenCountFormat, button.Day.OpenCount);
        AutomationProperties.SetName(button, ProjectsText.Format(DayAutomationFormat, date, button.Day.OpenCount));
    }

    private void UpdateSelectionTexts()
    {
        var day = SelectedDay is { } selected && _days.Contains(selected) ? selected : null;
        var slot = Has(day, SelectedSlot) ? SelectedSlot : null;
        if (_dayTitle is not null)
        {
            _dayTitle.Text = day is null ? null : DayLabel(day);
        }
        if (_selectionLabel is not null)
        {
            _selectionLabel.Text = IsConfirmed && slot is not null ? ConfirmedLabelText
                : slot is null ? NoSlotText
                : slot.IsEveryoneFree ? EveryoneFreeText
                : ProjectsText.Format(ConflictFormat, ShortNames(slot));
        }
        if (_selectionValue is not null)
        {
            _selectionValue.Text = day is null ? null
                : slot is null ? DayLabel(day)
                : ProjectsText.Format(SelectionFormat, DayLabel(day), RangeOf(day, slot));
        }
    }

    private void UpdateConfirmButton()
    {
        var confirmed = IsConfirmed;
        PseudoClasses.Set(ConfirmedPseudoClass, confirmed);
        if (_confirm is null)
        {
            return;
        }
        _confirm.Variant = confirmed ? ButtonVariant.Secondary : ButtonVariant.Primary;
        _confirm.IsPressScaleEnabled = !confirmed;
        if (confirmed)
        {
            if (_confirm.Content is AvailabilityBookedLabel booked)
            {
                booked.Text = ConfirmedText;
            }
            else
            {
                _confirm.Content = new AvailabilityBookedLabel(ConfirmedText);
            }
        }
        else
        {
            _confirm.Content = ConfirmText;
        }
        AutomationProperties.SetName(_confirm, confirmed ? ConfirmedText : ConfirmText);
        var day = SelectedDay;
        _confirm.IsEnabled = confirmed || (SelectedSlot is { IsEnabled: true } slot && Has(day, slot));
    }

    private string SubtitleText()
    {
        if (Subtitle is { } subtitle)
        {
            return subtitle;
        }
        var duration = ProjectsText.Format(DurationFormat, (int)Math.Round(Duration.TotalMinutes));
        return string.IsNullOrEmpty(TimeZoneName) ? duration : ProjectsText.Format(SubtitleFormat, duration, TimeZoneName);
    }

    /// <summary>The line of a time: its <see cref="AvailabilitySlot.Note"/>, everyone free, or who is busy.</summary>
    internal string NoteOf(AvailabilitySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.Note))
        {
            return slot.Note!;
        }
        var busy = slot.BusyAttendees;
        return busy is not { Count: > 0 } ? EveryoneFreeText
            : busy.Count == 1 ? ProjectsText.Format(BusyFormat, busy[0].DisplayShortName)
            : ProjectsText.Format(BusyManyFormat, ShortNames(slot));
    }

    private string ShortNames(AvailabilitySlot slot) => ProjectsText.JoinNames(slot.BusyAttendees.Select(p => p.DisplayShortName), AndWord);

    private static DateTime DateOf(AvailabilityDay day) => day.Date.ToDateTime(TimeOnly.MinValue);

    private DateTime StartOf(AvailabilityDay day, AvailabilitySlot slot) => DateOf(day) + slot.Start;

    private DateTime EndOf(AvailabilityDay day, AvailabilitySlot slot) => DateOf(day) + (slot.End ?? slot.Start + Duration);

    private string TimeText(DateTime time) => ProjectsText.Format(TimeFormat, time);

    internal string DayLabel(AvailabilityDay day) => ProjectsText.Format(DayLabelFormat, DateOf(day));

    internal string RangeOf(AvailabilityDay day, AvailabilitySlot slot) =>
        ProjectsText.Format(RangeFormat, TimeText(StartOf(day, slot)), TimeText(EndOf(day, slot)));
}

/// <summary>The line shown in place of the times on a day without any.</summary>
internal sealed class AvailabilityEmptyNote : TextBlock
{
    protected override Type StyleKeyOverride => typeof(AvailabilityEmptyNote);
}

/// <summary>The booked button's content: a check and <see cref="Text"/>.</summary>
internal sealed class AvailabilityBookedLabel : StackPanel
{
    private readonly TextBlock _text;

    public AvailabilityBookedLabel(string text)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        Children.Add(new ArcGlyph
        {
            Data = ArcIcons.Check,
            Size = 16,
            StrokeThickness = 2,
            VerticalAlignment = VerticalAlignment.Center
        });
        _text = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        Children.Add(_text);
    }

    public string? Text { get => _text.Text; set => _text.Text = value; }
}
