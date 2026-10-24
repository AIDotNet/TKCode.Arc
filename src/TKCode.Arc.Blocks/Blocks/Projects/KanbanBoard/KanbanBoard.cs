using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A team board of status columns where cards lift, tilt with the pointer's speed and spring into the slot that opens
/// for them. Group it by person for swimlanes; columns can carry work-in-progress limits that update live while a card
/// is dragged; every column has quick add, and a card opens into a details drawer for its title, status, owner, notes
/// and checklist. Cards move with the mouse, pen, touch (after a short hold) or the keyboard (Space, arrows, Space), and
/// every pick up, move and drop is announced. The card list's order is the order inside every column: persist
/// <see cref="CardsChanged"/>'s snapshot to keep the board as the user left it.
/// </summary>
[TemplatePart(ToolbarPart, typeof(Border))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(TotalPart, typeof(Control))]
[TemplatePart(TotalDigitsPart, typeof(RollingDigits))]
[TemplatePart(TotalWordPart, typeof(TextBlock))]
[TemplatePart(ViewSwitchPart, typeof(KanbanSegmented))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(PanelPart, typeof(KanbanPanel))]
[TemplatePart(DragLayerPart, typeof(Canvas))]
[TemplatePart(ScrimPart, typeof(Border))]
[TemplatePart(DrawerPart, typeof(KanbanDrawer))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[TemplatePart(DragAnnouncerPart, typeof(ProjectsAnnouncer))]
[PseudoClasses(CompactPseudoClass, WrapPseudoClass, LanesPseudoClass, DetailsOpenPseudoClass, DraggingPseudoClass, ReadOnlyPseudoClass)]
public partial class KanbanBoard : TemplatedControl
{
    public const string ToolbarPart = "PART_Toolbar";
    public const string TitlePart = "PART_Title";
    public const string TotalPart = "PART_Total";
    public const string TotalDigitsPart = "PART_TotalDigits";
    public const string TotalWordPart = "PART_TotalWord";
    public const string ViewSwitchPart = "PART_ViewSwitch";
    public const string ScrollerPart = "PART_Scroller";
    public const string PanelPart = "PART_Panel";
    public const string DragLayerPart = "PART_DragLayer";
    public const string ScrimPart = "PART_Scrim";
    public const string DrawerPart = "PART_Drawer";
    public const string AnnouncerPart = "PART_Announcer";
    public const string DragAnnouncerPart = "PART_DragAnnouncer";

    /// <summary>Set at or below <see cref="CompactWidth"/>: fixed narrow columns, tighter paddings, sideways snapping.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set when the board is so narrow that the view switch moves under the title.</summary>
    public const string WrapPseudoClass = ":wrap";

    /// <summary>Set while the board is grouped by person (swimlanes).</summary>
    public const string LanesPseudoClass = ":lanes";

    /// <summary>Set while the details drawer is open.</summary>
    public const string DetailsOpenPseudoClass = ":details-open";

    /// <summary>Set while a card is lifted (pointer, touch or keyboard).</summary>
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set while <see cref="IsReadOnly"/> is true.</summary>
    public const string ReadOnlyPseudoClass = ":read-only";

    /// <summary>The heading over the board; null shows only the card count.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<KanbanBoard, string?>(nameof(Title));

    /// <summary>The status columns, left to right.</summary>
    public static readonly StyledProperty<AvaloniaList<KanbanColumn>> ColumnsProperty =
        AvaloniaProperty.Register<KanbanBoard, AvaloniaList<KanbanColumn>>(nameof(Columns));

    /// <summary>The people cards can belong to: avatars on cards, one swimlane each, and the owner menu.</summary>
    public static readonly StyledProperty<AvaloniaList<ProjectPerson>> PeopleProperty =
        AvaloniaProperty.Register<KanbanBoard, AvaloniaList<ProjectPerson>>(nameof(People));

    /// <summary>The labels cards can carry (the dot color and the name).</summary>
    public static readonly StyledProperty<AvaloniaList<KanbanLabel>> LabelsProperty =
        AvaloniaProperty.Register<KanbanBoard, AvaloniaList<KanbanLabel>>(nameof(Labels));

    /// <summary>The cards in board order (the order inside every column). Use it in XAML; <see cref="CardsSource"/> wins when set.</summary>
    public static readonly StyledProperty<AvaloniaList<KanbanCard>> CardsProperty =
        AvaloniaProperty.Register<KanbanBoard, AvaloniaList<KanbanCard>>(nameof(Cards));

    /// <summary>
    /// A collection of <see cref="KanbanCard"/>s that replaces <see cref="Cards"/> (observed when it raises collection
    /// changes). A writable list is reordered in place on every drop and quick add; a read-only one is left alone and the
    /// board keeps its own order (persist <see cref="CardsChanged"/>).
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> CardsSourceProperty =
        AvaloniaProperty.Register<KanbanBoard, IEnumerable?>(nameof(CardsSource));

    /// <summary>Status columns only, or one swimlane per person; switching flies every card to its new place.</summary>
    public static readonly StyledProperty<KanbanGroupBy> GroupByProperty =
        AvaloniaProperty.Register<KanbanBoard, KanbanGroupBy>(nameof(GroupBy), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The person new cards belong to (and whose swimlane quick add opens in); null uses the first person.</summary>
    public static readonly StyledProperty<ProjectPerson?> CurrentUserProperty =
        AvaloniaProperty.Register<KanbanBoard, ProjectPerson?>(nameof(CurrentUser));

    /// <summary>The card shown in the details drawer; set it to open the drawer, null to close it.</summary>
    public static readonly StyledProperty<KanbanCard?> OpenedCardProperty =
        AvaloniaProperty.Register<KanbanBoard, KanbanCard?>(nameof(OpenedCard), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows the "+" buttons and the Add card rows.</summary>
    public static readonly StyledProperty<bool> IsQuickAddEnabledProperty =
        AvaloniaProperty.Register<KanbanBoard, bool>(nameof(IsQuickAddEnabled), true);

    /// <summary>Opens the details drawer when a card is clicked or Enter is pressed on it (false only raises <see cref="CardInvoked"/>).</summary>
    public static readonly StyledProperty<bool> IsDetailsEnabledProperty =
        AvaloniaProperty.Register<KanbanBoard, bool>(nameof(IsDetailsEnabled), true);

    /// <summary>Lets cards be dragged (pointer, touch and keyboard).</summary>
    public static readonly StyledProperty<bool> IsDragEnabledProperty =
        AvaloniaProperty.Register<KanbanBoard, bool>(nameof(IsDragEnabled), true);

    /// <summary>No dragging, no quick add and no edits; the drawer still opens and shows the card.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<KanbanBoard, bool>(nameof(IsReadOnly));

    /// <summary>Adds an "Unassigned" entry to the owner menu (cards without an owner always get their own swimlane).</summary>
    public static readonly StyledProperty<bool> AllowUnassignedProperty =
        AvaloniaProperty.Register<KanbanBoard, bool>(nameof(AllowUnassigned));

    /// <summary>Makes the id of a card created with quick add; null continues the highest number ("FLD-244" → "FLD-245").</summary>
    public static readonly StyledProperty<Func<KanbanColumn, string>?> CardKeyFactoryProperty =
        AvaloniaProperty.Register<KanbanBoard, Func<KanbanColumn, string>?>(nameof(CardKeyFactory));

    /// <summary>How far a mouse or pen must move before a pressed card lifts (Arc: 5).</summary>
    public static readonly StyledProperty<double> DragThresholdProperty =
        AvaloniaProperty.Register<KanbanBoard, double>(nameof(DragThreshold), KanbanDragPhysics.Threshold);

    /// <summary>How long a touch must rest on a card before it lifts; moving earlier scrolls the board (Arc: 250ms).</summary>
    public static readonly StyledProperty<TimeSpan> TouchHoldDelayProperty =
        AvaloniaProperty.Register<KanbanBoard, TimeSpan>(nameof(TouchHoldDelay), KanbanDragPhysics.TouchHoldDelay);

    /// <summary>The narrowest a column gets on a wide board; narrower boards scroll sideways (Arc: 251).</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<KanbanBoard, double>(nameof(MinColumnWidth), 251);

    /// <summary>The column width of a compact board (Arc: 235).</summary>
    public static readonly StyledProperty<double> CompactColumnWidthProperty =
        AvaloniaProperty.Register<KanbanBoard, double>(nameof(CompactColumnWidth), 235);

    /// <summary>At this width and below the board is compact: narrow columns, tighter paddings, sideways snapping (Arc: 560).</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<KanbanBoard, double>(nameof(CompactWidth), 560);

    /// <summary>The widest the details drawer gets; it is never wider than the board minus 16 (Arc: 400).</summary>
    public static readonly StyledProperty<double> DrawerMaxWidthProperty =
        AvaloniaProperty.Register<KanbanBoard, double>(nameof(DrawerMaxWidth), 400);

    /// <summary>Every word the board shows or announces.</summary>
    public static readonly StyledProperty<KanbanBoardLabels> StringsProperty =
        AvaloniaProperty.Register<KanbanBoard, KanbanBoardLabels>(nameof(Strings));

    /// <summary>Runs when a card is clicked or Enter is pressed on it (parameter: the <see cref="KanbanCard"/>).</summary>
    public static readonly StyledProperty<ICommand?> CardInvokedCommandProperty =
        AvaloniaProperty.Register<KanbanBoard, ICommand?>(nameof(CardInvokedCommand));

    /// <summary>Runs after quick add created a card (parameter: the new <see cref="KanbanCard"/>).</summary>
    public static readonly StyledProperty<ICommand?> CardCreatedCommandProperty =
        AvaloniaProperty.Register<KanbanBoard, ICommand?>(nameof(CardCreatedCommand));

    /// <summary>Runs after a card was moved (parameter: the <see cref="KanbanCardMovedEventArgs"/>).</summary>
    public static readonly StyledProperty<ICommand?> CardMovedCommandProperty =
        AvaloniaProperty.Register<KanbanBoard, ICommand?>(nameof(CardMovedCommand));

    /// <summary>True while a card is lifted (pointer, touch or keyboard).</summary>
    public static readonly DirectProperty<KanbanBoard, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<KanbanBoard, bool>(nameof(IsDragging), o => o.IsDragging);

    /// <summary>The lifted card, or null.</summary>
    public static readonly DirectProperty<KanbanBoard, KanbanCard?> DraggedCardProperty =
        AvaloniaProperty.RegisterDirect<KanbanBoard, KanbanCard?>(nameof(DraggedCard), o => o.DraggedCard);

    /// <summary>True while the board is at or below <see cref="CompactWidth"/>.</summary>
    public static readonly DirectProperty<KanbanBoard, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<KanbanBoard, bool>(nameof(IsCompact), o => o.IsCompact);

    /// <summary>Raised after every move, edit, checklist change or new card, with the full card list in board order.</summary>
    public static readonly RoutedEvent<KanbanCardsChangedEventArgs> CardsChangedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardsChangedEventArgs>(nameof(CardsChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after a card moved (dropped, or its status or owner changed in the drawer).</summary>
    public static readonly RoutedEvent<KanbanCardMovedEventArgs> CardMovedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardMovedEventArgs>(nameof(CardMoved), RoutingStrategies.Bubble);

    /// <summary>Raised before quick add creates a card; change its key or title, or cancel it.</summary>
    public static readonly RoutedEvent<KanbanCardCreatingEventArgs> CardCreatingEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardCreatingEventArgs>(nameof(CardCreating), RoutingStrategies.Bubble);

    /// <summary>Raised after quick add created a card.</summary>
    public static readonly RoutedEvent<KanbanCardCreatedEventArgs> CardCreatedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardCreatedEventArgs>(nameof(CardCreated), RoutingStrategies.Bubble);

    /// <summary>Raised when a card is clicked or Enter is pressed on it; set Handled to keep the drawer closed.</summary>
    public static readonly RoutedEvent<KanbanCardEventArgs> CardInvokedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardEventArgs>(nameof(CardInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when the details drawer opened for a card.</summary>
    public static readonly RoutedEvent<KanbanCardEventArgs> DetailsOpenedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardEventArgs>(nameof(DetailsOpened), RoutingStrategies.Bubble);

    /// <summary>Raised when the details drawer closed.</summary>
    public static readonly RoutedEvent<KanbanCardEventArgs> DetailsClosedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardEventArgs>(nameof(DetailsClosed), RoutingStrategies.Bubble);

    /// <summary>Raised when the title or the notes of a card were edited in the drawer.</summary>
    public static readonly RoutedEvent<KanbanCardEditedEventArgs> CardEditedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardEditedEventArgs>(nameof(CardEdited), RoutingStrategies.Bubble);

    /// <summary>Raised when a checklist step was ticked or unticked in the drawer.</summary>
    public static readonly RoutedEvent<KanbanChecklistItemToggledEventArgs> ChecklistItemToggledEvent =
        RoutedEvent.Register<KanbanBoard, KanbanChecklistItemToggledEventArgs>(nameof(ChecklistItemToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when the grouping changed.</summary>
    public static readonly RoutedEvent<KanbanGroupByChangedEventArgs> GroupByChangedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanGroupByChangedEventArgs>(nameof(GroupByChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a column went over its limit or came back under it (committed changes, not drag previews).</summary>
    public static readonly RoutedEvent<KanbanColumnLimitEventArgs> ColumnLimitStateChangedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanColumnLimitEventArgs>(nameof(ColumnLimitStateChanged), RoutingStrategies.Bubble);

    /// <summary>Raised before a card lifts; set Cancel to keep it in place.</summary>
    public static readonly RoutedEvent<KanbanCardDragStartingEventArgs> CardDragStartingEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardDragStartingEventArgs>(nameof(CardDragStarting), RoutingStrategies.Bubble);

    /// <summary>Raised after a card lifted.</summary>
    public static readonly RoutedEvent<KanbanCardDragEventArgs> CardDragStartedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardDragEventArgs>(nameof(CardDragStarted), RoutingStrategies.Bubble);

    /// <summary>Raised when the place a lifted card would land in changed.</summary>
    public static readonly RoutedEvent<KanbanCardDragEventArgs> CardDragTargetChangedEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardDragEventArgs>(nameof(CardDragTargetChanged), RoutingStrategies.Bubble);

    /// <summary>Raised before a lifted card is dropped; set Cancel to send it back.</summary>
    public static readonly RoutedEvent<KanbanCardMovingEventArgs> CardMovingEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardMovingEventArgs>(nameof(CardMoving), RoutingStrategies.Bubble);

    /// <summary>Raised when a drag ended without a drop and the card is going back.</summary>
    public static readonly RoutedEvent<KanbanCardDragCancelledEventArgs> CardDragCancelledEvent =
        RoutedEvent.Register<KanbanBoard, KanbanCardDragCancelledEventArgs>(nameof(CardDragCancelled), RoutingStrategies.Bubble);

    private bool _isDragging;
    private KanbanCard? _draggedCard;
    private bool _isCompact;

    private Border? _toolbar;
    private TextBlock? _title;
    private Control? _total;
    private RollingDigits? _totalDigits;
    private TextBlock? _totalWord;
    private KanbanSegmented? _viewSwitch;
    private ScrollViewer? _scroller;
    private KanbanPanel? _panel;
    private Border? _scrim;
    private Canvas? _dragLayer;
    private KanbanDrawer? _drawer;
    private ProjectsAnnouncer? _announcer;
    private ProjectsAnnouncer? _dragAnnouncer;

    static KanbanBoard()
    {
        FocusableProperty.OverrideDefaultValue<KanbanBoard>(false);
    }

    public KanbanBoard()
    {
        SetCurrentValue(ColumnsProperty, new AvaloniaList<KanbanColumn>());
        SetCurrentValue(PeopleProperty, new AvaloniaList<ProjectPerson>());
        SetCurrentValue(LabelsProperty, new AvaloniaList<KanbanLabel>());
        SetCurrentValue(CardsProperty, new AvaloniaList<KanbanCard>());
        SetCurrentValue(StringsProperty, new KanbanBoardLabels());
        InitializeDrag();
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ColumnsProperty"/>
    public AvaloniaList<KanbanColumn> Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <inheritdoc cref="PeopleProperty"/>
    public AvaloniaList<ProjectPerson> People { get => GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AvaloniaList<KanbanLabel> Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="CardsProperty"/>
    [Content]
    public AvaloniaList<KanbanCard> Cards { get => GetValue(CardsProperty); set => SetValue(CardsProperty, value); }

    /// <inheritdoc cref="CardsSourceProperty"/>
    public IEnumerable? CardsSource { get => GetValue(CardsSourceProperty); set => SetValue(CardsSourceProperty, value); }

    /// <inheritdoc cref="GroupByProperty"/>
    public KanbanGroupBy GroupBy { get => GetValue(GroupByProperty); set => SetValue(GroupByProperty, value); }

    /// <inheritdoc cref="CurrentUserProperty"/>
    public ProjectPerson? CurrentUser { get => GetValue(CurrentUserProperty); set => SetValue(CurrentUserProperty, value); }

    /// <inheritdoc cref="OpenedCardProperty"/>
    public KanbanCard? OpenedCard { get => GetValue(OpenedCardProperty); set => SetValue(OpenedCardProperty, value); }

    /// <inheritdoc cref="IsQuickAddEnabledProperty"/>
    public bool IsQuickAddEnabled { get => GetValue(IsQuickAddEnabledProperty); set => SetValue(IsQuickAddEnabledProperty, value); }

    /// <inheritdoc cref="IsDetailsEnabledProperty"/>
    public bool IsDetailsEnabled { get => GetValue(IsDetailsEnabledProperty); set => SetValue(IsDetailsEnabledProperty, value); }

    /// <inheritdoc cref="IsDragEnabledProperty"/>
    public bool IsDragEnabled { get => GetValue(IsDragEnabledProperty); set => SetValue(IsDragEnabledProperty, value); }

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <inheritdoc cref="AllowUnassignedProperty"/>
    public bool AllowUnassigned { get => GetValue(AllowUnassignedProperty); set => SetValue(AllowUnassignedProperty, value); }

    /// <inheritdoc cref="CardKeyFactoryProperty"/>
    public Func<KanbanColumn, string>? CardKeyFactory { get => GetValue(CardKeyFactoryProperty); set => SetValue(CardKeyFactoryProperty, value); }

    /// <inheritdoc cref="DragThresholdProperty"/>
    public double DragThreshold { get => GetValue(DragThresholdProperty); set => SetValue(DragThresholdProperty, value); }

    /// <inheritdoc cref="TouchHoldDelayProperty"/>
    public TimeSpan TouchHoldDelay { get => GetValue(TouchHoldDelayProperty); set => SetValue(TouchHoldDelayProperty, value); }

    /// <inheritdoc cref="MinColumnWidthProperty"/>
    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    /// <inheritdoc cref="CompactColumnWidthProperty"/>
    public double CompactColumnWidth { get => GetValue(CompactColumnWidthProperty); set => SetValue(CompactColumnWidthProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="DrawerMaxWidthProperty"/>
    public double DrawerMaxWidth { get => GetValue(DrawerMaxWidthProperty); set => SetValue(DrawerMaxWidthProperty, value); }

    /// <inheritdoc cref="StringsProperty"/>
    public KanbanBoardLabels Strings { get => GetValue(StringsProperty); set => SetValue(StringsProperty, value); }

    /// <inheritdoc cref="CardInvokedCommandProperty"/>
    public ICommand? CardInvokedCommand { get => GetValue(CardInvokedCommandProperty); set => SetValue(CardInvokedCommandProperty, value); }

    /// <inheritdoc cref="CardCreatedCommandProperty"/>
    public ICommand? CardCreatedCommand { get => GetValue(CardCreatedCommandProperty); set => SetValue(CardCreatedCommandProperty, value); }

    /// <inheritdoc cref="CardMovedCommandProperty"/>
    public ICommand? CardMovedCommand { get => GetValue(CardMovedCommandProperty); set => SetValue(CardMovedCommandProperty, value); }

    /// <inheritdoc cref="IsDraggingProperty"/>
    public bool IsDragging { get => _isDragging; private set => SetAndRaise(IsDraggingProperty, ref _isDragging, value); }

    /// <inheritdoc cref="DraggedCardProperty"/>
    public KanbanCard? DraggedCard { get => _draggedCard; private set => SetAndRaise(DraggedCardProperty, ref _draggedCard, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    /// <inheritdoc cref="CardsChangedEvent"/>
    public event EventHandler<KanbanCardsChangedEventArgs>? CardsChanged
    {
        add => AddHandler(CardsChangedEvent, value);
        remove => RemoveHandler(CardsChangedEvent, value);
    }

    /// <inheritdoc cref="CardMovedEvent"/>
    public event EventHandler<KanbanCardMovedEventArgs>? CardMoved
    {
        add => AddHandler(CardMovedEvent, value);
        remove => RemoveHandler(CardMovedEvent, value);
    }

    /// <inheritdoc cref="CardCreatingEvent"/>
    public event EventHandler<KanbanCardCreatingEventArgs>? CardCreating
    {
        add => AddHandler(CardCreatingEvent, value);
        remove => RemoveHandler(CardCreatingEvent, value);
    }

    /// <inheritdoc cref="CardCreatedEvent"/>
    public event EventHandler<KanbanCardCreatedEventArgs>? CardCreated
    {
        add => AddHandler(CardCreatedEvent, value);
        remove => RemoveHandler(CardCreatedEvent, value);
    }

    /// <inheritdoc cref="CardInvokedEvent"/>
    public event EventHandler<KanbanCardEventArgs>? CardInvoked
    {
        add => AddHandler(CardInvokedEvent, value);
        remove => RemoveHandler(CardInvokedEvent, value);
    }

    /// <inheritdoc cref="DetailsOpenedEvent"/>
    public event EventHandler<KanbanCardEventArgs>? DetailsOpened
    {
        add => AddHandler(DetailsOpenedEvent, value);
        remove => RemoveHandler(DetailsOpenedEvent, value);
    }

    /// <inheritdoc cref="DetailsClosedEvent"/>
    public event EventHandler<KanbanCardEventArgs>? DetailsClosed
    {
        add => AddHandler(DetailsClosedEvent, value);
        remove => RemoveHandler(DetailsClosedEvent, value);
    }

    /// <inheritdoc cref="CardEditedEvent"/>
    public event EventHandler<KanbanCardEditedEventArgs>? CardEdited
    {
        add => AddHandler(CardEditedEvent, value);
        remove => RemoveHandler(CardEditedEvent, value);
    }

    /// <inheritdoc cref="ChecklistItemToggledEvent"/>
    public event EventHandler<KanbanChecklistItemToggledEventArgs>? ChecklistItemToggled
    {
        add => AddHandler(ChecklistItemToggledEvent, value);
        remove => RemoveHandler(ChecklistItemToggledEvent, value);
    }

    /// <inheritdoc cref="GroupByChangedEvent"/>
    public event EventHandler<KanbanGroupByChangedEventArgs>? GroupByChanged
    {
        add => AddHandler(GroupByChangedEvent, value);
        remove => RemoveHandler(GroupByChangedEvent, value);
    }

    /// <inheritdoc cref="ColumnLimitStateChangedEvent"/>
    public event EventHandler<KanbanColumnLimitEventArgs>? ColumnLimitStateChanged
    {
        add => AddHandler(ColumnLimitStateChangedEvent, value);
        remove => RemoveHandler(ColumnLimitStateChangedEvent, value);
    }

    /// <inheritdoc cref="CardDragStartingEvent"/>
    public event EventHandler<KanbanCardDragStartingEventArgs>? CardDragStarting
    {
        add => AddHandler(CardDragStartingEvent, value);
        remove => RemoveHandler(CardDragStartingEvent, value);
    }

    /// <inheritdoc cref="CardDragStartedEvent"/>
    public event EventHandler<KanbanCardDragEventArgs>? CardDragStarted
    {
        add => AddHandler(CardDragStartedEvent, value);
        remove => RemoveHandler(CardDragStartedEvent, value);
    }

    /// <inheritdoc cref="CardDragTargetChangedEvent"/>
    public event EventHandler<KanbanCardDragEventArgs>? CardDragTargetChanged
    {
        add => AddHandler(CardDragTargetChangedEvent, value);
        remove => RemoveHandler(CardDragTargetChangedEvent, value);
    }

    /// <inheritdoc cref="CardMovingEvent"/>
    public event EventHandler<KanbanCardMovingEventArgs>? CardMoving
    {
        add => AddHandler(CardMovingEvent, value);
        remove => RemoveHandler(CardMovingEvent, value);
    }

    /// <inheritdoc cref="CardDragCancelledEvent"/>
    public event EventHandler<KanbanCardDragCancelledEventArgs>? CardDragCancelled
    {
        add => AddHandler(CardDragCancelledEvent, value);
        remove => RemoveHandler(CardDragCancelledEvent, value);
    }

    internal KanbanPanel? Panel => _panel;

    internal ScrollViewer? Scroller => _scroller;

    internal KanbanDrawer? Drawer => _drawer;

    internal Canvas? DragLayer => _dragLayer;

    internal KanbanSegmented? ViewSwitch => _viewSwitch;

    internal ProjectsAnnouncer? Announcer => _announcer;

    internal ProjectsAnnouncer? DragAnnouncer => _dragAnnouncer;

    /// <summary>The current scroll position of the board (0 before the template is applied).</summary>
    internal Vector ScrollOffset => _scroller?.Offset ?? default;

    /// <summary>The size of the scroll viewport.</summary>
    internal Size Viewport
    {
        get
        {
            if (_scroller is null)
            {
                return default;
            }
            var viewport = _scroller.Viewport;
            return viewport.Width > 0 ? viewport : _scroller.Bounds.Size;
        }
    }

    // Template ---------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged -= OnViewSwitchChanged;
        }
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
            _scroller.PropertyChanged -= OnScrollerPropertyChanged;
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPressed;
        }
        DetachPanel();
        DetachDrawer();
        _toolbar = e.NameScope.Find<Border>(ToolbarPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _total = e.NameScope.Find<Control>(TotalPart);
        _totalDigits = e.NameScope.Find<RollingDigits>(TotalDigitsPart);
        _totalWord = e.NameScope.Find<TextBlock>(TotalWordPart);
        _viewSwitch = e.NameScope.Find<KanbanSegmented>(ViewSwitchPart);
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        _panel = e.NameScope.Find<KanbanPanel>(PanelPart);
        _scrim = e.NameScope.Find<Border>(ScrimPart);
        HideProxy();
        _dragLayer = e.NameScope.Find<Canvas>(DragLayerPart);
        _drawer = e.NameScope.Find<KanbanDrawer>(DrawerPart);
        _announcer = e.NameScope.Find<ProjectsAnnouncer>(AnnouncerPart);
        _dragAnnouncer = e.NameScope.Find<ProjectsAnnouncer>(DragAnnouncerPart);
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged += OnViewSwitchChanged;
        }
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
            _scroller.PropertyChanged += OnScrollerPropertyChanged;
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPressed;
        }
        AttachPanel();
        AttachDrawer();
        UpdateTitle();
        UpdateViewSwitch();
        RebuildNow(animate: false);
        UpdateBreakpoints(Bounds.Width);
    }

    private void OnScrollerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty)
        {
            _panel?.InvalidateMeasure();
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // Sticky head row and lane heads are arranged at the scroll offset; the lifted card's proxy follows the content.
        _panel?.InvalidateArrange();
        SyncProxy();
        OnScrolledDuringDrag();
        OnScrolledForSnap();
    }

    private void OnViewSwitchChanged(int index)
    {
        SetCurrentValue(GroupByProperty, index == 1 ? KanbanGroupBy.Assignee : KanbanGroupBy.Status);
    }

    private void UpdateViewSwitch()
    {
        if (_viewSwitch is null)
        {
            return;
        }
        var strings = Strings;
        _viewSwitch.SetOptions([strings.StatusView, strings.LanesView]);
        _viewSwitch.SelectedIndex = GroupBy == KanbanGroupBy.Assignee ? 1 : 0;
        AutomationProperties.SetName(_viewSwitch, strings.ViewSwitchName);
    }

    private void UpdateTitle()
    {
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrEmpty(Title);
        }
    }

    /// <summary>The total in the toolbar: a rolling number and the word after it.</summary>
    private void UpdateTotal()
    {
        var count = _grid?.Placed.Count ?? 0;
        if (_totalDigits is not null)
        {
            _totalDigits.Value = count;
        }
        if (_totalWord is not null)
        {
            _totalWord.Text = Strings.CardsWord(count);
        }
        if (_total is not null)
        {
            AutomationProperties.SetName(_total, Strings.CardCount(count));
        }
    }

    // Size classes -----------------------------------------------------------------------------------------------------

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
        _proxyOriginDirty = true;
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        var compact = width <= CompactWidth;
        var changed = compact != IsCompact;
        IsCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(WrapPseudoClass, width < 360);
        _drawer?.UpdateWidth(width);
        if (changed && compact)
        {
            // The compact board starts flush on its first column (the 12px padding stays out of view, as Arc).
            SnapAfterLayout(immediate: true);
        }
        _panel?.InvalidateMeasure();
    }

    // Property changes -------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == CardsProperty)
        {
            WatchCollections();
            if (CardsSource is null)
            {
                RebuildNow(animate: false);
            }
        }
        else if (property == CardsSourceProperty)
        {
            WatchCollections();
            RebuildNow(animate: false);
        }
        else if (property == ColumnsProperty || property == PeopleProperty || property == LabelsProperty)
        {
            WatchCollections();
            WatchStructure();
            ScheduleRebuild(animate: true);
        }
        else if (property == GroupByProperty)
        {
            OnGroupByChanged(change.GetOldValue<KanbanGroupBy>(), change.GetNewValue<KanbanGroupBy>());
        }
        else if (property == OpenedCardProperty)
        {
            OnOpenedCardChanged(change.GetOldValue<KanbanCard?>(), change.GetNewValue<KanbanCard?>());
        }
        else if (property == TitleProperty)
        {
            UpdateTitle();
        }
        else if (property == StringsProperty)
        {
            UpdateViewSwitch();
            UpdateTotal();
            _panel?.RefreshTexts();
            _drawer?.Refresh();
        }
        else if (property == IsQuickAddEnabledProperty || property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
            if (!CanQuickAdd)
            {
                CloseComposer(returnFocus: false, animate: false);
            }
            if (IsReadOnly)
            {
                CancelDrag();
            }
            _panel?.RefreshTexts();
            _drawer?.Refresh();
            _panel?.InvalidateMeasure();
        }
        else if (property == IsDragEnabledProperty && !IsDragEnabled)
        {
            CancelDrag();
        }
        else if (property == AllowUnassignedProperty || property == CurrentUserProperty)
        {
            _drawer?.Refresh();
        }
        else if (property == MinColumnWidthProperty || property == CompactColumnWidthProperty || property == CompactWidthProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
        else if (property == DrawerMaxWidthProperty)
        {
            _drawer?.UpdateWidth(Bounds.Width);
        }
        else if (property == TouchHoldDelayProperty)
        {
            _hold.Delay = TouchHoldDelay;
        }
    }

    private void OnGroupByChanged(KanbanGroupBy old, KanbanGroupBy value)
    {
        CancelDrag();
        CloseComposer(returnFocus: false, animate: false);
        PseudoClasses.Set(LanesPseudoClass, value == KanbanGroupBy.Assignee);
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectedIndex = value == KanbanGroupBy.Assignee ? 1 : 0;
        }
        // Every card flies from where it is drawn to its new cell on the layout spring (Arc: one shared layout flight).
        RebuildNow(animate: true);
        RaiseEvent(new KanbanGroupByChangedEventArgs(GroupByChangedEvent, old, value));
    }

    // Announcements ----------------------------------------------------------------------------------------------------

    /// <summary>Says <paramref name="text"/> politely (quick add, drawer moves, limits).</summary>
    internal void Announce(string text) => _announcer?.Announce(text);

    /// <summary>Says <paramref name="text"/> at once (drag and drop).</summary>
    internal void AnnounceDrag(string text) => _dragAnnouncer?.Announce(text);

    // Lookups ----------------------------------------------------------------------------------------------------------

    internal KanbanLabel? LabelFor(string? key)
    {
        if (key is null)
        {
            return null;
        }
        foreach (var label in Labels ?? [])
        {
            if (label.Identity == key)
            {
                return label;
            }
        }
        return null;
    }

    internal ProjectPerson? PersonFor(string? key)
    {
        if (key is null)
        {
            return null;
        }
        foreach (var person in People ?? [])
        {
            if (person.Identity == key)
            {
                return person;
            }
        }
        return null;
    }

    internal KanbanColumn? ColumnFor(string? key)
    {
        if (key is null)
        {
            return null;
        }
        foreach (var column in Columns ?? [])
        {
            if (column.Identity == key)
            {
                return column;
            }
        }
        return null;
    }

    /// <summary>The person new cards go to: <see cref="CurrentUser"/>, or the first person.</summary>
    internal ProjectPerson? EffectiveCurrentUser => CurrentUser ?? (People is { Count: > 0 } people ? people[0] : null);

    internal bool CanQuickAdd => IsQuickAddEnabled && !IsReadOnly;

    internal bool CanDrag => IsDragEnabled && !IsReadOnly;
}
