using System.Collections;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A desktop-class week planner: a toolbar with Today, previous / next and a view switch; a sidebar with a month and the
/// calendars to show (it folds into a Calendars popover on narrower calendars); a header of days, an all-day row and a
/// scrolling time grid with the events of each day side by side, the current time and the past dimmed. Week, work week and
/// day views; narrow calendars show three days, a day or an agenda. Changing the range slides the header, the all-day row
/// and the grid together on one spring; changing the view crossfades. Events are data (<see cref="Events"/> or
/// <see cref="EventsSource"/>) in absolute time, shown in <see cref="TimeZone"/>. Drag across empty time to draw an event
/// and name it in a quick create popover, drag events between days and times, drag their bottom edge to resize, click
/// one to edit its title, day, times and calendar; everything snaps to 15 minutes, and the last change can be undone.
/// </summary>
[TemplatePart(ToolbarPart, typeof(WeekCalendarToolbar))]
[TemplatePart(TodayPart, typeof(Button))]
[TemplatePart(PreviousPart, typeof(Button))]
[TemplatePart(NextPart, typeof(Button))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(ViewsPart, typeof(SegmentedControl))]
[TemplatePart(CalendarsPopoverPart, typeof(Popover))]
[TemplatePart(CalendarsButtonPart, typeof(Button))]
[TemplatePart(NewEventPart, typeof(Button))]
[TemplatePart(SidebarPart, typeof(Control))]
[TemplatePart(MiniMonthPart, typeof(WeekCalendarMiniMonth))]
[TemplatePart(SidebarListPart, typeof(WeekCalendarSourceList))]
[TemplatePart(PopoverListPart, typeof(WeekCalendarSourceList))]
[TemplatePart(MainPart, typeof(Panel))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[TemplatePart(FramePart, typeof(Control))]
[TemplatePart(EditorLayerPart, typeof(Panel))]
[PseudoClasses(WidePseudoClass, RegularPseudoClass, CompactPseudoClass, TightPseudoClass, SwatchOnlyPseudoClass,
    StackedPseudoClass, SidebarPseudoClass, SlidingPseudoClass, NoCalendarsPseudoClass, ReadOnlyPseudoClass, DraggingPseudoClass,
    EditingPseudoClass)]
public partial class WeekCalendar : TemplatedControl
{
    public const string ToolbarPart = "PART_Toolbar";
    public const string TodayPart = "PART_Today";
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string TitlePart = "PART_Title";
    public const string ViewsPart = "PART_Views";
    public const string CalendarsPopoverPart = "PART_CalendarsPopover";
    public const string CalendarsButtonPart = "PART_CalendarsButton";
    public const string NewEventPart = "PART_NewEvent";
    public const string SidebarPart = "PART_Sidebar";
    public const string MiniMonthPart = "PART_MiniMonth";
    public const string SidebarListPart = "PART_SidebarList";
    public const string PopoverListPart = "PART_PopoverList";
    public const string MainPart = "PART_Main";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set from <see cref="WeekCalendarMetrics.WideWidth"/> (1100): the sidebar shows.</summary>
    public const string WidePseudoClass = ":wide";

    /// <summary>Set between the compact and the wide width: week views without the sidebar, a Calendars button.</summary>
    public const string RegularPseudoClass = ":regular";

    /// <summary>Set below 640: three days, a day or the agenda.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set below 900: smaller header dates and title, the New event button shows only its plus.</summary>
    public const string TightPseudoClass = ":tight";

    /// <summary>Set below 760: the Calendars button shows only its swatches.</summary>
    public const string SwatchOnlyPseudoClass = ":swatch-only";

    /// <summary>Set below 680: the toolbar takes two rows with 40px controls.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the sidebar is shown.</summary>
    public const string SidebarPseudoClass = ":sidebar";

    /// <summary>Set while a range slides.</summary>
    public const string SlidingPseudoClass = ":sliding";

    /// <summary>Set when there are no calendars (no calendars list, no Calendars button).</summary>
    public const string NoCalendarsPseudoClass = ":no-calendars";

    /// <summary>The events. Use it in XAML; <see cref="EventsSource"/> takes precedence when set.</summary>
    public static readonly StyledProperty<AvaloniaList<WeekCalendarEvent>> EventsProperty =
        AvaloniaProperty.Register<WeekCalendar, AvaloniaList<WeekCalendarEvent>>(nameof(Events));

    /// <summary>A collection of <see cref="WeekCalendarEvent"/>s (observed when it raises collection changes); replaces <see cref="Events"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> EventsSourceProperty =
        AvaloniaProperty.Register<WeekCalendar, IEnumerable?>(nameof(EventsSource));

    /// <summary>The calendars listed in the sidebar and the Calendars popover; empty hides both.</summary>
    public static readonly StyledProperty<AvaloniaList<WeekCalendarSource>> CalendarsProperty =
        AvaloniaProperty.Register<WeekCalendar, AvaloniaList<WeekCalendarSource>>(nameof(Calendars));

    /// <summary>A collection of <see cref="WeekCalendarSource"/>s (observed); replaces <see cref="Calendars"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> CalendarsSourceProperty =
        AvaloniaProperty.Register<WeekCalendar, IEnumerable?>(nameof(CalendarsSource));

    /// <summary>
    /// The focused day (two-way; today by default). The days shown always hold it: setting a day outside them moves the
    /// calendar to the range around it.
    /// </summary>
    public static readonly StyledProperty<DateOnly> DateProperty =
        AvaloniaProperty.Register<WeekCalendar, DateOnly>(nameof(Date), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The chosen view (two-way). Narrow calendars show <see cref="EffectiveView"/> instead.</summary>
    public static readonly StyledProperty<WeekCalendarView> ViewProperty =
        AvaloniaProperty.Register<WeekCalendar, WeekCalendarView>(nameof(View), WeekCalendarView.Week, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The view shown: below 640 the week and work week show three days; from 640 the three-day and agenda views show the week.
    /// </summary>
    public static readonly DirectProperty<WeekCalendar, WeekCalendarView> EffectiveViewProperty =
        AvaloniaProperty.RegisterDirect<WeekCalendar, WeekCalendarView>(nameof(EffectiveView), o => o.EffectiveView);

    /// <summary>The first day shown.</summary>
    public static readonly DirectProperty<WeekCalendar, DateOnly> RangeStartProperty =
        AvaloniaProperty.RegisterDirect<WeekCalendar, DateOnly>(nameof(RangeStart), o => o.RangeStart);

    /// <summary>The last day shown.</summary>
    public static readonly DirectProperty<WeekCalendar, DateOnly> RangeEndProperty =
        AvaloniaProperty.RegisterDirect<WeekCalendar, DateOnly>(nameof(RangeEnd), o => o.RangeEnd);

    /// <summary>The title of the days shown ("Sep 21 to 27, 2026", "Thursday, November 5").</summary>
    public static readonly DirectProperty<WeekCalendar, string> RangeTitleProperty =
        AvaloniaProperty.RegisterDirect<WeekCalendar, string>(nameof(RangeTitle), o => o.RangeTitle);

    /// <summary>The first day of the week (Monday).</summary>
    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<WeekCalendar, DayOfWeek>(nameof(FirstDayOfWeek), DayOfWeek.Monday);

    /// <summary>The first hour of the grid (0–23); the gutter labels start one hour later.</summary>
    public static readonly StyledProperty<int> StartHourProperty =
        AvaloniaProperty.Register<WeekCalendar, int>(nameof(StartHour), 0);

    /// <summary>The hour the grid ends at (1–24, after <see cref="StartHour"/>).</summary>
    public static readonly StyledProperty<int> EndHourProperty =
        AvaloniaProperty.Register<WeekCalendar, int>(nameof(EndHour), 24);

    /// <summary>Pixels per hour (52, the same at every width).</summary>
    public static readonly StyledProperty<double> HourHeightProperty =
        AvaloniaProperty.Register<WeekCalendar, double>(nameof(HourHeight), WeekCalendarTimeGrid.DefaultHourHeight);

    /// <summary>The height of the grid (header, all-day row and scrolling hours); NaN takes 72% of the window, 480 to 760.</summary>
    public static readonly StyledProperty<double> GridHeightProperty =
        AvaloniaProperty.Register<WeekCalendar, double>(nameof(GridHeight), double.NaN);

    /// <summary>The zone the grid shows times in; null uses the computer's. The header's corner shows its offset ("GMT+8").</summary>
    public static readonly StyledProperty<TimeZoneInfo?> TimeZoneProperty =
        AvaloniaProperty.Register<WeekCalendar, TimeZoneInfo?>(nameof(TimeZone));

    /// <summary>The culture of dates, times and weekday names; null uses the current culture.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<WeekCalendar, CultureInfo?>(nameof(Culture));

    /// <summary>A fixed "now" for demos and tests; null follows the clock, moving the current time line every minute.</summary>
    public static readonly StyledProperty<DateTimeOffset?> NowProperty =
        AvaloniaProperty.Register<WeekCalendar, DateTimeOffset?>(nameof(Now));

    /// <summary>Shows the sidebar (true), hides it (false) or shows it from 1100 wide (null).</summary>
    public static readonly StyledProperty<bool?> IsSidebarVisibleProperty =
        AvaloniaProperty.Register<WeekCalendar, bool?>(nameof(IsSidebarVisible));

    /// <summary>Draws the current time line on today's range.</summary>
    public static readonly StyledProperty<bool> ShowNowIndicatorProperty =
        AvaloniaProperty.Register<WeekCalendar, bool>(nameof(ShowNowIndicator), true);

    /// <summary>Shades Saturday and Sunday.</summary>
    public static readonly StyledProperty<bool> ShowWeekendShadingProperty =
        AvaloniaProperty.Register<WeekCalendar, bool>(nameof(ShowWeekendShading), true);

    /// <summary>The hour the grid opens at; null opens at the first event's hour (8 AM without events).</summary>
    public static readonly StyledProperty<TimeOnly?> InitialScrollTimeProperty =
        AvaloniaProperty.Register<WeekCalendar, TimeOnly?>(nameof(InitialScrollTime));

    /// <summary>The all-day lanes shown before the row collapses into "+N more" chips (3); 0 never collapses.</summary>
    public static readonly StyledProperty<int> AllDayLaneLimitProperty =
        AvaloniaProperty.Register<WeekCalendar, int>(nameof(AllDayLaneLimit), WeekCalendarAllDayLayout.DefaultLaneLimit);

    /// <summary>Shows every all-day lane of a collapsed row (two-way; the chevron under "All day" toggles it).</summary>
    public static readonly StyledProperty<bool> IsAllDayExpandedProperty =
        AvaloniaProperty.Register<WeekCalendar, bool>(nameof(IsAllDayExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Every text the calendar shows or reads to screen readers.</summary>
    public static readonly StyledProperty<WeekCalendarLabels> LabelsProperty =
        AvaloniaProperty.Register<WeekCalendar, WeekCalendarLabels>(nameof(Labels), new WeekCalendarLabels());

    /// <summary>Runs when New event is pressed (parameter: the focused <see cref="DateOnly"/>), after <see cref="NewEventRequested"/>.</summary>
    public static readonly StyledProperty<ICommand?> NewEventCommandProperty =
        AvaloniaProperty.Register<WeekCalendar, ICommand?>(nameof(NewEventCommand));

    /// <summary>Runs when an event is clicked or activated with Enter (parameter: the <see cref="WeekCalendarEvent"/>).</summary>
    public static readonly StyledProperty<ICommand?> EventInvokedCommandProperty =
        AvaloniaProperty.Register<WeekCalendar, ICommand?>(nameof(EventInvokedCommand));

    /// <summary>Raised when <see cref="View"/> changes.</summary>
    public static readonly RoutedEvent<WeekCalendarViewChangedEventArgs> ViewChangedEvent =
        RoutedEvent.Register<WeekCalendar, WeekCalendarViewChangedEventArgs>(nameof(ViewChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the days shown change (previous / next, Today, a day in the month, the keys, a view change).</summary>
    public static readonly RoutedEvent<WeekCalendarRangeChangedEventArgs> RangeChangedEvent =
        RoutedEvent.Register<WeekCalendar, WeekCalendarRangeChangedEventArgs>(nameof(RangeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a calendar's toggle shows or hides its events.</summary>
    public static readonly RoutedEvent<WeekCalendarVisibilityChangedEventArgs> CalendarVisibilityChangedEvent =
        RoutedEvent.Register<WeekCalendar, WeekCalendarVisibilityChangedEventArgs>(nameof(CalendarVisibilityChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when an event is clicked or activated with Enter or Space; set Handled to keep its details closed.</summary>
    public static readonly RoutedEvent<WeekCalendarEventEventArgs> EventInvokedEvent =
        RoutedEvent.Register<WeekCalendar, WeekCalendarEventEventArgs>(nameof(EventInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when New event is pressed, with the focused day; set Handled to keep the calendar from drafting one at the day's next free hour.</summary>
    public static readonly RoutedEvent<WeekCalendarNewEventRequestedEventArgs> NewEventRequestedEvent =
        RoutedEvent.Register<WeekCalendar, WeekCalendarNewEventRequestedEventArgs>(nameof(NewEventRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when an event is created in the calendar (quick create), a deleted one is restored, or a delete is undone.</summary>
    public static readonly RoutedEvent<WeekCalendarEventEventArgs> EventCreatedEvent =
        RoutedEvent.Register<WeekCalendar, WeekCalendarEventEventArgs>(nameof(EventCreated), RoutingStrategies.Bubble);

    /// <summary>Raised when an event is moved, resized or edited in the calendar (or a change is undone), with a copy of it from before.</summary>
    public static readonly RoutedEvent<WeekCalendarEventUpdatedEventArgs> EventUpdatedEvent =
        RoutedEvent.Register<WeekCalendar, WeekCalendarEventUpdatedEventArgs>(nameof(EventUpdated), RoutingStrategies.Bubble);

    /// <summary>Raised when an event is deleted in the calendar (or its creation is undone).</summary>
    public static readonly RoutedEvent<WeekCalendarEventEventArgs> EventDeletedEvent =
        RoutedEvent.Register<WeekCalendar, WeekCalendarEventEventArgs>(nameof(EventDeleted), RoutingStrategies.Bubble);

    private WeekCalendarView _effectiveView = WeekCalendarView.Week;
    private DateOnly _rangeStart;
    private DateOnly _rangeEnd;
    private string _rangeTitle = string.Empty;

    static WeekCalendar()
    {
        FocusableProperty.OverrideDefaultValue<WeekCalendar>(false);
        StartHourProperty.Changed.AddClassHandler<WeekCalendar>((c, _) => c.OnHoursChanged());
        EndHourProperty.Changed.AddClassHandler<WeekCalendar>((c, _) => c.OnHoursChanged());
        HourHeightProperty.Changed.AddClassHandler<WeekCalendar>((c, _) => c.OnHoursChanged());
    }

    public WeekCalendar()
    {
        SetCurrentValue(EventsProperty, new AvaloniaList<WeekCalendarEvent>());
        SetCurrentValue(CalendarsProperty, new AvaloniaList<WeekCalendarSource>());
        Paint = new WeekCalendarPaint(this);
        InitializeState();
        InitializeEditing();
    }

    /// <inheritdoc cref="EventsProperty"/>
    [Content]
    public AvaloniaList<WeekCalendarEvent> Events { get => GetValue(EventsProperty); set => SetValue(EventsProperty, value); }

    /// <inheritdoc cref="EventsSourceProperty"/>
    public IEnumerable? EventsSource { get => GetValue(EventsSourceProperty); set => SetValue(EventsSourceProperty, value); }

    /// <inheritdoc cref="CalendarsProperty"/>
    public AvaloniaList<WeekCalendarSource> Calendars { get => GetValue(CalendarsProperty); set => SetValue(CalendarsProperty, value); }

    /// <inheritdoc cref="CalendarsSourceProperty"/>
    public IEnumerable? CalendarsSource { get => GetValue(CalendarsSourceProperty); set => SetValue(CalendarsSourceProperty, value); }

    /// <inheritdoc cref="DateProperty"/>
    public DateOnly Date { get => GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <inheritdoc cref="ViewProperty"/>
    public WeekCalendarView View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    /// <inheritdoc cref="EffectiveViewProperty"/>
    public WeekCalendarView EffectiveView { get => _effectiveView; private set => SetAndRaise(EffectiveViewProperty, ref _effectiveView, value); }

    /// <inheritdoc cref="RangeStartProperty"/>
    public DateOnly RangeStart { get => _rangeStart; private set => SetAndRaise(RangeStartProperty, ref _rangeStart, value); }

    /// <inheritdoc cref="RangeEndProperty"/>
    public DateOnly RangeEnd { get => _rangeEnd; private set => SetAndRaise(RangeEndProperty, ref _rangeEnd, value); }

    /// <inheritdoc cref="RangeTitleProperty"/>
    public string RangeTitle { get => _rangeTitle; private set => SetAndRaise(RangeTitleProperty, ref _rangeTitle, value); }

    /// <inheritdoc cref="FirstDayOfWeekProperty"/>
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <inheritdoc cref="StartHourProperty"/>
    public int StartHour { get => GetValue(StartHourProperty); set => SetValue(StartHourProperty, value); }

    /// <inheritdoc cref="EndHourProperty"/>
    public int EndHour { get => GetValue(EndHourProperty); set => SetValue(EndHourProperty, value); }

    /// <inheritdoc cref="HourHeightProperty"/>
    public double HourHeight { get => GetValue(HourHeightProperty); set => SetValue(HourHeightProperty, value); }

    /// <inheritdoc cref="GridHeightProperty"/>
    public double GridHeight { get => GetValue(GridHeightProperty); set => SetValue(GridHeightProperty, value); }

    /// <inheritdoc cref="TimeZoneProperty"/>
    public TimeZoneInfo? TimeZone { get => GetValue(TimeZoneProperty); set => SetValue(TimeZoneProperty, value); }

    /// <inheritdoc cref="CultureProperty"/>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <inheritdoc cref="NowProperty"/>
    public DateTimeOffset? Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <inheritdoc cref="IsSidebarVisibleProperty"/>
    public bool? IsSidebarVisible { get => GetValue(IsSidebarVisibleProperty); set => SetValue(IsSidebarVisibleProperty, value); }

    /// <inheritdoc cref="ShowNowIndicatorProperty"/>
    public bool ShowNowIndicator { get => GetValue(ShowNowIndicatorProperty); set => SetValue(ShowNowIndicatorProperty, value); }

    /// <inheritdoc cref="ShowWeekendShadingProperty"/>
    public bool ShowWeekendShading { get => GetValue(ShowWeekendShadingProperty); set => SetValue(ShowWeekendShadingProperty, value); }

    /// <inheritdoc cref="InitialScrollTimeProperty"/>
    public TimeOnly? InitialScrollTime { get => GetValue(InitialScrollTimeProperty); set => SetValue(InitialScrollTimeProperty, value); }

    /// <inheritdoc cref="AllDayLaneLimitProperty"/>
    public int AllDayLaneLimit { get => GetValue(AllDayLaneLimitProperty); set => SetValue(AllDayLaneLimitProperty, value); }

    /// <inheritdoc cref="IsAllDayExpandedProperty"/>
    public bool IsAllDayExpanded { get => GetValue(IsAllDayExpandedProperty); set => SetValue(IsAllDayExpandedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public WeekCalendarLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="NewEventCommandProperty"/>
    public ICommand? NewEventCommand { get => GetValue(NewEventCommandProperty); set => SetValue(NewEventCommandProperty, value); }

    /// <inheritdoc cref="EventInvokedCommandProperty"/>
    public ICommand? EventInvokedCommand { get => GetValue(EventInvokedCommandProperty); set => SetValue(EventInvokedCommandProperty, value); }

    /// <inheritdoc cref="ViewChangedEvent"/>
    public event EventHandler<WeekCalendarViewChangedEventArgs>? ViewChanged
    {
        add => AddHandler(ViewChangedEvent, value);
        remove => RemoveHandler(ViewChangedEvent, value);
    }

    /// <inheritdoc cref="RangeChangedEvent"/>
    public event EventHandler<WeekCalendarRangeChangedEventArgs>? RangeChanged
    {
        add => AddHandler(RangeChangedEvent, value);
        remove => RemoveHandler(RangeChangedEvent, value);
    }

    /// <inheritdoc cref="CalendarVisibilityChangedEvent"/>
    public event EventHandler<WeekCalendarVisibilityChangedEventArgs>? CalendarVisibilityChanged
    {
        add => AddHandler(CalendarVisibilityChangedEvent, value);
        remove => RemoveHandler(CalendarVisibilityChangedEvent, value);
    }

    /// <inheritdoc cref="EventInvokedEvent"/>
    public event EventHandler<WeekCalendarEventEventArgs>? EventInvoked
    {
        add => AddHandler(EventInvokedEvent, value);
        remove => RemoveHandler(EventInvokedEvent, value);
    }

    /// <inheritdoc cref="NewEventRequestedEvent"/>
    public event EventHandler<WeekCalendarNewEventRequestedEventArgs>? NewEventRequested
    {
        add => AddHandler(NewEventRequestedEvent, value);
        remove => RemoveHandler(NewEventRequestedEvent, value);
    }

    /// <inheritdoc cref="EventCreatedEvent"/>
    public event EventHandler<WeekCalendarEventEventArgs>? EventCreated
    {
        add => AddHandler(EventCreatedEvent, value);
        remove => RemoveHandler(EventCreatedEvent, value);
    }

    /// <inheritdoc cref="EventUpdatedEvent"/>
    public event EventHandler<WeekCalendarEventUpdatedEventArgs>? EventUpdated
    {
        add => AddHandler(EventUpdatedEvent, value);
        remove => RemoveHandler(EventUpdatedEvent, value);
    }

    /// <inheritdoc cref="EventDeletedEvent"/>
    public event EventHandler<WeekCalendarEventEventArgs>? EventDeleted
    {
        add => AddHandler(EventDeletedEvent, value);
        remove => RemoveHandler(EventDeletedEvent, value);
    }

    /// <summary>Brushes, typefaces and calendar colors of the current theme (internal parts read them while drawing).</summary>
    internal WeekCalendarPaint Paint { get; }
}
