using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A complete calendar app screen around a month: a sidebar (Calendar, Upcoming and Invitations, the user's calendars with a
/// show or hide toggle and Only, teammates' calendars and an account menu), a month grid that packs multi-day events into
/// lanes, shows timed events as chips and folds the rest into "N more", and a docked day panel with the day's busy time,
/// all-day events, the schedule and the free gaps you can block as focus time. Events open details with replies, guests,
/// notes, a link to copy, a calendar picker and delete; a form adds events. Filter by calendar or by text, drag chips to
/// another day, drag across days to plan a span, and undo moves and deletes from the toast.
/// The screen fills its container (give it a height). From 1080 wide the day panel docks beside the month, below that it
/// is a sheet; below 900 the sidebar is a sheet; when the month is narrower than 560 it shows load meters and the selected
/// day's list under it.
/// <code>
/// &lt;arc:MonthCalendar Height="900" CurrentUserId="me" EventCreated="OnCreated" EventChanged="OnChanged" EventDeleted="OnDeleted" /&gt;
/// </code>
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
public partial class MonthCalendar : TemplatedControl
{
    public const string RootPart = "PART_Root";

    #region Properties

    /// <summary>The month shown (two-way): any date in it; it is kept as the month's first day.</summary>
    public static readonly StyledProperty<DateOnly> MonthProperty =
        AvaloniaProperty.Register<MonthCalendar, DateOnly>(nameof(Month), defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => new DateOnly(v.Year, v.Month, 1));

    /// <summary>The selected day (two-way): the day panel shows it. Picking a day, an event or "N more" changes it.</summary>
    public static readonly StyledProperty<DateOnly> SelectedDateProperty =
        AvaloniaProperty.Register<MonthCalendar, DateOnly>(nameof(SelectedDate), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The date treated as today; null follows the system clock (and midnight).</summary>
    public static readonly StyledProperty<DateOnly?> TodayProperty =
        AvaloniaProperty.Register<MonthCalendar, DateOnly?>(nameof(Today));

    /// <summary>The <see cref="MonthCalendarPerson.Id"/> of the signed-in person: their invitations get a reply control and their name fills the account row.</summary>
    public static readonly StyledProperty<string?> CurrentUserIdProperty =
        AvaloniaProperty.Register<MonthCalendar, string?>(nameof(CurrentUserId));

    /// <summary>The workspace name at the top of the sidebar.</summary>
    public static readonly StyledProperty<string> WorkspaceNameProperty =
        AvaloniaProperty.Register<MonthCalendar, string>(nameof(WorkspaceName), "Calendar");

    /// <summary>What the main area shows (two-way): the month, the upcoming list, the invitations or a teammate's month.</summary>
    public static readonly StyledProperty<MonthCalendarView> ViewProperty =
        AvaloniaProperty.Register<MonthCalendar, MonthCalendarView>(nameof(View), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The teammate whose month shows when <see cref="View"/> is <see cref="MonthCalendarView.Teammate"/> (two-way).</summary>
    public static readonly StyledProperty<string?> TeammateIdProperty =
        AvaloniaProperty.Register<MonthCalendar, string?>(nameof(TeammateId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The first day of the week (Monday by default; the account menu switches it between Monday and Sunday).</summary>
    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<MonthCalendar, DayOfWeek>(nameof(FirstDayOfWeek), DayOfWeek.Monday, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether Saturday and Sunday show as columns (two-way; the account menu switches it).</summary>
    public static readonly StyledProperty<bool> ShowWeekendsProperty =
        AvaloniaProperty.Register<MonthCalendar, bool>(nameof(ShowWeekends), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Month, weekday, date and time formats; null uses the current UI culture.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<MonthCalendar, CultureInfo?>(nameof(Culture));

    /// <summary>Whether the docked day panel shows (two-way; the header toggle changes it). Below 1080 wide the panel is a sheet that opens when a day or an event is picked.</summary>
    public static readonly StyledProperty<bool> IsDayPanelOpenProperty =
        AvaloniaProperty.Register<MonthCalendar, bool>(nameof(IsDayPanelOpen), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The docked day panel's width (two-way; 300–460, the grip on its edge changes it).</summary>
    public static readonly StyledProperty<double> DayPanelWidthProperty =
        AvaloniaProperty.Register<MonthCalendar, double>(nameof(DayPanelWidth), 340, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => Math.Clamp(double.IsNaN(v) ? 340 : v, ScreenShell.MinPanelWidth, ScreenShell.MaxPanelWidth));

    /// <summary>Whether the docked sidebar is collapsed to a rail of icons (two-way).</summary>
    public static readonly StyledProperty<bool> IsSidebarCollapsedProperty =
        AvaloniaProperty.Register<MonthCalendar, bool>(nameof(IsSidebarCollapsed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The event filter text (two-way): events whose title does not contain it dim.</summary>
    public static readonly StyledProperty<string> FilterTextProperty =
        AvaloniaProperty.Register<MonthCalendar, string>(nameof(FilterText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The start of the working hours: free gaps are looked for from here.</summary>
    public static readonly StyledProperty<TimeOnly> WorkDayStartProperty =
        AvaloniaProperty.Register<MonthCalendar, TimeOnly>(nameof(WorkDayStart), new TimeOnly(9, 0));

    /// <summary>The end of the working hours.</summary>
    public static readonly StyledProperty<TimeOnly> WorkDayEndProperty =
        AvaloniaProperty.Register<MonthCalendar, TimeOnly>(nameof(WorkDayEnd), new TimeOnly(18, 0));

    /// <summary>The shortest free gap the day panel offers to block (an hour).</summary>
    public static readonly StyledProperty<TimeSpan> MinimumFreeGapProperty =
        AvaloniaProperty.Register<MonthCalendar, TimeSpan>(nameof(MinimumFreeGap), TimeSpan.FromHours(1));

    /// <summary>Every word the screen shows or announces.</summary>
    public static readonly StyledProperty<MonthCalendarLabels> LabelsProperty =
        AvaloniaProperty.Register<MonthCalendar, MonthCalendarLabels>(nameof(Labels), new MonthCalendarLabels());

    #endregion

    #region Events

    /// <summary>Raised before the new event form, a focus block or Undo adds an event; set <see cref="MonthCalendarEventCreatingEventArgs.ErrorMessage"/> to refuse it.</summary>
    public static readonly RoutedEvent<MonthCalendarEventCreatingEventArgs> EventCreatingEvent =
        RoutedEvent.Register<MonthCalendar, MonthCalendarEventCreatingEventArgs>(nameof(EventCreating), RoutingStrategies.Bubble);

    /// <summary>Raised after an event was added: the form, Block focus time, or Undo of a delete (the same event comes back).</summary>
    public static readonly RoutedEvent<MonthCalendarEventEventArgs> EventCreatedEvent =
        RoutedEvent.Register<MonthCalendar, MonthCalendarEventEventArgs>(nameof(EventCreated), RoutingStrategies.Bubble);

    /// <summary>Raised after an event was moved (drag, Alt + arrows, Undo), answered or put on another calendar.</summary>
    public static readonly RoutedEvent<MonthCalendarEventChangedEventArgs> EventChangedEvent =
        RoutedEvent.Register<MonthCalendar, MonthCalendarEventChangedEventArgs>(nameof(EventChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after an event was deleted (the trash button, Delete, or Undo of a new event).</summary>
    public static readonly RoutedEvent<MonthCalendarEventEventArgs> EventDeletedEvent =
        RoutedEvent.Register<MonthCalendar, MonthCalendarEventEventArgs>(nameof(EventDeleted), RoutingStrategies.Bubble);

    /// <summary>Raised when the user answers an invitation (also raises <see cref="EventChanged"/>).</summary>
    public static readonly RoutedEvent<MonthCalendarRsvpChangedEventArgs> RsvpChangedEvent =
        RoutedEvent.Register<MonthCalendar, MonthCalendarRsvpChangedEventArgs>(nameof(RsvpChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the user opens an event (a chip, a row, an invitation), whether or not it was open.</summary>
    public static readonly RoutedEvent<MonthCalendarEventEventArgs> EventInvokedEvent =
        RoutedEvent.Register<MonthCalendar, MonthCalendarEventEventArgs>(nameof(EventInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="Month"/> changes (by the user or in code).</summary>
    public static readonly RoutedEvent<MonthCalendarDateChangedEventArgs> MonthChangedEvent =
        RoutedEvent.Register<MonthCalendar, MonthCalendarDateChangedEventArgs>(nameof(MonthChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SelectedDate"/> changes (by the user or in code).</summary>
    public static readonly RoutedEvent<MonthCalendarDateChangedEventArgs> SelectedDateChangedEvent =
        RoutedEvent.Register<MonthCalendar, MonthCalendarDateChangedEventArgs>(nameof(SelectedDateChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="EventCreatingEvent"/>
    public event EventHandler<MonthCalendarEventCreatingEventArgs>? EventCreating
    {
        add => AddHandler(EventCreatingEvent, value);
        remove => RemoveHandler(EventCreatingEvent, value);
    }

    /// <inheritdoc cref="EventCreatedEvent"/>
    public event EventHandler<MonthCalendarEventEventArgs>? EventCreated
    {
        add => AddHandler(EventCreatedEvent, value);
        remove => RemoveHandler(EventCreatedEvent, value);
    }

    /// <inheritdoc cref="EventChangedEvent"/>
    public event EventHandler<MonthCalendarEventChangedEventArgs>? EventChanged
    {
        add => AddHandler(EventChangedEvent, value);
        remove => RemoveHandler(EventChangedEvent, value);
    }

    /// <inheritdoc cref="EventDeletedEvent"/>
    public event EventHandler<MonthCalendarEventEventArgs>? EventDeleted
    {
        add => AddHandler(EventDeletedEvent, value);
        remove => RemoveHandler(EventDeletedEvent, value);
    }

    /// <inheritdoc cref="RsvpChangedEvent"/>
    public event EventHandler<MonthCalendarRsvpChangedEventArgs>? RsvpChanged
    {
        add => AddHandler(RsvpChangedEvent, value);
        remove => RemoveHandler(RsvpChangedEvent, value);
    }

    /// <inheritdoc cref="EventInvokedEvent"/>
    public event EventHandler<MonthCalendarEventEventArgs>? EventInvoked
    {
        add => AddHandler(EventInvokedEvent, value);
        remove => RemoveHandler(EventInvokedEvent, value);
    }

    /// <inheritdoc cref="MonthChangedEvent"/>
    public event EventHandler<MonthCalendarDateChangedEventArgs>? MonthChanged
    {
        add => AddHandler(MonthChangedEvent, value);
        remove => RemoveHandler(MonthChangedEvent, value);
    }

    /// <inheritdoc cref="SelectedDateChangedEvent"/>
    public event EventHandler<MonthCalendarDateChangedEventArgs>? SelectedDateChanged
    {
        add => AddHandler(SelectedDateChangedEvent, value);
        remove => RemoveHandler(SelectedDateChangedEvent, value);
    }

    #endregion

    /// <summary>The events (observed: adding, removing and changing them updates the screen).</summary>
    public AvaloniaList<MonthCalendarEvent> Events { get; } = new();

    /// <summary>The calendars, in sidebar order.</summary>
    public AvaloniaList<MonthCalendarSource> Calendars { get; } = new();

    /// <summary>The people events refer to; the first five teammates fill the sidebar.</summary>
    public AvaloniaList<MonthCalendarPerson> People { get; } = new();

    /// <inheritdoc cref="MonthProperty"/>
    public DateOnly Month { get => GetValue(MonthProperty); set => SetValue(MonthProperty, value); }

    /// <inheritdoc cref="SelectedDateProperty"/>
    public DateOnly SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }

    /// <inheritdoc cref="TodayProperty"/>
    public DateOnly? Today { get => GetValue(TodayProperty); set => SetValue(TodayProperty, value); }

    /// <inheritdoc cref="CurrentUserIdProperty"/>
    public string? CurrentUserId { get => GetValue(CurrentUserIdProperty); set => SetValue(CurrentUserIdProperty, value); }

    /// <inheritdoc cref="WorkspaceNameProperty"/>
    public string WorkspaceName { get => GetValue(WorkspaceNameProperty); set => SetValue(WorkspaceNameProperty, value); }

    /// <inheritdoc cref="ViewProperty"/>
    public MonthCalendarView View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    /// <inheritdoc cref="TeammateIdProperty"/>
    public string? TeammateId { get => GetValue(TeammateIdProperty); set => SetValue(TeammateIdProperty, value); }

    /// <inheritdoc cref="FirstDayOfWeekProperty"/>
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <inheritdoc cref="ShowWeekendsProperty"/>
    public bool ShowWeekends { get => GetValue(ShowWeekendsProperty); set => SetValue(ShowWeekendsProperty, value); }

    /// <inheritdoc cref="CultureProperty"/>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <inheritdoc cref="IsDayPanelOpenProperty"/>
    public bool IsDayPanelOpen { get => GetValue(IsDayPanelOpenProperty); set => SetValue(IsDayPanelOpenProperty, value); }

    /// <inheritdoc cref="DayPanelWidthProperty"/>
    public double DayPanelWidth { get => GetValue(DayPanelWidthProperty); set => SetValue(DayPanelWidthProperty, value); }

    /// <inheritdoc cref="IsSidebarCollapsedProperty"/>
    public bool IsSidebarCollapsed { get => GetValue(IsSidebarCollapsedProperty); set => SetValue(IsSidebarCollapsedProperty, value); }

    /// <inheritdoc cref="FilterTextProperty"/>
    public string FilterText { get => GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }

    /// <inheritdoc cref="WorkDayStartProperty"/>
    public TimeOnly WorkDayStart { get => GetValue(WorkDayStartProperty); set => SetValue(WorkDayStartProperty, value); }

    /// <inheritdoc cref="WorkDayEndProperty"/>
    public TimeOnly WorkDayEnd { get => GetValue(WorkDayEndProperty); set => SetValue(WorkDayEndProperty, value); }

    /// <inheritdoc cref="MinimumFreeGapProperty"/>
    public TimeSpan MinimumFreeGap { get => GetValue(MinimumFreeGapProperty); set => SetValue(MinimumFreeGapProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public MonthCalendarLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
}
