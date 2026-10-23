using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Editing in <see cref="WeekCalendar"/> (notes B): drawing, moving and resizing in the grid, the quick create and details
/// popovers, delete with a one-level undo and its toast, the editing keys and their announcements. Edits write the
/// <see cref="WeekCalendarEvent"/> objects and add or remove them in <see cref="Events"/> (or a writable
/// <see cref="EventsSource"/> list), then raise <see cref="EventCreated"/>, <see cref="EventUpdated"/> or
/// <see cref="EventDeleted"/>.
/// </summary>
public partial class WeekCalendar
{
    public const string EditorLayerPart = "PART_EditorLayer";
    public const string FramePart = "PART_Frame";

    /// <summary>Set while <see cref="IsReadOnly"/> is true.</summary>
    public const string ReadOnlyPseudoClass = ":readonly";

    /// <summary>Set while an event is drawn, moved or resized.</summary>
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set while the quick create or details popover is open.</summary>
    public const string EditingPseudoClass = ":editing";

    /// <summary>Turns every edit off: no drawing, dragging, quick create, deleting or undo; the details show as text.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<WeekCalendar, bool>(nameof(IsReadOnly));

    /// <summary>The selected event (two-way): clicking, dragging or a keyboard move selects one; Escape clears it.</summary>
    public static readonly StyledProperty<WeekCalendarEvent?> SelectedEventProperty =
        AvaloniaProperty.Register<WeekCalendar, WeekCalendarEvent?>(nameof(SelectedEvent), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The calendar new events start in; null (or an unknown id) takes the first calendar shown.</summary>
    public static readonly StyledProperty<string?> DefaultCalendarIdProperty =
        AvaloniaProperty.Register<WeekCalendar, string?>(nameof(DefaultCalendarId));

    /// <summary>How long the undo toast stays (6 s); zero turns undo off.</summary>
    public static readonly StyledProperty<TimeSpan> UndoDurationProperty =
        AvaloniaProperty.Register<WeekCalendar, TimeSpan>(nameof(UndoDuration), TimeSpan.FromSeconds(6));

    /// <summary>True while the last change can be undone (its toast shows).</summary>
    public static readonly DirectProperty<WeekCalendar, bool> CanUndoProperty =
        AvaloniaProperty.RegisterDirect<WeekCalendar, bool>(nameof(CanUndo), o => o.CanUndo);

    private static readonly object s_keyboardSession = new();
    private WeekCalendarEditorLayer? _editorLayer;
    private Control? _frame;
    private WeekCalendarEditor? _editor;
    private WeekCalendarUndoToast? _toast;
    private WeekCalendarUndoEntry? _undo;
    private DispatcherTimer? _undoTimer;
    private bool _canUndo;
    private PointerPressedEventArgs? _consumedPress;
    private TopLevel? _pressTopLevel;
    private bool _lastInputKeyboard;

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <inheritdoc cref="SelectedEventProperty"/>
    public WeekCalendarEvent? SelectedEvent { get => GetValue(SelectedEventProperty); set => SetValue(SelectedEventProperty, value); }

    /// <inheritdoc cref="DefaultCalendarIdProperty"/>
    public string? DefaultCalendarId { get => GetValue(DefaultCalendarIdProperty); set => SetValue(DefaultCalendarIdProperty, value); }

    /// <inheritdoc cref="UndoDurationProperty"/>
    public TimeSpan UndoDuration { get => GetValue(UndoDurationProperty); set => SetValue(UndoDurationProperty, value); }

    /// <inheritdoc cref="CanUndoProperty"/>
    public bool CanUndo { get => _canUndo; private set => SetAndRaise(CanUndoProperty, ref _canUndo, value); }

    /// <summary>True unless <see cref="IsReadOnly"/>.</summary>
    internal bool IsEditable => !IsReadOnly;

    /// <summary>The open popover, if any.</summary>
    internal WeekCalendarEditor? Editor => _editor;

    /// <summary>The undo toast shown, if any.</summary>
    internal WeekCalendarUndoToast? Toast => _toast;

    /// <summary>The change undo would revert.</summary>
    internal WeekCalendarUndoEntry? UndoEntry => _undo;

    /// <summary>The layer of the popovers and the toast.</summary>
    internal WeekCalendarEditorLayer? EditorLayer => _editorLayer;

    /// <summary>True when the last input in the calendar was a key (focus comes back with a ring).</summary>
    internal bool LastInputWasKeyboard => _lastInputKeyboard;

    /// <summary>The calendar a new event starts in: <see cref="DefaultCalendarId"/>, else the first shown, else the first.</summary>
    internal string? NewEventCalendarId
    {
        get
        {
            FlushData();
            if (DefaultCalendarId is { } id && FindCalendar(id) is not null)
            {
                return id;
            }
            return _calendars.FirstOrDefault(c => c.IsVisible && !string.IsNullOrEmpty(c.Id))?.Id ?? _calendars.FirstOrDefault()?.Id;
        }
    }

    // Public methods ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Drafts a new event on <paramref name="day"/> and opens the quick create popover: at <paramref name="start"/>, or at
    /// the next free hour from 9 AM (the first hour shown when the day has none). Returns false when read only.
    /// </summary>
    public bool BeginCreate(DateOnly day, TimeOnly? start = null) => DraftAt(day, start, keyboard: true);

    /// <summary>Opens the details popover of <paramref name="calendarEvent"/>, moving to its days first.</summary>
    public bool OpenDetails(WeekCalendarEvent calendarEvent)
    {
        FlushData();
        if (PartFor(calendarEvent) is not { } part)
        {
            GoTo(DayOf(calendarEvent), animate: false);
            UpdateLayout();
            part = PartFor(calendarEvent);
        }
        return part is not null && OpenDetailsCore(calendarEvent, part, keyboard: true);
    }

    /// <summary>Closes the open popover: the quick create drops its draft; the details keep their title and place.</summary>
    public void CloseEditor() => CloseEditorCore(restoreFocus: true);

    /// <summary>Deletes <paramref name="calendarEvent"/> with an undo toast; false when read only.</summary>
    public bool DeleteEvent(WeekCalendarEvent calendarEvent) => DeleteEvent(calendarEvent, keyboard: false);

    /// <summary>Reverts the last create, move, resize, time, day or calendar change, or delete (one level, while its toast shows).</summary>
    public bool Undo()
    {
        if (_undo is not { } entry || !IsEditable)
        {
            return false;
        }
        ClearUndo();
        var e = entry.Event;
        ArmRelayout();
        switch (entry.Kind)
        {
            case WeekCalendarUndoKind.Created:
                RemoveFromData(e);
                if (ReferenceEquals(SelectedEvent, e))
                {
                    SelectEvent(null);
                }
                if (_editor is WeekCalendarDetails open && ReferenceEquals(open.Event, e))
                {
                    CloseEditorCore(restoreFocus: false, commit: false);
                }
                RaiseEvent(new WeekCalendarEventEventArgs(EventDeletedEvent, e));
                Announce(Labels.UndoneAnnouncement(entry.Description));
                break;
            case WeekCalendarUndoKind.Updated when entry.Previous is { } previous:
                var current = e.Clone();
                e.Start = previous.Start;
                e.End = previous.End;
                e.CalendarId = previous.CalendarId;
                e.IsAllDay = previous.IsAllDay;
                AfterDataWrite();
                // The swapped values: the event as it was before the undo is now the "previous" one.
                RaiseEvent(new WeekCalendarEventUpdatedEventArgs(EventUpdatedEvent, e, current));
                Announce(Labels.UndoneAnnouncement(entry.Description));
                RefreshEditorFor(e);
                break;
            case WeekCalendarUndoKind.Deleted:
                AddToData(e, entry.Index);
                RaiseEvent(new WeekCalendarEventEventArgs(EventCreatedEvent, e));
                Announce(Labels.RestoredAnnouncement(TitleOf(e)));
                break;
        }
        return true;
    }

    /// <summary>Cancels a drag in progress (the event springs back, a draft goes); false when none runs.</summary>
    public bool CancelDrag() => _gridView?.CancelGesture() == true;

    // Wiring --------------------------------------------------------------------------------------------------------------

    private void InitializeEditing()
    {
        AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnRootKeyDown, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnRootPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void ApplyEditingTemplate(INameScope scope)
    {
        CloseEditorCore(restoreFocus: false, commit: true, animate: false);
        RemoveToastNow();
        _editorLayer = scope.Find<WeekCalendarEditorLayer>(EditorLayerPart);
        _frame = scope.Find<Control>(FramePart);
        PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
    }

    private void OnEditingPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SelectedEventProperty)
        {
            _gridView?.SetSelected(SelectedEvent);
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
            if (IsReadOnly)
            {
                _gridView?.CancelGesture(announce: false);
                CloseEditorCore(restoreFocus: true, commit: false, animate: false);
                ClearUndo();
            }
            _gridView?.Refresh();
        }
        else if (change.Property == UndoDurationProperty && UndoDuration <= TimeSpan.Zero)
        {
            ClearUndo();
        }
    }

    private void OnEditingDetached()
    {
        CloseEditorCore(restoreFocus: false, commit: true, animate: false);
        ClearUndo();
        RemoveToastNow();
        HookOutsidePresses(false);
    }

    /// <summary>The days shown are about to change: a drag stops and a popover closes (its anchor is leaving).</summary>
    private void OnEditingRangeChanging()
    {
        if (_gridView?.IsDragging == true)
        {
            _gridView.CancelGesture(announce: false);
        }
        if (_editor is not null)
        {
            CloseEditorCore(restoreFocus: false);
        }
    }

    internal void SetDragging(bool dragging) => PseudoClasses.Set(DraggingPseudoClass, dragging);

    /// <summary>True for the press that closed a popover (it does nothing else).</summary>
    internal bool IsPressConsumed(PointerPressedEventArgs e) => ReferenceEquals(e, _consumedPress);

    internal void SelectEvent(WeekCalendarEvent? calendarEvent) => SetCurrentValue(SelectedEventProperty, calendarEvent);

    /// <summary>Escape on a selected event: clears the selection.</summary>
    internal void ClearSelection(bool announce)
    {
        if (SelectedEvent is null)
        {
            return;
        }
        SelectEvent(null);
        if (announce)
        {
            Announce(Labels.SelectionClearedAnnouncement);
        }
    }

    private void OnRootPreviewKeyDown(object? sender, KeyEventArgs e) => _lastInputKeyboard = true;

    private void OnRootKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Source is Visual source && source.FindAncestorOfType<TextBox>(includeSelf: true) is not null)
        {
            // Text boxes keep their keys (and their own undo).
            return;
        }
        var command = e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta;
        if (e.Key == Key.Z && command)
        {
            e.Handled = Undo();
            return;
        }
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (_gridView?.CancelGesture() == true)
            {
                e.Handled = true;
            }
            else if (_toast is not null && e.Source is Visual inToast && IsWithin(inToast, _toast))
            {
                DismissUndo(fromToast: true);
                e.Handled = true;
            }
            else if (_editor is not null)
            {
                CloseEditor();
                e.Handled = true;
            }
            return;
        }
        if (e.Key == Key.N && e.KeyModifiers == KeyModifiers.None && IsEditable &&
            !(_editor is not null && e.Source is Visual inEditor && IsWithin(inEditor, _editor)))
        {
            var visual = e.Source as Visual;
            var day = visual?.FindAncestorOfType<WeekCalendarDayCell>(true)?.Day ??
                      visual?.FindAncestorOfType<WeekCalendarEventPresenter>(true)?.Day ?? Date;
            e.Handled = DraftAt(day, null, keyboard: true);
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastInputKeyboard = false;
        if (_editor is not { IsClosing: false } editor || e.Source is not Visual source)
        {
            return;
        }
        if (IsWithin(source, editor) || _toast is not null && IsWithin(source, _toast))
        {
            return;
        }
        // A press outside only closes the popover (Arc): it draws, drags and opens nothing; the focus goes where it landed.
        var presenter = source.FindAncestorOfType<WeekCalendarEventPresenter>(includeSelf: true);
        var bar = source.FindAncestorOfType<WeekCalendarAllDayBar>(includeSelf: true);
        var cell = source.FindAncestorOfType<WeekCalendarDayCell>(includeSelf: true);
        CloseEditorCore(restoreFocus: false);
        if (presenter is not null)
        {
            presenter.Focus(NavigationMethod.Pointer);
        }
        else if (bar is not null)
        {
            bar.Focus(NavigationMethod.Pointer);
        }
        else if (cell is not null)
        {
            SetCursor(cell.Day, focusCell: true);
        }
        _consumedPress = e;
        e.Handled = true;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_editor is null || e.Source is not Visual source || IsWithin(source, this))
        {
            return;
        }
        CloseEditorCore(restoreFocus: false);
    }

    private void HookOutsidePresses(bool hook)
    {
        if (_pressTopLevel is not null)
        {
            _pressTopLevel.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
            _pressTopLevel = null;
        }
        if (hook && TopLevel.GetTopLevel(this) is { } top)
        {
            _pressTopLevel = top;
            top.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    /// <summary>True when <paramref name="source"/> is inside <paramref name="container"/> (also through popups it opened).</summary>
    private static bool IsWithin(Visual source, Visual container)
    {
        if (ReferenceEquals(source, container) || source.GetVisualAncestors().Any(a => ReferenceEquals(a, container)))
        {
            return true;
        }
        return source is ILogical logical && logical.GetLogicalAncestors().Any(a => ReferenceEquals(a, container));
    }

    // Popovers -----------------------------------------------------------------------------------------------------------

    private bool DraftAt(DateOnly day, TimeOnly? start, bool keyboard)
    {
        if (!IsEditable || _gridView is null || EffectiveView == WeekCalendarView.Agenda)
        {
            return false;
        }
        FlushData();
        if (!_range.Contains(day))
        {
            GoTo(day, animate: false);
        }
        var grid = TimeGrid;
        var step = WeekCalendarEditMetrics.SnapMinutes;
        var from = start is { } time
            ? Math.Clamp(WeekCalendarTimeGrid.SnapDown((int)time.ToTimeSpan().TotalMinutes), grid.StartMinute, grid.EndMinute - step)
            : WeekCalendarEditMath.FreeStart(Index.Timed(day).Select(s => (s.StartMinute, s.EndMinute)), grid);
        var to = Math.Min(grid.EndMinute, from + WeekCalendarEditMetrics.DraftMinutes);
        _gridView.ScrollRangeIntoView(from, to);
        if (_gridView.Current?.Grid is not { } pane)
        {
            return false;
        }
        return OpenQuickCreate(pane, day, from, to, keyboard);
    }

    /// <summary>Shows the draft of a new event and the quick create popover beside it.</summary>
    internal bool OpenQuickCreate(WeekCalendarGridPane pane, DateOnly day, int start, int end, bool keyboard)
    {
        CloseEditorCore(restoreFocus: false);
        if (!IsEditable || _editorLayer is null)
        {
            pane.HideDraft();
            return false;
        }
        var calendarId = NewEventCalendarId;
        pane.ShowDraft(day, start, end, calendarId);
        var editor = new WeekCalendarQuickCreate(this, pane, day, start, end, calendarId)
        {
            OpenedByKeyboard = keyboard,
            IsCompactPlacement = _compact,
        };
        editor.AnchorProvider = () => RectInLayer(pane, pane.DraftBox(day, start, end, pane.Bounds.Size));
        ShowEditor(editor);
        return true;
    }

    private bool OpenDetailsCore(WeekCalendarEvent calendarEvent, Control anchor, bool keyboard)
    {
        if (_editorLayer is null)
        {
            return false;
        }
        CloseEditorCore(restoreFocus: false);
        SelectEvent(calendarEvent);
        var editor = new WeekCalendarDetails(this, calendarEvent, IsReadOnly)
        {
            OpenedByKeyboard = keyboard,
            IsCompactPlacement = _compact,
        };
        editor.AnchorProvider = () => RectInLayer(anchor, new Rect(anchor.Bounds.Size));
        ShowEditor(editor);
        return true;
    }

    partial void OnEventInvoked(WeekCalendarEventEventArgs args, Control? source)
    {
        if (args.Handled || source is null)
        {
            return;
        }
        OpenDetailsCore(args.Event, source, _lastInputKeyboard);
    }

    private Rect RectInLayer(Visual visual, Rect rect)
    {
        var target = (Visual?)_editorLayer ?? this;
        var topLeft = visual.TranslatePoint(rect.TopLeft, target) ?? rect.TopLeft;
        return new Rect(topLeft, rect.Size);
    }

    private void ShowEditor(WeekCalendarEditor editor)
    {
        var layer = _editorLayer!;
        _editor = editor;
        editor.BottomProvider = () => _gridView?.VisibleBottomIn(layer) ?? double.PositiveInfinity;
        layer.Show(editor, editor.Place);
        PseudoClasses.Set(EditingPseudoClass, true);
        HookOutsidePresses(true);
        // Placed by the next layout pass (after any scroll into view), so the corner it grows from is known.
        UpdateLayout();
        editor.Enter();
        editor.TitleBox?.Focus(editor.OpenedByKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }

    private void CloseEditorCore(bool restoreFocus, bool commit = true, bool animate = true)
    {
        if (_editor is not { } editor || editor.IsClosing)
        {
            return;
        }
        _editor = null;
        PseudoClasses.Set(EditingPseudoClass, false);
        HookOutsidePresses(false);
        var hadFocus = editor.IsKeyboardFocusWithin;
        var method = editor.OpenedByKeyboard || _lastInputKeyboard ? NavigationMethod.Directional : NavigationMethod.Pointer;
        Control? target = null;
        switch (editor)
        {
            case WeekCalendarQuickCreate quick:
                // The draft goes in the frame the popover starts to leave (Arc).
                quick.Pane.HideDraft();
                target = _gridView?.Current?.Grid.CellFor(quick.Day);
                break;
            case WeekCalendarDetails details:
                if (commit)
                {
                    CommitDetailsTexts(details);
                }
                target = PartFor(details.Event) ?? _gridView?.Current?.Grid.CellFor(Date);
                break;
        }
        if (restoreFocus && hadFocus && target is not null)
        {
            if (!target.IsArrangeValid)
            {
                UpdateLayout();
            }
            target.Focus(method);
        }
        if (animate)
        {
            _ = RemoveEditorAsync(editor);
        }
        else
        {
            _editorLayer?.Hide(editor);
        }
    }

    private async Task RemoveEditorAsync(WeekCalendarEditor editor)
    {
        await editor.Exit();
        _editorLayer?.Hide(editor);
    }

    /// <summary>Save in the quick create popover (or Enter in its title): the draft becomes an event, selected and focused.</summary>
    internal void SaveQuickCreate(WeekCalendarQuickCreate quick)
    {
        if (!ReferenceEquals(_editor, quick) || quick.IsClosing || !IsEditable)
        {
            return;
        }
        var title = string.IsNullOrWhiteSpace(quick.Title) ? Labels.Untitled : quick.Title.Trim();
        var zone = Zone;
        var dayStart = quick.Day.ToDateTime(TimeOnly.MinValue);
        var created = new WeekCalendarEvent(Guid.NewGuid().ToString("N"), title, WeekCalendarTime.FromWall(dayStart.AddMinutes(quick.Start), zone),
            WeekCalendarTime.FromWall(dayStart.AddMinutes(quick.End), zone), quick.CalendarId);
        var method = quick.OpenedByKeyboard ? NavigationMethod.Directional : NavigationMethod.Pointer;
        CloseEditorCore(restoreFocus: false);
        ArmRelayout();
        var index = AddToData(created);
        SelectEvent(created);
        RaiseEvent(new WeekCalendarEventEventArgs(EventCreatedEvent, created));
        var culture = EffectiveCulture;
        var text = Labels.CreatedAnnouncement(title, WeekCalendarTime.DayName(quick.Day, culture), WeekCalendarTime.FullTime(quick.Start, culture),
            WeekCalendarTime.FullTime(quick.End, culture));
        Announce(text);
        PushUndo(WeekCalendarUndoKind.Created, created, null, index, text, null);
        FocusEvent(created, method);
    }

    /// <summary>A start, end or date picked in the details: the event moves at once (the popover stays where it is).</summary>
    internal void ApplyDetailsTimes(WeekCalendarDetails details, DateTime wallStart, DateTime wallEnd)
    {
        var e = details.Event;
        if (!IsEditable || e.IsAllDay)
        {
            return;
        }
        var zone = Zone;
        var start = WeekCalendarTime.FromWall(wallStart, zone, e.Start.Offset);
        var end = WeekCalendarTime.FromWall(wallEnd, zone, e.End.Offset);
        if (end <= start)
        {
            end = start.AddMinutes(WeekCalendarEditMetrics.SnapMinutes);
        }
        if (start == e.Start && end == e.End)
        {
            return;
        }
        var culture = EffectiveCulture;
        var startMinute = wallStart.TimeOfDay.TotalMinutes;
        var text = Labels.RescheduledAnnouncement(TitleOf(e), WeekCalendarTime.ShortDayName(DateOnly.FromDateTime(wallStart), culture),
            WeekCalendarTime.TimeRange(startMinute, startMinute + (end - start).TotalMinutes, culture));
        ApplyUpdate(e, ev =>
        {
            ev.Start = start;
            ev.End = end;
        }, text, details, undoable: true);
    }

    /// <summary>A calendar picked in the details.</summary>
    internal void ApplyDetailsCalendar(WeekCalendarDetails details, string calendarId)
    {
        var e = details.Event;
        if (!IsEditable || e.CalendarId == calendarId)
        {
            return;
        }
        var name = FindCalendar(calendarId)?.Name ?? calendarId;
        ApplyUpdate(e, ev => ev.CalendarId = calendarId, Labels.CalendarChangedAnnouncement(TitleOf(e), name), details, undoable: true,
            relayout: false);
    }

    /// <summary>The details closed: a new title or place is kept (raises <see cref="EventUpdated"/>; not undoable, as Arc).</summary>
    private void CommitDetailsTexts(WeekCalendarDetails details)
    {
        var e = details.Event;
        if (!IsEditable)
        {
            return;
        }
        var title = details.TitleText.Trim();
        if (!string.Equals(title, e.Title ?? string.Empty, StringComparison.Ordinal))
        {
            ApplyUpdate(e, ev => ev.Title = title, Labels.RenamedAnnouncement(TitleOf(title)), null, undoable: false, relayout: false);
        }
        if (details.LocationText is { } location && !string.Equals(location.Trim(), e.Location ?? string.Empty, StringComparison.Ordinal))
        {
            var place = location.Trim();
            ApplyUpdate(e, ev => ev.Location = place.Length == 0 ? null : place, null, null, undoable: false, relayout: false);
        }
    }

    private void RefreshEditorFor(WeekCalendarEvent calendarEvent)
    {
        if (_editor is WeekCalendarDetails details && ReferenceEquals(details.Event, calendarEvent) && !details.IsClosing)
        {
            details.Refresh();
        }
    }

    // Changes --------------------------------------------------------------------------------------------------------------

    /// <summary>A drag moved <paramref name="calendarEvent"/> to <paramref name="startMinute"/> on <paramref name="day"/>.</summary>
    internal void MoveEventTo(WeekCalendarEvent calendarEvent, DateOnly day, double startMinute, WeekCalendarEditSource source)
    {
        var e = calendarEvent;
        var duration = e.End > e.Start ? e.End - e.Start : TimeSpan.FromMinutes(WeekCalendarEditMetrics.SnapMinutes);
        var start = WeekCalendarTime.FromWall(day.ToDateTime(TimeOnly.MinValue).AddMinutes(startMinute), Zone, e.Start.Offset);
        var end = (start + duration).ToOffset(e.End.Offset);
        if (start == e.Start && end == e.End)
        {
            return;
        }
        var culture = EffectiveCulture;
        var text = Labels.MovedAnnouncement(TitleOf(e), WeekCalendarTime.ShortDayName(day, culture), WeekCalendarTime.FullTime(startMinute, culture));
        ApplyUpdate(e, ev =>
        {
            ev.Start = start;
            ev.End = end;
        }, text, null, undoable: true);
        SelectEvent(e);
        FocusEvent(e, NavigationMethod.Pointer);
    }

    /// <summary>A drag of the bottom edge ended <paramref name="calendarEvent"/> at <paramref name="endMinute"/> of <paramref name="day"/>.</summary>
    internal void ResizeEventTo(WeekCalendarEvent calendarEvent, DateOnly day, double endMinute, WeekCalendarEditSource source)
    {
        var e = calendarEvent;
        var end = WeekCalendarTime.FromWall(day.ToDateTime(TimeOnly.MinValue).AddMinutes(endMinute), Zone, e.End.Offset);
        if (end <= e.Start || end == e.End)
        {
            return;
        }
        var culture = EffectiveCulture;
        var startMinute = WeekCalendarTime.ToWall(e.Start, Zone).TimeOfDay.TotalMinutes;
        var text = Labels.ResizedAnnouncement(TitleOf(e), WeekCalendarTime.TimeRange(startMinute, endMinute, culture));
        ApplyUpdate(e, ev => ev.End = end, text, null, undoable: true);
        SelectEvent(e);
        FocusEvent(e, NavigationMethod.Pointer);
    }

    /// <summary>
    /// Alt+arrows: moves <paramref name="calendarEvent"/> by <paramref name="days"/> or <paramref name="minutes"/>, or (Alt+Shift)
    /// changes its end by <paramref name="endMinutes"/> (15 minutes at least), inside the hours shown. The event stays
    /// selected and focused; a run of these merges into one undo.
    /// </summary>
    internal bool NudgeEvent(WeekCalendarEvent calendarEvent, int days, int minutes, int endMinutes)
    {
        var e = calendarEvent;
        if (!IsEditable || e.IsAllDay)
        {
            return false;
        }
        var zone = Zone;
        var grid = TimeGrid;
        var wallStart = WeekCalendarTime.ToWall(e.Start, zone);
        var wallEnd = WeekCalendarTime.ToWall(e.End > e.Start ? e.End : e.Start.AddMinutes(WeekCalendarEditMetrics.SnapMinutes), zone);
        var day = DateOnly.FromDateTime(wallStart);
        var dayStart = day.ToDateTime(TimeOnly.MinValue);
        var startMinute = wallStart.TimeOfDay.TotalMinutes;
        var duration = (wallEnd - wallStart).TotalMinutes;
        DateTimeOffset start = e.Start;
        DateTimeOffset end = e.End;
        double newStart = startMinute;
        double newEnd = startMinute + duration;
        if (endMinutes != 0)
        {
            newEnd = Math.Max(startMinute + WeekCalendarEditMetrics.SnapMinutes, newEnd + endMinutes);
            newEnd = Math.Min(newEnd, Math.Max(startMinute + duration, grid.EndMinute));
            end = WeekCalendarTime.FromWall(dayStart.AddMinutes(newEnd), zone, e.End.Offset);
        }
        else
        {
            newStart = Math.Clamp(startMinute + minutes, Math.Min(startMinute, grid.StartMinute), Math.Max(startMinute, grid.EndMinute - duration));
            day = day.AddDays(days);
            dayStart = day.ToDateTime(TimeOnly.MinValue);
            start = WeekCalendarTime.FromWall(dayStart.AddMinutes(newStart), zone, e.Start.Offset);
            end = (start + (e.End > e.Start ? e.End - e.Start : TimeSpan.FromMinutes(WeekCalendarEditMetrics.SnapMinutes))).ToOffset(e.End.Offset);
            newEnd = newStart + duration;
        }
        if (start == e.Start && end == e.End)
        {
            // At the edge of the hours: the key is used, nothing changes.
            return true;
        }
        var culture = EffectiveCulture;
        var text = Labels.NudgedAnnouncement(TitleOf(e), WeekCalendarTime.ShortDayName(day, culture), WeekCalendarTime.FullTime(newStart, culture),
            WeekCalendarTime.FullTime(newEnd % (24 * 60), culture));
        ApplyUpdate(e, ev =>
        {
            ev.Start = start;
            ev.End = end;
        }, text, s_keyboardSession, undoable: true);
        SelectEvent(e);
        if (!_range.Contains(day))
        {
            GoTo(day);
        }
        FocusEvent(e, NavigationMethod.Directional);
        return true;
    }

    /// <summary>Deletes with an undo toast; the focus goes to the event's day.</summary>
    internal bool DeleteEvent(WeekCalendarEvent calendarEvent, bool keyboard)
    {
        var e = calendarEvent;
        if (!IsEditable)
        {
            return false;
        }
        FlushData();
        var focusWasHere = IsKeyboardFocusWithin;
        var day = DayOf(e);
        if (_editor is WeekCalendarDetails details && ReferenceEquals(details.Event, e))
        {
            CloseEditorCore(restoreFocus: false, commit: false);
        }
        if (_gridView?.IsDragging == true)
        {
            _gridView.CancelGesture(announce: false);
        }
        ArmRelayout();
        var index = RemoveFromData(e);
        if (ReferenceEquals(SelectedEvent, e))
        {
            SelectEvent(null);
        }
        RaiseEvent(new WeekCalendarEventEventArgs(EventDeletedEvent, e));
        var text = Labels.DeletedAnnouncement(TitleOf(e));
        Announce(text);
        PushUndo(WeekCalendarUndoKind.Deleted, e, null, index, text, null);
        if (focusWasHere && _range.Contains(day))
        {
            _gridView?.FocusDay(day, keyboard ? NavigationMethod.Directional : NavigationMethod.Pointer);
        }
        return true;
    }

    private void ApplyUpdate(WeekCalendarEvent e, Action<WeekCalendarEvent> change, string? announcement, object? session, bool undoable,
                             bool relayout = true)
    {
        var previous = e.Clone();
        if (relayout)
        {
            ArmRelayout();
        }
        change(e);
        AfterDataWrite();
        RaiseEvent(new WeekCalendarEventUpdatedEventArgs(EventUpdatedEvent, e, previous));
        if (announcement is not null)
        {
            Announce(announcement);
            if (undoable)
            {
                PushUndo(WeekCalendarUndoKind.Updated, e, previous, -1, announcement, session);
            }
        }
        RefreshEditorFor(e);
    }

    /// <summary>The next layout lets moved blocks settle on the relayout spring from where they are drawn.</summary>
    private void ArmRelayout()
    {
        if (_gridView is null)
        {
            return;
        }
        foreach (var slot in _gridView.Slots)
        {
            slot.Grid.Flip.Arm(MotionSpec.Spring(WeekCalendarMotion.Relayout));
        }
    }

    // Data -----------------------------------------------------------------------------------------------------------------

    /// <summary>The list edits add to and remove from: <see cref="Events"/>, or a writable <see cref="EventsSource"/> list.</summary>
    private IList? WritableEvents
    {
        get
        {
            if (EventsSource is null)
            {
                return Events;
            }
            return EventsSource is IList { IsReadOnly: false, IsFixedSize: false } list ? list : null;
        }
    }

    private int AddToData(WeekCalendarEvent e, int index = -1)
    {
        if (WritableEvents is not { } list)
        {
            return -1;
        }
        if (index >= 0 && index <= list.Count)
        {
            list.Insert(index, e);
        }
        else
        {
            index = list.Count;
            list.Add(e);
        }
        AfterDataWrite();
        return index;
    }

    private int RemoveFromData(WeekCalendarEvent e)
    {
        if (WritableEvents is not { } list)
        {
            return -1;
        }
        var index = list.IndexOf(e);
        if (index >= 0)
        {
            list.RemoveAt(index);
        }
        AfterDataWrite();
        return index;
    }

    /// <summary>Applies a write now (a list that does not raise changes is re-read), so the next layout sees it.</summary>
    private void AfterDataWrite()
    {
        if (EventsSource is not null and not INotifyCollectionChanged)
        {
            ScheduleRebuild();
        }
        FlushData();
    }

    // Undo -----------------------------------------------------------------------------------------------------------------

    private void PushUndo(WeekCalendarUndoKind kind, WeekCalendarEvent e, WeekCalendarEvent? previous, int index, string text, object? session)
    {
        if (UndoDuration <= TimeSpan.Zero)
        {
            ClearUndo();
            return;
        }
        if (kind == WeekCalendarUndoKind.Updated && session is not null && _undo is { Kind: WeekCalendarUndoKind.Updated } current &&
            ReferenceEquals(current.Session, session) && ReferenceEquals(current.Event, e))
        {
            // The same run (keyboard nudges, one details popover): undo still goes back to before its first change.
            current.Description = text;
            if (_toast is not null)
            {
                _toast.Message = text;
            }
            RestartUndoTimer();
            return;
        }
        _undo = new WeekCalendarUndoEntry(kind, e, previous, index, text, session);
        CanUndo = true;
        ShowToast(text);
        RestartUndoTimer();
    }

    private void RestartUndoTimer()
    {
        _undoTimer ??= new DispatcherTimer(DispatcherPriority.Normal);
        _undoTimer.Tick -= OnUndoTimer;
        _undoTimer.Tick += OnUndoTimer;
        _undoTimer.Stop();
        _undoTimer.Interval = UndoDuration;
        _undoTimer.Start();
    }

    private void OnUndoTimer(object? sender, EventArgs e) => ExpireUndo();

    /// <summary>The toast's time is up: the change can no longer be undone.</summary>
    internal void ExpireUndo() => ClearUndo();

    private void ClearUndo()
    {
        _undoTimer?.Stop();
        _undo = null;
        CanUndo = false;
        if (_toast is { } toast)
        {
            _toast = null;
            _ = RemoveToastAsync(toast);
        }
    }

    private void ShowToast(string text)
    {
        if (_editorLayer is not { } layer)
        {
            return;
        }
        if (_toast is { } old)
        {
            // A newer change replaces the toast: the old one leaves while the new one rises (a crossfade).
            _toast = null;
            _ = RemoveToastAsync(old);
        }
        var toast = new WeekCalendarUndoToast(this, text);
        if (_frame is { } frame)
        {
            toast.MaxWidth = Math.Max(120, frame.Bounds.Width - 32);
        }
        _toast = toast;
        layer.Show(toast, PlaceToast);
        toast.Enter();
    }

    private Point PlaceToast(Size size, Size bounds)
    {
        var frame = _frame is not null && _editorLayer is not null
            ? new Rect(_frame.TranslatePoint(default, _editorLayer) ?? default, _frame.Bounds.Size)
            : new Rect(bounds);
        return new Point(frame.X + (frame.Width - size.Width) / 2, frame.Bottom - WeekCalendarEditMetrics.ToastBottom - size.Height);
    }

    private async Task RemoveToastAsync(WeekCalendarUndoToast toast)
    {
        var hadFocus = toast.IsKeyboardFocusWithin;
        if (hadFocus)
        {
            // Arc drops the focus to the page here; ours goes back to the selected event or the focused day.
            RestoreFocusAfterToast();
        }
        await toast.Exit();
        _editorLayer?.Hide(toast);
    }

    private void RemoveToastNow()
    {
        if (_toast is { } toast)
        {
            _toast = null;
            _editorLayer?.Hide(toast);
        }
    }

    private void RestoreFocusAfterToast()
    {
        var method = _lastInputKeyboard ? NavigationMethod.Directional : NavigationMethod.Pointer;
        if (SelectedEvent is { } selected && _gridView?.Current?.Grid.PresenterFor(selected) is { } presenter)
        {
            presenter.Focus(method);
            return;
        }
        _gridView?.FocusDay(Date, method);
    }

    /// <summary>Undo on the toast.</summary>
    internal void UndoFromToast()
    {
        var hadFocus = _toast?.IsKeyboardFocusWithin == true;
        Undo();
        if (hadFocus)
        {
            RestoreFocusAfterToast();
        }
    }

    /// <summary>× on the toast (or Escape there): the change stays and can no longer be undone.</summary>
    internal void DismissUndo(bool fromToast)
    {
        var hadFocus = fromToast && _toast?.IsKeyboardFocusWithin == true;
        ClearUndo();
        if (hadFocus)
        {
            RestoreFocusAfterToast();
        }
    }

    // Helpers ----------------------------------------------------------------------------------------------------------------

    private string TitleOf(WeekCalendarEvent e) => TitleOf(e.Title);

    private string TitleOf(string? title) => string.IsNullOrWhiteSpace(title) ? Labels.Untitled : title;

    /// <summary>The day an event starts on, in the calendar's zone (an all-day event: its first date).</summary>
    internal DateOnly DayOf(WeekCalendarEvent e) =>
        e.IsAllDay ? DateOnly.FromDateTime(e.Start.DateTime) : DateOnly.FromDateTime(WeekCalendarTime.ToWall(e.Start, Zone));

    /// <summary>The part showing <paramref name="e"/> now: its block, its all-day bar or its agenda row.</summary>
    internal Control? PartFor(WeekCalendarEvent e)
    {
        if (_gridView is { IsVisible: true } grid && grid.Current is { } slot)
        {
            if (slot.Grid.PresenterFor(e) is { } presenter)
            {
                return presenter;
            }
            if (slot.AllDay.Bars.FirstOrDefault(b => ReferenceEquals(b.Event, e)) is { } bar)
            {
                return bar;
            }
        }
        if (_agendaView is { IsVisible: true } agenda)
        {
            return agenda.GetVisualDescendants().OfType<WeekCalendarAgendaRow>().FirstOrDefault(r => r.IsVisible && ReferenceEquals(r.Event, e));
        }
        return null;
    }

    /// <summary>Moves the focus to the block of <paramref name="e"/> in the current range.</summary>
    internal void FocusEvent(WeekCalendarEvent e, NavigationMethod method)
    {
        if (_gridView?.Current?.Grid.PresenterFor(e) is not { } presenter)
        {
            return;
        }
        if (!presenter.IsArrangeValid)
        {
            UpdateLayout();
        }
        presenter.Focus(method);
    }

    /// <summary>A swipe committed toward <paramref name="direction"/>: the range it dragged in becomes the one shown.</summary>
    internal void SwipeTo(int direction, WeekCalendarDays days)
    {
        var (range, cursor) = WeekCalendarRange.Step(EffectiveView, _range, Date, direction);
        Navigate(range.Start == days.Start ? range : days, cursor, WeekCalendarRangeChangeReason.Swipe, direction);
    }
}
