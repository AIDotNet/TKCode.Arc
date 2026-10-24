using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One collection of cards and six ways to look at it: a grid, a masonry wall, a list, a fanned stack, a 3D carousel and
/// a helix. Switching the view flies every card to its new place on its own spring, starting from the option you chose
/// and spreading outward; pointing at a card lifts it and shows it in the bottom panel, and selecting a card turns it
/// into a detail with a picture, a description, facts and an action. The carousel and helix turn by dragging,
/// flicking, the wheel or the keys and always settle on a card.
/// </summary>
[TemplatePart(StageHostPart, typeof(Panel))]
[TemplatePart(ViewBarPart, typeof(LayoutMorphViewBar))]
[PseudoClasses(NarrowPseudoClass, DensePseudoClass, OpenPseudoClass, DraggingPseudoClass, ScrollPseudoClass, FramedPseudoClass)]
public partial class LayoutMorph : TemplatedControl
{
    public const string StageHostPart = "PART_StageHost";
    public const string ViewBarPart = "PART_ViewBar";
    public const string NarrowPseudoClass = ":narrow";
    public const string DensePseudoClass = ":dense";
    public const string OpenPseudoClass = ":open";
    public const string DraggingPseudoClass = ":dragging";
    public const string ScrollPseudoClass = ":scroll";
    public const string FramedPseudoClass = ":framed";

    /// <summary>The cards, in order. Use it in XAML; <see cref="ItemsSource"/> takes precedence when set.</summary>
    public static readonly StyledProperty<AvaloniaList<LayoutMorphItem>> ItemsProperty =
        AvaloniaProperty.Register<LayoutMorph, AvaloniaList<LayoutMorphItem>>(nameof(Items));

    /// <summary>A collection of <see cref="LayoutMorphItem"/>s (observed when it raises collection changes); replaces <see cref="Items"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<LayoutMorph, IEnumerable?>(nameof(ItemsSource));

    /// <summary>The current layout; changing it flies the cards to the new one.</summary>
    public static readonly StyledProperty<LayoutMorphMode> LayoutProperty =
        AvaloniaProperty.Register<LayoutMorph, LayoutMorphMode>(nameof(Layout), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The layouts offered in the view switch, in this order; null offers all six.</summary>
    public static readonly StyledProperty<IList<LayoutMorphMode>?> LayoutsProperty =
        AvaloniaProperty.Register<LayoutMorph, IList<LayoutMorphMode>?>(nameof(Layouts));

    /// <summary>The item shown as an open detail; set it to open one, null to close.</summary>
    public static readonly StyledProperty<LayoutMorphItem?> OpenedItemProperty =
        AvaloniaProperty.Register<LayoutMorph, LayoutMorphItem?>(nameof(OpenedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The card in front of the carousel or the helix (null in the flat layouts).</summary>
    public static readonly DirectProperty<LayoutMorph, LayoutMorphItem?> FrontItemProperty =
        AvaloniaProperty.RegisterDirect<LayoutMorph, LayoutMorphItem?>(nameof(FrontItem), o => o.FrontItem);

    /// <summary>The large heading above the view switch; null leaves the heading out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LayoutMorph, string?>(nameof(Title));

    /// <summary>The sentence under the heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> LedeProperty =
        AvaloniaProperty.Register<LayoutMorph, string?>(nameof(Lede));

    /// <summary>The screen reader name of the view switch.</summary>
    public static readonly StyledProperty<string> ViewLabelProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(ViewLabel), "Gallery view");

    /// <summary>The name of the grid view.</summary>
    public static readonly StyledProperty<string> GridLabelProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(GridLabel), "Grid");

    /// <summary>The name of the masonry view.</summary>
    public static readonly StyledProperty<string> MasonryLabelProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(MasonryLabel), "Masonry");

    /// <summary>The name of the list view.</summary>
    public static readonly StyledProperty<string> ListLabelProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(ListLabel), "List");

    /// <summary>The name of the stack view.</summary>
    public static readonly StyledProperty<string> StackLabelProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(StackLabel), "Stack");

    /// <summary>The name of the carousel view.</summary>
    public static readonly StyledProperty<string> CarouselLabelProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(CarouselLabel), "Carousel");

    /// <summary>The name of the helix view.</summary>
    public static readonly StyledProperty<string> HelixLabelProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(HelixLabel), "Helix");

    /// <summary>The hint beside the view switch in the grid.</summary>
    public static readonly StyledProperty<string> GridHintProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(GridHint), "Select a card to open it");

    /// <summary>The hint in the masonry view.</summary>
    public static readonly StyledProperty<string> MasonryHintProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(MasonryHint), "Every card keeps its own shape");

    /// <summary>The hint in the list view.</summary>
    public static readonly StyledProperty<string> ListHintProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(ListHint), "Maker and price on every row");

    /// <summary>The hint in the stack view.</summary>
    public static readonly StyledProperty<string> StackHintProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(StackHint), "Hover the fan to lift one card");

    /// <summary>The hint in the carousel view.</summary>
    public static readonly StyledProperty<string> CarouselHintProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(CarouselHint), "Drag or swipe, it lands on the closest card");

    /// <summary>The hint in the helix view.</summary>
    public static readonly StyledProperty<string> HelixHintProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(HelixHint), "Drag to spin, flick to keep it turning");

    /// <summary>The panel's name while no card is pointed at, focused, in front or open; <see cref="Title"/> when null.</summary>
    public static readonly StyledProperty<string?> DefaultPanelTitleProperty =
        AvaloniaProperty.Register<LayoutMorph, string?>(nameof(DefaultPanelTitle));

    /// <summary>The line under the panel's default name.</summary>
    public static readonly StyledProperty<string?> DefaultPanelSubtitleProperty =
        AvaloniaProperty.Register<LayoutMorph, string?>(nameof(DefaultPanelSubtitle), "Point at a card for details");

    /// <summary>The panel's three statistics while no card is described (the collection's totals).</summary>
    public static readonly StyledProperty<AvaloniaList<LayoutMorphFact>> DefaultStatsProperty =
        AvaloniaProperty.Register<LayoutMorph, AvaloniaList<LayoutMorphFact>>(nameof(DefaultStats));

    /// <summary>Draws the stage and the panel as bordered, rounded surfaces (true); false lays them flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<LayoutMorph, bool>(nameof(IsFramed), true);

    /// <summary>Shows the bottom panel with the described card and its statistics.</summary>
    public static readonly StyledProperty<bool> IsPanelVisibleProperty =
        AvaloniaProperty.Register<LayoutMorph, bool>(nameof(IsPanelVisible), true);

    /// <summary>Shows the hint beside (narrow: under) the view switch.</summary>
    public static readonly StyledProperty<bool> IsHintVisibleProperty =
        AvaloniaProperty.Register<LayoutMorph, bool>(nameof(IsHintVisible), true);

    /// <summary>How long after a view change the farthest card starts (the nearest starts almost at once); zero starts all together.</summary>
    public static readonly StyledProperty<TimeSpan> StaggerDurationProperty =
        AvaloniaProperty.Register<LayoutMorph, TimeSpan>(nameof(StaggerDuration), LayoutMorphEngine.DefaultStagger);

    /// <summary>The stage height; NaN follows the width (600, 540 at 720 and below, 500 below 480, 460 below 360).</summary>
    public static readonly StyledProperty<double> StageHeightProperty =
        AvaloniaProperty.Register<LayoutMorph, double>(nameof(StageHeight), double.NaN);

    /// <summary>The close button's name; {0} is the open item's title.</summary>
    public static readonly StyledProperty<string> CloseTextFormatProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(CloseTextFormat), "Close {0}");

    /// <summary>Announced when a card opens; {0} is its title.</summary>
    public static readonly StyledProperty<string> OpenedFormatProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(OpenedFormat), "{0} opened");

    /// <summary>Announced when the view changes; {0} is the view's name.</summary>
    public static readonly StyledProperty<string> LayoutChangedFormatProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(LayoutChangedFormat), "{0} view");

    /// <summary>Announced when the detail's button turns on; {0} is the title, {1} the button's new text.</summary>
    public static readonly StyledProperty<string> ActionOnFormatProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(ActionOnFormat), "{0}: {1}");

    /// <summary>Announced when the detail's button turns off; {0} is the title, {1} the button's new text.</summary>
    public static readonly StyledProperty<string> ActionOffFormatProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(ActionOffFormat), "{0}: {1}");

    /// <summary>The stage's screen reader name: {0} the title, {1} the view, {2} the number of cards.</summary>
    public static readonly StyledProperty<string> StageLabelFormatProperty =
        AvaloniaProperty.Register<LayoutMorph, string>(nameof(StageLabelFormat),
            "{0} in {1} view: {2} cards. Left and right arrows change the view, up and down move between cards, Enter opens one.");

    /// <summary>Runs when a card is selected (parameter: its <see cref="LayoutMorphItem"/>), before it opens.</summary>
    public static readonly StyledProperty<ICommand?> OpenCommandProperty =
        AvaloniaProperty.Register<LayoutMorph, ICommand?>(nameof(OpenCommand));

    /// <summary>Runs when the detail's button is pressed (parameter: the item, whose <see cref="LayoutMorphItem.IsActive"/> has just flipped).</summary>
    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<LayoutMorph, ICommand?>(nameof(ActionCommand));

    /// <summary>Raised when the view changes.</summary>
    public static readonly RoutedEvent<LayoutMorphLayoutChangedEventArgs> LayoutChangedEvent =
        RoutedEvent.Register<LayoutMorph, LayoutMorphLayoutChangedEventArgs>(nameof(LayoutChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a card is selected (click, Enter, Space); set Handled to keep it from opening.</summary>
    public static readonly RoutedEvent<LayoutMorphItemEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<LayoutMorph, LayoutMorphItemEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a card has opened into its detail.</summary>
    public static readonly RoutedEvent<LayoutMorphItemEventArgs> ItemOpenedEvent =
        RoutedEvent.Register<LayoutMorph, LayoutMorphItemEventArgs>(nameof(ItemOpened), RoutingStrategies.Bubble);

    /// <summary>Raised when the open detail closes.</summary>
    public static readonly RoutedEvent<LayoutMorphItemEventArgs> ItemClosedEvent =
        RoutedEvent.Register<LayoutMorph, LayoutMorphItemEventArgs>(nameof(ItemClosed), RoutingStrategies.Bubble);

    /// <summary>Raised when the detail's button is pressed, after <see cref="LayoutMorphItem.IsActive"/> flipped.</summary>
    public static readonly RoutedEvent<LayoutMorphItemEventArgs> ItemActionInvokedEvent =
        RoutedEvent.Register<LayoutMorph, LayoutMorphItemEventArgs>(nameof(ItemActionInvoked), RoutingStrategies.Bubble);

    private static readonly LayoutMorphMode[] s_allLayouts =
    [
        LayoutMorphMode.Grid, LayoutMorphMode.Masonry, LayoutMorphMode.List,
        LayoutMorphMode.Stack, LayoutMorphMode.Carousel, LayoutMorphMode.Helix
    ];

    private readonly LayoutMorphStage _stage;
    private readonly List<LayoutMorphItem> _effective = new();
    private readonly HashSet<LayoutMorphItem> _watched = new();
    private readonly AvaloniaList<LayoutMorphItem> _ownItems = new();
    private AvaloniaList<LayoutMorphItem>? _watchedItems;
    private INotifyCollectionChanged? _observed;
    // The items and the lists a host hands in can outlive the morph (a view model's, a static sample list): the morph
    // follows them only while it is in a visual tree and reads them again when it comes back.
    private bool _watching;
    private bool _refreshCards;
    private LayoutMorphItem? _frontItem;
    private Panel? _stageHost;
    private LayoutMorphViewBar? _viewBar;
    private Point? _pendingOrigin;
    private NavigationMethod _openMethod = NavigationMethod.Unspecified;
    private bool _rebuildPending;
    private int _pendingFront = -1;

    static LayoutMorph()
    {
        FocusableProperty.OverrideDefaultValue<LayoutMorph>(false);
    }

    public LayoutMorph()
    {
        _stage = new LayoutMorphStage(this);
        SetCurrentValue(DefaultStatsProperty, new AvaloniaList<LayoutMorphFact>());
        // The morph's own list is followed always; a list set in its place only while attached (WatchItems).
        _ownItems.CollectionChanged += OnItemsChanged;
        SetCurrentValue(ItemsProperty, _ownItems);
        PseudoClasses.Set(FramedPseudoClass, true);
    }

    /// <inheritdoc cref="ItemsProperty"/>
    [Content]
    public AvaloniaList<LayoutMorphItem> Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="LayoutProperty"/>
    public LayoutMorphMode Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <inheritdoc cref="LayoutsProperty"/>
    public IList<LayoutMorphMode>? Layouts { get => GetValue(LayoutsProperty); set => SetValue(LayoutsProperty, value); }

    /// <inheritdoc cref="OpenedItemProperty"/>
    public LayoutMorphItem? OpenedItem { get => GetValue(OpenedItemProperty); set => SetValue(OpenedItemProperty, value); }

    /// <inheritdoc cref="FrontItemProperty"/>
    public LayoutMorphItem? FrontItem { get => _frontItem; private set => SetAndRaise(FrontItemProperty, ref _frontItem, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="LedeProperty"/>
    public string? Lede { get => GetValue(LedeProperty); set => SetValue(LedeProperty, value); }

    /// <inheritdoc cref="ViewLabelProperty"/>
    public string ViewLabel { get => GetValue(ViewLabelProperty); set => SetValue(ViewLabelProperty, value); }

    /// <inheritdoc cref="GridLabelProperty"/>
    public string GridLabel { get => GetValue(GridLabelProperty); set => SetValue(GridLabelProperty, value); }

    /// <inheritdoc cref="MasonryLabelProperty"/>
    public string MasonryLabel { get => GetValue(MasonryLabelProperty); set => SetValue(MasonryLabelProperty, value); }

    /// <inheritdoc cref="ListLabelProperty"/>
    public string ListLabel { get => GetValue(ListLabelProperty); set => SetValue(ListLabelProperty, value); }

    /// <inheritdoc cref="StackLabelProperty"/>
    public string StackLabel { get => GetValue(StackLabelProperty); set => SetValue(StackLabelProperty, value); }

    /// <inheritdoc cref="CarouselLabelProperty"/>
    public string CarouselLabel { get => GetValue(CarouselLabelProperty); set => SetValue(CarouselLabelProperty, value); }

    /// <inheritdoc cref="HelixLabelProperty"/>
    public string HelixLabel { get => GetValue(HelixLabelProperty); set => SetValue(HelixLabelProperty, value); }

    /// <inheritdoc cref="GridHintProperty"/>
    public string GridHint { get => GetValue(GridHintProperty); set => SetValue(GridHintProperty, value); }

    /// <inheritdoc cref="MasonryHintProperty"/>
    public string MasonryHint { get => GetValue(MasonryHintProperty); set => SetValue(MasonryHintProperty, value); }

    /// <inheritdoc cref="ListHintProperty"/>
    public string ListHint { get => GetValue(ListHintProperty); set => SetValue(ListHintProperty, value); }

    /// <inheritdoc cref="StackHintProperty"/>
    public string StackHint { get => GetValue(StackHintProperty); set => SetValue(StackHintProperty, value); }

    /// <inheritdoc cref="CarouselHintProperty"/>
    public string CarouselHint { get => GetValue(CarouselHintProperty); set => SetValue(CarouselHintProperty, value); }

    /// <inheritdoc cref="HelixHintProperty"/>
    public string HelixHint { get => GetValue(HelixHintProperty); set => SetValue(HelixHintProperty, value); }

    /// <inheritdoc cref="DefaultPanelTitleProperty"/>
    public string? DefaultPanelTitle { get => GetValue(DefaultPanelTitleProperty); set => SetValue(DefaultPanelTitleProperty, value); }

    /// <inheritdoc cref="DefaultPanelSubtitleProperty"/>
    public string? DefaultPanelSubtitle { get => GetValue(DefaultPanelSubtitleProperty); set => SetValue(DefaultPanelSubtitleProperty, value); }

    /// <inheritdoc cref="DefaultStatsProperty"/>
    public AvaloniaList<LayoutMorphFact> DefaultStats { get => GetValue(DefaultStatsProperty); set => SetValue(DefaultStatsProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="IsPanelVisibleProperty"/>
    public bool IsPanelVisible { get => GetValue(IsPanelVisibleProperty); set => SetValue(IsPanelVisibleProperty, value); }

    /// <inheritdoc cref="IsHintVisibleProperty"/>
    public bool IsHintVisible { get => GetValue(IsHintVisibleProperty); set => SetValue(IsHintVisibleProperty, value); }

    /// <inheritdoc cref="StaggerDurationProperty"/>
    public TimeSpan StaggerDuration { get => GetValue(StaggerDurationProperty); set => SetValue(StaggerDurationProperty, value); }

    /// <inheritdoc cref="StageHeightProperty"/>
    public double StageHeight { get => GetValue(StageHeightProperty); set => SetValue(StageHeightProperty, value); }

    /// <inheritdoc cref="CloseTextFormatProperty"/>
    public string CloseTextFormat { get => GetValue(CloseTextFormatProperty); set => SetValue(CloseTextFormatProperty, value); }

    /// <inheritdoc cref="OpenedFormatProperty"/>
    public string OpenedFormat { get => GetValue(OpenedFormatProperty); set => SetValue(OpenedFormatProperty, value); }

    /// <inheritdoc cref="LayoutChangedFormatProperty"/>
    public string LayoutChangedFormat { get => GetValue(LayoutChangedFormatProperty); set => SetValue(LayoutChangedFormatProperty, value); }

    /// <inheritdoc cref="ActionOnFormatProperty"/>
    public string ActionOnFormat { get => GetValue(ActionOnFormatProperty); set => SetValue(ActionOnFormatProperty, value); }

    /// <inheritdoc cref="ActionOffFormatProperty"/>
    public string ActionOffFormat { get => GetValue(ActionOffFormatProperty); set => SetValue(ActionOffFormatProperty, value); }

    /// <inheritdoc cref="StageLabelFormatProperty"/>
    public string StageLabelFormat { get => GetValue(StageLabelFormatProperty); set => SetValue(StageLabelFormatProperty, value); }

    /// <inheritdoc cref="OpenCommandProperty"/>
    public ICommand? OpenCommand { get => GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }

    /// <inheritdoc cref="ActionCommandProperty"/>
    public ICommand? ActionCommand { get => GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }

    /// <inheritdoc cref="LayoutChangedEvent"/>
    public event EventHandler<LayoutMorphLayoutChangedEventArgs>? LayoutChanged
    {
        add => AddHandler(LayoutChangedEvent, value);
        remove => RemoveHandler(LayoutChangedEvent, value);
    }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<LayoutMorphItemEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <inheritdoc cref="ItemOpenedEvent"/>
    public event EventHandler<LayoutMorphItemEventArgs>? ItemOpened
    {
        add => AddHandler(ItemOpenedEvent, value);
        remove => RemoveHandler(ItemOpenedEvent, value);
    }

    /// <inheritdoc cref="ItemClosedEvent"/>
    public event EventHandler<LayoutMorphItemEventArgs>? ItemClosed
    {
        add => AddHandler(ItemClosedEvent, value);
        remove => RemoveHandler(ItemClosedEvent, value);
    }

    /// <inheritdoc cref="ItemActionInvokedEvent"/>
    public event EventHandler<LayoutMorphItemEventArgs>? ItemActionInvoked
    {
        add => AddHandler(ItemActionInvokedEvent, value);
        remove => RemoveHandler(ItemActionInvokedEvent, value);
    }

    internal LayoutMorphStage Stage => _stage;

    internal IReadOnlyList<LayoutMorphItem> EffectiveItems
    {
        get
        {
            FlushItems();
            return _effective;
        }
    }

    internal IReadOnlyList<LayoutMorphMode> EffectiveLayouts =>
        Layouts is { Count: > 0 } layouts ? layouts.Distinct().ToList() : s_allLayouts;

    // Public methods -------------------------------------------------------------------------------------------------------

    /// <summary>Opens <paramref name="item"/> into its detail (the card morphs; the others dim).</summary>
    public void Open(LayoutMorphItem item) => SetCurrentValue(OpenedItemProperty, item);

    /// <summary>Closes the open detail; focus returns to its card.</summary>
    public void Close() => SetCurrentValue(OpenedItemProperty, null);

    /// <summary>
    /// Moves keyboard focus to <paramref name="item"/>'s card. The carousel and the helix turn it to the front (with
    /// <paramref name="animate"/>), a scrolling list scrolls to it; the other layouts only move the focus.
    /// </summary>
    public void FocusItem(LayoutMorphItem item, bool animate = true)
    {
        FlushItems();
        var index = _stage.IndexOfItem(item);
        if (index >= 0)
        {
            _stage.FocusCard(index, animate);
        }
    }

    /// <summary>
    /// Switches to <paramref name="layout"/>, staggering the cards from <paramref name="origin"/> (in this control's
    /// coordinates; null: the chosen view option's center).
    /// </summary>
    public void SetLayout(LayoutMorphMode layout, Point? origin = null)
    {
        _pendingOrigin = origin is { } p ? this.TranslatePoint(p, _stage) : null;
        if (Layout == layout)
        {
            _pendingOrigin = null;
            return;
        }
        SetCurrentValue(LayoutProperty, layout);
    }

    // Items --------------------------------------------------------------------------------------------------------------

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The own list stays followed after Items moved to another list: only the list on show counts.
        if (ItemsSource is null && ReferenceEquals(sender, Items))
        {
            ScheduleRebuild();
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRebuild();

    /// <summary>
    /// Collection changes are applied together, once per dispatcher turn: a Clear followed by Adds keeps every card whose
    /// item (or key) comes back, and the cards move once. Public methods apply pending changes first.
    /// </summary>
    private void ScheduleRebuild()
    {
        if (_rebuildPending)
        {
            return;
        }
        _rebuildPending = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildPending)
            {
                RebuildItems();
            }
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    /// <summary>Applies collection changes that are still waiting.</summary>
    internal void FlushItems()
    {
        if (_rebuildPending)
        {
            RebuildItems();
        }
    }

    private void RebuildItems()
    {
        _rebuildPending = false;
        _effective.Clear();
        IEnumerable source = ItemsSource ?? Items;
        foreach (var o in source)
        {
            if (o is LayoutMorphItem item)
            {
                _effective.Add(item);
            }
        }
        foreach (var item in _watched.Where(w => !_effective.Contains(w)).ToList())
        {
            item.PropertyChanged -= OnItemPropertyChanged;
            _watched.Remove(item);
        }
        if (_watching)
        {
            foreach (var item in _effective)
            {
                if (_watched.Add(item))
                {
                    item.PropertyChanged += OnItemPropertyChanged;
                }
            }
        }
        _stage.SetItems(_effective);
        if (OpenedItem is { } opened && !_effective.Contains(opened))
        {
            SetCurrentValue(OpenedItemProperty, null);
        }
        if (_refreshCards)
        {
            RefreshCards();
        }
        UpdateStageName();
        UpdateDefaultPanel();
    }

    /// <summary>The items' own changes made while the morph was away: every card, and the open detail, reads its item again.</summary>
    private void RefreshCards()
    {
        _refreshCards = false;
        foreach (var item in _effective)
        {
            // Any text property: the card drops its text, renames itself and repaints (sizes and pictures come with SetItems
            // and the stage's attach).
            _stage.OnItemChanged(item, LayoutMorphItem.TitleProperty);
        }
        if (OpenedItem is { } open)
        {
            UpdateDialog(open);
            UpdateAction(open, animate: false);
        }
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not LayoutMorphItem item)
        {
            return;
        }
        if (e.Property == LayoutMorphItem.IsActiveProperty)
        {
            if (ReferenceEquals(item, OpenedItem))
            {
                UpdateAction(item, animate: true);
            }
            return;
        }
        _stage.OnItemChanged(item, e.Property);
        if (ReferenceEquals(item, OpenedItem))
        {
            UpdateDialog(item);
        }
    }

    // Property changes ----------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ItemsProperty)
        {
            WatchItems();
            RebuildItems();
        }
        else if (property == ItemsSourceProperty)
        {
            WatchSource();
            RebuildItems();
        }
        else if (property == LayoutProperty)
        {
            OnLayoutPropertyChanged(change.GetOldValue<LayoutMorphMode>(), change.GetNewValue<LayoutMorphMode>());
        }
        else if (property == OpenedItemProperty)
        {
            OnOpenedItemChanged(change.GetOldValue<LayoutMorphItem?>(), change.GetNewValue<LayoutMorphItem?>());
        }
        else if (property == LayoutsProperty || property == GridLabelProperty || property == MasonryLabelProperty ||
                 property == ListLabelProperty || property == StackLabelProperty || property == CarouselLabelProperty ||
                 property == HelixLabelProperty)
        {
            UpdateViewOptions();
            UpdateStageName();
        }
        else if (property == GridHintProperty || property == MasonryHintProperty || property == ListHintProperty ||
                 property == StackHintProperty || property == CarouselHintProperty || property == HelixHintProperty)
        {
            UpdateHint();
        }
        else if (property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (property == StageHeightProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
        else if (property == TitleProperty || property == StageLabelFormatProperty)
        {
            UpdateStageName();
            UpdateDefaultPanel();
        }
        else if (property == DefaultPanelTitleProperty || property == DefaultPanelSubtitleProperty || property == DefaultStatsProperty)
        {
            UpdateDefaultPanel();
        }
        else if (property == CloseTextFormatProperty && OpenedItem is { } opened)
        {
            UpdateDialog(opened);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchItems();
        WatchSource();
        // Whatever changed while the morph was away: the next rebuild reads the lists, follows the items again and
        // refreshes every card.
        _refreshCards = true;
        ScheduleRebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchItems();
        WatchSource();
        foreach (var item in _watched)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
        _watched.Clear();
    }

    /// <summary>Follows a list set as <see cref="Items"/> in place of the morph's own (only while attached).</summary>
    private void WatchItems()
    {
        if (_watchedItems is not null)
        {
            _watchedItems.CollectionChanged -= OnItemsChanged;
        }
        _watchedItems = _watching && !ReferenceEquals(Items, _ownItems) ? Items : null;
        if (_watchedItems is not null)
        {
            _watchedItems.CollectionChanged += OnItemsChanged;
        }
    }

    private void WatchSource()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourceChanged;
        }
        _observed = _watching ? ItemsSource as INotifyCollectionChanged : null;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnLayoutPropertyChanged(LayoutMorphMode old, LayoutMorphMode layout)
    {
        var origin = _pendingOrigin ?? OptionCenter(layout);
        var front = _pendingFront;
        _pendingOrigin = null;
        _pendingFront = -1;
        if (_viewBar is not null)
        {
            _viewBar.SelectedMode = layout;
        }
        if (OpenedItem is not null)
        {
            Close();
        }
        _stage.SetLayout(layout, origin, StaggerDuration.TotalSeconds, front);
        PseudoClasses.Set(ScrollPseudoClass, _stage.IsScrollable);
        UpdateHint();
        UpdateStageName();
        Announce(string.Format(LayoutChangedFormat, LabelOf(layout)));
        RaiseEvent(new LayoutMorphLayoutChangedEventArgs(LayoutChangedEvent, old, layout));
    }

    /// <summary>The center of the view option for <paramref name="layout"/> in stage coordinates (the stagger's origin).</summary>
    private Point? OptionCenter(LayoutMorphMode layout)
    {
        if (_viewBar?.OptionFor(layout) is { } option && option.Bounds.Width > 0)
        {
            return option.TranslatePoint(new Point(option.Bounds.Width / 2, option.Bounds.Height / 2), _stage);
        }
        return null;
    }

    /// <summary>The arrow keys on a card: the previous or next view, staggered from that card, which keeps focus.</summary>
    internal void StepLayout(int direction, LayoutMorphCard card)
    {
        var layouts = EffectiveLayouts;
        if (layouts.Count == 0)
        {
            return;
        }
        var current = layouts.ToList().IndexOf(Layout);
        var next = layouts[((current < 0 ? 0 : current) + direction + layouts.Count) % layouts.Count];
        if (next == Layout)
        {
            return;
        }
        _pendingOrigin = card.ScreenCenter;
        _pendingFront = next is LayoutMorphMode.Carousel or LayoutMorphMode.Helix ? card.Index : -1;
        SetCurrentValue(LayoutProperty, next);
        card.Focus(NavigationMethod.Directional);
    }

    // Template -------------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        FlushItems();
        if (_stageHost is not null)
        {
            _stageHost.Children.Remove(_stage);
        }
        if (_viewBar is not null)
        {
            _viewBar.Chosen -= OnViewChosen;
        }
        _stageHost = e.NameScope.Find<Panel>(StageHostPart);
        _stageHost?.Children.Insert(0, _stage);
        _viewBar = e.NameScope.Find<LayoutMorphViewBar>(ViewBarPart);
        if (_viewBar is not null)
        {
            _viewBar.Chosen += OnViewChosen;
            _viewBar.SelectedMode = Layout;
        }
        ApplyPanelTemplate(e.NameScope);
        ApplyDialogTemplate(e.NameScope);
        UpdateViewOptions();
        UpdateHint();
        UpdateStageName();
        UpdateDefaultPanel();
        OnSubjectChanged(_subject, force: true);
        UpdateBreakpoints(Bounds.Width);
    }

    private void OnViewChosen(LayoutMorphMode mode, LayoutMorphViewOption option)
    {
        _pendingOrigin = option.TranslatePoint(new Point(option.Bounds.Width / 2, option.Bounds.Height / 2), _stage);
        SetCurrentValue(LayoutProperty, mode);
    }

    private void UpdateViewOptions()
    {
        if (_viewBar is null)
        {
            return;
        }
        _viewBar.SetOptions(EffectiveLayouts.Select(mode => (mode, LabelOf(mode), IconOf(mode))).ToList());
        Avalonia.Automation.AutomationProperties.SetName(_viewBar, ViewLabel);
    }

    internal string LabelOf(LayoutMorphMode mode) => mode switch
    {
        LayoutMorphMode.Masonry => MasonryLabel,
        LayoutMorphMode.List => ListLabel,
        LayoutMorphMode.Stack => StackLabel,
        LayoutMorphMode.Carousel => CarouselLabel,
        LayoutMorphMode.Helix => HelixLabel,
        _ => GridLabel
    };

    internal string HintOf(LayoutMorphMode mode) => mode switch
    {
        LayoutMorphMode.Masonry => MasonryHint,
        LayoutMorphMode.List => ListHint,
        LayoutMorphMode.Stack => StackHint,
        LayoutMorphMode.Carousel => CarouselHint,
        LayoutMorphMode.Helix => HelixHint,
        _ => GridHint
    };

    private static Geometry IconOf(LayoutMorphMode mode) => mode switch
    {
        LayoutMorphMode.Masonry => ShowpieceIcons.LayoutDashboard,
        LayoutMorphMode.List => ArcIcons.List,
        LayoutMorphMode.Stack => ArcIcons.Layers,
        LayoutMorphMode.Carousel => ShowpieceIcons.GalleryHorizontalEnd,
        LayoutMorphMode.Helix => ShowpieceIcons.Dna,
        _ => ArcIcons.LayoutGrid
    };

    internal string StageSummary() =>
        string.Format(StageLabelFormat, Title ?? DefaultPanelTitle ?? string.Empty, LabelOf(Layout), _effective.Count);

    private void UpdateStageName() => Avalonia.Automation.AutomationProperties.SetName(_stage, StageSummary());

    // Size classes ------------------------------------------------------------------------------------------------------

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        var narrow = width < LayoutMorphEngine.NarrowWidth;
        var dense = width < LayoutMorphEngine.DenseWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(DensePseudoClass, dense);
        if (_viewBar is not null)
        {
            _viewBar.IsCompact = narrow;
        }
        var height = double.IsNaN(StageHeight) ? LayoutMorphEngine.StageHeightFor(width) : Math.Max(120, StageHeight);
        if (_stageHost is not null && (double.IsNaN(_stageHost.Height) || Math.Abs(_stageHost.Height - height) > 0.01))
        {
            _stageHost.Height = height;
        }
        ArrangePanel(narrow);
    }

    // Events from the stage -----------------------------------------------------------------------------------------------

    internal void InvokeItem(LayoutMorphItem item, NavigationMethod method = NavigationMethod.Unspecified)
    {
        _openMethod = method;
        var args = new LayoutMorphItemEventArgs(ItemInvokedEvent, item);
        RaiseEvent(args);
        if (OpenCommand is { } command && command.CanExecute(item))
        {
            command.Execute(item);
        }
        if (!args.Handled)
        {
            Open(item);
        }
    }

    internal void OnFrontChanged(LayoutMorphItem? item) => FrontItem = item;

    /// <summary>The stage was resized while a card is open: the dialog layer follows the detail's new size.</summary>
    internal void OnOpenPoseChanged()
    {
        if (OpenedItem is { } item)
        {
            UpdateDialog(item);
        }
    }

    /// <summary>The stage started or ended a drag, or its list became (un)scrollable.</summary>
    internal void OnStageStateChanged()
    {
        PseudoClasses.Set(DraggingPseudoClass, _stage.IsDragging);
        PseudoClasses.Set(ScrollPseudoClass, _stage.IsScrollable);
    }

    internal void Announce(string text) => _live?.Announce(text);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && OpenedItem is not null)
        {
            Close();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    // Opening ------------------------------------------------------------------------------------------------------------

    private void OnOpenedItemChanged(LayoutMorphItem? old, LayoutMorphItem? item)
    {
        FlushItems();
        var index = _stage.IndexOfItem(item);
        if (item is not null && index < 0)
        {
            SetCurrentValue(OpenedItemProperty, null);
            return;
        }
        if (item is null)
        {
            var returnTo = _stage.OpenIndex;
            _stage.Close();
            PseudoClasses.Set(OpenPseudoClass, false);
            HideDialog(old);
            if (returnTo >= 0 && returnTo < _stage.Count)
            {
                _stage.CardAt(returnTo).Focus(NavigationMethod.Directional);
            }
            if (old is not null)
            {
                RaiseEvent(new LayoutMorphItemEventArgs(ItemClosedEvent, old));
            }
            return;
        }
        _stage.Open(index);
        PseudoClasses.Set(OpenPseudoClass, true);
        ShowDialog(item);
        Announce(string.Format(OpenedFormat, item.Title));
        if (old is not null)
        {
            RaiseEvent(new LayoutMorphItemEventArgs(ItemClosedEvent, old));
        }
        RaiseEvent(new LayoutMorphItemEventArgs(ItemOpenedEvent, item));
    }
}
