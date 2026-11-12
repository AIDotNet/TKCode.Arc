using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The top bar of a website, drawn as a single surface that changes shape: the brand first, then the sections, then
/// search and a single call to action. A highlight follows the open item, else the one under the pointer or keyboard
/// focus; a section with links stretches the surface down into its panel, resized on a spring, and search opens inside
/// the same surface. <see cref="IsCompact"/> slims the bar, and a nav narrower than <see cref="CollapseBelow"/> gathers
/// its sections into one menu.
/// </summary>
/// <remarks>
/// The nav takes the bar's height in the layout and the width it is given; the bar is centred in it. Open panels and
/// search grow over the page below (the nav sits above its siblings, <c>ZIndex</c> 10), so put it in the same panel as
/// the content it floats over and do not clip that panel.
/// </remarks>
[TemplatePart(StagePart, typeof(MorphNavStage))]
[TemplatePart(LayersPart, typeof(MorphNavLayers))]
[TemplatePart(BarPart, typeof(MorphNavBar))]
[TemplatePart(BrandPart, typeof(MorphNavBrandButton))]
[TemplatePart(TrackPart, typeof(MorphNavTrack))]
[TemplatePart(SearchButtonPart, typeof(MorphNavToolButton))]
[TemplatePart(ActionPart, typeof(MorphNavActionButton))]
[PseudoClasses(OpenPseudoClass, SearchingPseudoClass, CompactPseudoClass, CollapsedPseudoClass)]
public class MorphNav : TemplatedControl
{
    internal const string StagePart = "PART_Stage";
    internal const string LayersPart = "PART_Layers";
    internal const string BarPart = "PART_Bar";
    internal const string BrandPart = "PART_Brand";
    internal const string TrackPart = "PART_Track";
    internal const string SearchButtonPart = "PART_SearchButton";
    internal const string ActionPart = "PART_Action";
    internal const string OpenPseudoClass = ":open";
    internal const string SearchingPseudoClass = ":searching";
    internal const string CompactPseudoClass = ":compact";
    internal const string CollapsedPseudoClass = ":collapsed";

    /// <summary>What the menu trigger of a folded bar opens.</summary>
    internal static readonly object MenuKey = new();

    /// <summary>What the search results face shows.</summary>
    internal static readonly object SearchKey = new();

    /// <summary>Your brand's name, shown first in the bar and used as the brand button's accessible name.</summary>
    public static readonly StyledProperty<string?> BrandNameProperty =
        AvaloniaProperty.Register<MorphNav, string?>(nameof(BrandName));

    /// <summary>The brand mark before the name (any content, about 26 × 26), such as a logo tile.</summary>
    public static readonly StyledProperty<object?> BrandMarkProperty =
        AvaloniaProperty.Register<MorphNav, object?>(nameof(BrandMark));

    /// <summary>Template for <see cref="BrandMark"/> when it is data rather than a control.</summary>
    public static readonly StyledProperty<IDataTemplate?> BrandMarkTemplateProperty =
        AvaloniaProperty.Register<MorphNav, IDataTemplate?>(nameof(BrandMarkTemplate));

    /// <summary>Where the brand leads, passed on in <see cref="MorphNavNavigateEventArgs.Href"/>.</summary>
    public static readonly StyledProperty<string?> BrandHrefProperty =
        AvaloniaProperty.Register<MorphNav, string?>(nameof(BrandHref));

    /// <summary>Text of the call-to-action button at the far end of the bar; null leaves the button out.</summary>
    public static readonly StyledProperty<string?> ActionLabelProperty =
        AvaloniaProperty.Register<MorphNav, string?>(nameof(ActionLabel));

    /// <summary>A 24 × 24 stroked glyph before the action's label; null shows the label alone.</summary>
    public static readonly StyledProperty<Geometry?> ActionIconProperty =
        AvaloniaProperty.Register<MorphNav, Geometry?>(nameof(ActionIcon));

    /// <summary>Command run when the action is chosen, after <see cref="ActionInvoked"/>.</summary>
    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<MorphNav, ICommand?>(nameof(ActionCommand));

    /// <summary>The <see cref="MorphNavItem.Value"/> of the section the visitor is in; that item is drawn in the foreground color and announced as the current page.</summary>
    public static readonly StyledProperty<string?> CurrentProperty =
        AvaloniaProperty.Register<MorphNav, string?>(nameof(Current));

    /// <summary>Slims the bar to 46 px (from 56) and tucks the brand name away; typically switched on after the page has scrolled a little.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<MorphNav, bool>(nameof(IsCompact));

    /// <summary>The nav width (px) under which its sections are gathered into a single menu.</summary>
    public static readonly StyledProperty<double> CollapseBelowProperty =
        AvaloniaProperty.Register<MorphNav, double>(nameof(CollapseBelow), 720);

    /// <summary>Accessible name of the navigation landmark.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(Label), "Main");

    /// <summary>Placeholder of the search input.</summary>
    public static readonly StyledProperty<string?> SearchPlaceholderProperty =
        AvaloniaProperty.Register<MorphNav, string?>(nameof(SearchPlaceholder), "Search");

    /// <summary>Accessible name of the search button and the search input.</summary>
    public static readonly StyledProperty<string> SearchLabelProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(SearchLabel), "Search");

    /// <summary>Accessible name and tooltip of the button that closes search.</summary>
    public static readonly StyledProperty<string> CloseSearchLabelProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(CloseSearchLabel), "Close search");

    /// <summary>Accessible name of the result list.</summary>
    public static readonly StyledProperty<string> SearchResultsLabelProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(SearchResultsLabel), "Search results");

    /// <summary>Label of the one item a folded bar shows instead of its items.</summary>
    public static readonly StyledProperty<string> MenuLabelProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(MenuLabel), "Menu");

    /// <summary>Heading of the suggestions search shows for an empty query.</summary>
    public static readonly StyledProperty<string> SuggestedLabelProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(SuggestedLabel), "Suggested");

    /// <summary>Heading of a single result.</summary>
    public static readonly StyledProperty<string> SingleResultLabelProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(SingleResultLabel), "1 result");

    /// <summary>Heading of several results; <c>{0}</c> is the count.</summary>
    public static readonly StyledProperty<string> ResultsFormatProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(ResultsFormat), "{0} results");

    /// <summary>Heading when nothing matches; <c>{0}</c> is the query.</summary>
    public static readonly StyledProperty<string> NoResultsFormatProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(NoResultsFormat), "No results for “{0}”");

    /// <summary>The <see cref="MorphNavItem.Value"/> of the item whose panel is open, or null.</summary>
    public static readonly DirectProperty<MorphNav, string?> OpenValueProperty =
        AvaloniaProperty.RegisterDirect<MorphNav, string?>(nameof(OpenValue), o => o.OpenValue);

    /// <summary>Whether search is open.</summary>
    public static readonly DirectProperty<MorphNav, bool> IsSearchOpenProperty =
        AvaloniaProperty.RegisterDirect<MorphNav, bool>(nameof(IsSearchOpen), o => o.IsSearchOpen);

    /// <summary>Whether the items are folded into one menu (the nav is narrower than <see cref="CollapseBelow"/>).</summary>
    public static readonly DirectProperty<MorphNav, bool> IsCollapsedProperty =
        AvaloniaProperty.RegisterDirect<MorphNav, bool>(nameof(IsCollapsed), o => o.IsCollapsed);

    /// <summary>Raised whenever something that leads somewhere is chosen: a panel or footer link, a feature card, a search result, an item without a panel, or the brand. Bubbles.</summary>
    public static readonly RoutedEvent<MorphNavNavigateEventArgs> NavigateEvent =
        RoutedEvent.Register<MorphNav, MorphNavNavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    /// <summary>Raised when the primary action is chosen. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<MorphNav, RoutedEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    private readonly List<MorphNavTrigger> _triggers = new();
    private readonly List<MorphNavSearchEntry> _results = new();
    private MorphNavStage? _stage;
    private MorphNavLayers? _layers;
    private MorphNavBar? _bar;
    private MorphNavBrandButton? _brand;
    private MorphNavTrack? _track;
    private MorphNavToolButton? _searchButton;
    private MorphNavActionButton? _action;
    private MorphNavTrigger? _menuTrigger;
    private MorphNavLayerMotion? _barMotion;
    private MorphNavSearchBar? _searchBar;
    private MorphNavFace? _searchFace;
    private MorphNavSearchBody? _searchBody;
    private MorphNavFace? _face;
    private MorphNavTrigger? _openTrigger;
    private MorphNavTrigger? _hovered;
    private MorphNavTrigger? _pendingOpen;
    private DispatcherTimer? _openTimer;
    private DispatcherTimer? _closeTimer;
    private TopLevel? _pressRoot;
    private IDisposable? _viewport;
    private MorphNavItem[] _watched = [];
    private string? _openValue;
    private bool _searchOpen;
    private bool _collapsed;
    private bool _tiny;
    private bool _hasTarget;
    private bool _pendingAnimate;
    private Size _target;
    private double _targetRadius;
    private Size _lastBar;
    private double _arrangedWidth;
    private int _focusRequest;

    static MorphNav()
    {
        FocusableProperty.OverrideDefaultValue<MorphNav>(false);
        ClipToBoundsProperty.OverrideDefaultValue<MorphNav>(false);
        ZIndexProperty.OverrideDefaultValue<MorphNav>(10);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<MorphNav>(AutomationLandmarkType.Navigation);
        AffectsMeasure<MorphNav>(CollapseBelowProperty);
        SpecialFocusModality.Watch();
    }

    public MorphNav()
    {
        Items.CollectionChanged += OnItemsChanged;
        SearchEntries.CollectionChanged += OnSearchEntriesChanged;
        AutomationProperties.SetName(this, Label);
    }

    /// <summary>The top-level items, in order.</summary>
    [Content]
    public AvaloniaList<MorphNavItem> Items { get; } = new();

    /// <summary>What search can find. Search shows only while there are entries.</summary>
    public AvaloniaList<MorphNavSearchEntry> SearchEntries { get; } = new();

    /// <inheritdoc cref="BrandNameProperty"/>
    public string? BrandName { get => GetValue(BrandNameProperty); set => SetValue(BrandNameProperty, value); }

    /// <inheritdoc cref="BrandMarkProperty"/>
    public object? BrandMark { get => GetValue(BrandMarkProperty); set => SetValue(BrandMarkProperty, value); }

    /// <inheritdoc cref="BrandMarkTemplateProperty"/>
    public IDataTemplate? BrandMarkTemplate { get => GetValue(BrandMarkTemplateProperty); set => SetValue(BrandMarkTemplateProperty, value); }

    /// <inheritdoc cref="BrandHrefProperty"/>
    public string? BrandHref { get => GetValue(BrandHrefProperty); set => SetValue(BrandHrefProperty, value); }

    /// <inheritdoc cref="ActionLabelProperty"/>
    public string? ActionLabel { get => GetValue(ActionLabelProperty); set => SetValue(ActionLabelProperty, value); }

    /// <inheritdoc cref="ActionIconProperty"/>
    public Geometry? ActionIcon { get => GetValue(ActionIconProperty); set => SetValue(ActionIconProperty, value); }

    /// <inheritdoc cref="ActionCommandProperty"/>
    public ICommand? ActionCommand { get => GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }

    /// <inheritdoc cref="CurrentProperty"/>
    public string? Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <inheritdoc cref="CollapseBelowProperty"/>
    public double CollapseBelow { get => GetValue(CollapseBelowProperty); set => SetValue(CollapseBelowProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="SearchPlaceholderProperty"/>
    public string? SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <inheritdoc cref="SearchLabelProperty"/>
    public string SearchLabel { get => GetValue(SearchLabelProperty); set => SetValue(SearchLabelProperty, value); }

    /// <inheritdoc cref="CloseSearchLabelProperty"/>
    public string CloseSearchLabel { get => GetValue(CloseSearchLabelProperty); set => SetValue(CloseSearchLabelProperty, value); }

    /// <inheritdoc cref="SearchResultsLabelProperty"/>
    public string SearchResultsLabel { get => GetValue(SearchResultsLabelProperty); set => SetValue(SearchResultsLabelProperty, value); }

    /// <inheritdoc cref="MenuLabelProperty"/>
    public string MenuLabel { get => GetValue(MenuLabelProperty); set => SetValue(MenuLabelProperty, value); }

    /// <inheritdoc cref="SuggestedLabelProperty"/>
    public string SuggestedLabel { get => GetValue(SuggestedLabelProperty); set => SetValue(SuggestedLabelProperty, value); }

    /// <inheritdoc cref="SingleResultLabelProperty"/>
    public string SingleResultLabel { get => GetValue(SingleResultLabelProperty); set => SetValue(SingleResultLabelProperty, value); }

    /// <inheritdoc cref="ResultsFormatProperty"/>
    public string ResultsFormat { get => GetValue(ResultsFormatProperty); set => SetValue(ResultsFormatProperty, value); }

    /// <inheritdoc cref="NoResultsFormatProperty"/>
    public string NoResultsFormat { get => GetValue(NoResultsFormatProperty); set => SetValue(NoResultsFormatProperty, value); }

    /// <inheritdoc cref="OpenValueProperty"/>
    public string? OpenValue
    {
        get => _openValue;
        private set => SetAndRaise(OpenValueProperty, ref _openValue, value);
    }

    /// <inheritdoc cref="IsSearchOpenProperty"/>
    public bool IsSearchOpen
    {
        get => _searchOpen;
        private set => SetAndRaise(IsSearchOpenProperty, ref _searchOpen, value);
    }

    /// <inheritdoc cref="IsCollapsedProperty"/>
    public bool IsCollapsed
    {
        get => _collapsed;
        private set => SetAndRaise(IsCollapsedProperty, ref _collapsed, value);
    }

    /// <inheritdoc cref="NavigateEvent"/>
    public event EventHandler<MorphNavNavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<RoutedEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <summary>The bar's item triggers (without the menu trigger), in order.</summary>
    internal IReadOnlyList<MorphNavTrigger> Triggers => _triggers;

    internal MorphNavTrigger? MenuTrigger => _menuTrigger;

    internal MorphNavStage? Stage => _stage;

    internal MorphNavBar? Bar => _bar;

    internal MorphNavTrack? Track => _track;

    internal MorphNavBrandButton? Brand => _brand;

    internal MorphNavToolButton? SearchButton => _searchButton;

    internal MorphNavActionButton? ActionButton => _action;

    internal MorphNavFace? OpenFace => _face;

    internal MorphNavSearchBar? SearchBar => _searchBar;

    internal MorphNavSearchBody? SearchBody => _searchBody;

    internal MorphNavFace? SearchFace => _searchFace;

    internal MorphNavTrigger? OpenTrigger => _openTrigger;

    internal bool IsOpenPending => _openTimer?.IsEnabled == true;

    internal bool IsClosePending => _closeTimer?.IsEnabled == true;

    /// <summary>Opens the panel of the item with <paramref name="value"/> (or the folded menu when the bar is folded).</summary>
    public void Open(string value)
    {
        if (_collapsed)
        {
            if (_menuTrigger is not null)
            {
                OpenPanel(_menuTrigger, focusFirst: false);
            }
            return;
        }
        var trigger = _triggers.FirstOrDefault(t => t.HasPanel && t.Item?.Value == value);
        if (trigger is not null)
        {
            OpenPanel(trigger, focusFirst: false);
        }
    }

    /// <summary>Closes the open panel.</summary>
    public void ClosePanel() => ClosePanel(returnFocus: false);

    /// <summary>Opens search and moves focus to its input.</summary>
    public void OpenSearch() => ShowSearch();

    /// <summary>Closes search.</summary>
    public void CloseSearch() => HideSearch(returnFocus: false);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _stage = e.NameScope.Find<MorphNavStage>(StagePart);
        _layers = e.NameScope.Find<MorphNavLayers>(LayersPart);
        _bar = e.NameScope.Find<MorphNavBar>(BarPart);
        _brand = e.NameScope.Find<MorphNavBrandButton>(BrandPart);
        _track = e.NameScope.Find<MorphNavTrack>(TrackPart);
        _searchButton = e.NameScope.Find<MorphNavToolButton>(SearchButtonPart);
        _action = e.NameScope.Find<MorphNavActionButton>(ActionPart);
        _searchBar = null;
        _searchFace = null;
        _searchBody = null;
        _face = null;
        _openTrigger = null;
        _hasTarget = false;
        if (_layers is not null)
        {
            _layers.Bar = _bar;
            _layers.Measured += OnLayersMeasured;
        }
        if (_bar is not null)
        {
            _barMotion = new MorphNavLayerMotion(_bar, scales: true);
        }
        if (_brand is not null)
        {
            _brand.Click += OnBrandClick;
        }
        if (_searchButton is not null)
        {
            _searchButton.Click += OnSearchButtonClick;
        }
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        if (_track is not null)
        {
            _track.Arranged += OnTrackArranged;
        }
        RebuildTriggers();
        UpdateTools();
        UpdateCompact();
        UpdateNames();
        UpdateOpenState();
    }

    private void Unhook()
    {
        if (_brand is not null)
        {
            _brand.Click -= OnBrandClick;
        }
        if (_searchButton is not null)
        {
            _searchButton.Click -= OnSearchButtonClick;
        }
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        if (_track is not null)
        {
            _track.Arranged -= OnTrackArranged;
        }
        if (_layers is not null)
        {
            _layers.Measured -= OnLayersMeasured;
        }
        CancelTimers();
        OpenValue = null;
        IsSearchOpen = false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentProperty)
        {
            UpdateCurrent();
        }
        else if (change.Property == IsCompactProperty)
        {
            UpdateCompact();
        }
        else if (change.Property == ActionLabelProperty || change.Property == BrandNameProperty || change.Property == BrandMarkProperty)
        {
            UpdateTools();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == MenuLabelProperty && _menuTrigger is not null)
        {
            _menuTrigger.Label = MenuLabel;
        }
        else if (change.Property == SearchLabelProperty || change.Property == CloseSearchLabelProperty
                 || change.Property == SearchResultsLabelProperty || change.Property == SearchPlaceholderProperty)
        {
            UpdateNames();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && !change.GetNewValue<bool>())
        {
            Dispatcher.UIThread.Post(CloseIfFocusLeft, DispatcherPriority.Input);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewport = ArcViewport.Observe(this, _ => InvalidateMeasure());
        WatchOutsidePresses(_openTrigger is not null || _searchOpen);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewport?.Dispose();
        _viewport = null;
        CancelTimers();
        WatchOutsidePresses(false);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NavPeer(this);

    #region Layout and surface

    private double Token(MorphNavTokenKind kind, double fallback)
        => this.TryFindResource(MorphNavTokens.Key(kind), out var value) && value is double d ? d : fallback;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var container = double.IsInfinity(availableSize.Width)
            ? (_arrangedWidth > 0 ? _arrangedWidth : double.PositiveInfinity)
            : availableSize.Width;
        var viewport = ArcViewport.GetSize(this);
        _tiny = viewport is { } size && size.Width < Token(MorphNavTokenKind.TinyViewportWidth, 420);
        UpdateCollapsed(!double.IsInfinity(container) && container < CollapseBelow);
        if (_layers is not null)
        {
            var gutter = Token(MorphNavTokenKind.FaceGutter, 16);
            var room = double.IsInfinity(container) ? double.PositiveInfinity : Math.Max(160, container - gutter);
            _layers.FaceMaxWidth = Math.Min(Token(MorphNavTokenKind.FaceMaxWidth, 700), room);
            _layers.SearchWidth = Math.Min(Token(MorphNavTokenKind.SearchWidth, 544), room);
        }
        return base.MeasureOverride(availableSize);
    }

    private void OnLayersMeasured(object? sender, EventArgs e) => SyncSurface();

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _arrangedWidth) > 0.5)
        {
            _arrangedWidth = finalSize.Width;
        }
        return base.ArrangeOverride(finalSize);
    }

    /// <summary>
    /// Points the surface at the bar, the open panel or search. State changes and panels that change size animate on the
    /// grow or shrink spring; the bar changing size on its own (compact, folding) is followed at once unless a morph is
    /// already under way.
    /// </summary>
    private void SyncSurface()
    {
        if (_stage is null || _bar is null)
        {
            return;
        }
        var bar = _bar.DesiredSize;
        Size target;
        double radius;
        var openRadius = _stage.OpenCornerRadius.TopLeft;
        if (_searchOpen && _searchFace is not null)
        {
            target = new Size(_layers?.SearchWidth ?? 544, bar.Height + _searchFace.DesiredSize.Height);
            radius = openRadius;
        }
        else if (_face is not null)
        {
            target = new Size(Math.Max(bar.Width, _face.DesiredSize.Width), bar.Height + _face.DesiredSize.Height);
            radius = openRadius;
        }
        else
        {
            target = bar;
            radius = Math.Min(_stage.ClosedCornerRadius.TopLeft, bar.Height / 2);
        }
        target = new Size(Math.Round(target.Width), Math.Round(target.Height));
        var barChanged = bar != _lastBar;
        _lastBar = bar;
        if (_hasTarget && target == _target && Math.Abs(radius - _targetRadius) < 0.01)
        {
            _pendingAnimate = false;
            return;
        }
        var open = _searchOpen || _face is not null;
        var animate = _hasTarget && (_pendingAnimate || _stage.IsMorphing || (open && !barChanged));
        var grow = target.Width * target.Height > _target.Width * _target.Height;
        _target = target;
        _targetRadius = radius;
        _hasTarget = true;
        _pendingAnimate = false;
        _stage.MoveTo(target, radius, grow ? MorphNavMotion.Grow : MorphNavMotion.Shrink, animate);
    }

    private void UpdateCollapsed(bool collapsed)
    {
        if (collapsed == _collapsed && _menuTrigger is not null && _menuTrigger.IsVisible == collapsed)
        {
            return;
        }
        var changed = collapsed != _collapsed;
        IsCollapsed = collapsed;
        PseudoClasses.Set(CollapsedPseudoClass, collapsed);
        foreach (var trigger in _triggers)
        {
            trigger.IsVisible = !collapsed;
        }
        if (_menuTrigger is not null)
        {
            _menuTrigger.IsVisible = collapsed;
        }
        _brand?.SetBare(IsCompact || collapsed);
        if (changed && _openTrigger is not null)
        {
            // The open panel belongs to the other layout: it closes rather than jump into the menu.
            ClosePanel(returnFocus: false);
        }
    }

    #endregion

    #region Items

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildTriggers();

    private void OnLinksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var trigger in _triggers)
        {
            trigger.HasPanel = trigger.Item?.HasPanel == true;
        }
    }

    private void RebuildTriggers()
    {
        foreach (var item in _watched)
        {
            item.Links.CollectionChanged -= OnLinksChanged;
        }
        _watched = Items.ToArray();
        foreach (var item in _watched)
        {
            item.Links.CollectionChanged += OnLinksChanged;
        }
        if (_track is null)
        {
            return;
        }
        if (_openTrigger is not null)
        {
            ClosePanel(returnFocus: false);
        }
        foreach (var trigger in _triggers)
        {
            Unhook(trigger);
            _track.Children.Remove(trigger);
        }
        _triggers.Clear();
        _hovered = null;
        if (_menuTrigger is null)
        {
            _menuTrigger = new MorphNavTrigger(null) { Label = MenuLabel, HasPanel = true, IsVisible = _collapsed };
            Hook(_menuTrigger);
            _track.Children.Add(_menuTrigger);
        }
        foreach (var item in _watched)
        {
            var trigger = new MorphNavTrigger(item)
            {
                Label = item.Label,
                HasPanel = item.HasPanel,
                IsVisible = !_collapsed
            };
            Hook(trigger);
            _triggers.Add(trigger);
            _track.Children.Add(trigger);
        }
        UpdateCurrent();
        UpdateCompact();
        UpdateHighlight();
    }

    private void Hook(MorphNavTrigger trigger)
    {
        trigger.Click += OnTriggerClick;
        trigger.PointerEntered += OnTriggerPointerEntered;
        trigger.PointerExited += OnTriggerPointerExited;
        trigger.FocusChanged += OnTriggerFocusChanged;
    }

    private void Unhook(MorphNavTrigger trigger)
    {
        trigger.Click -= OnTriggerClick;
        trigger.PointerEntered -= OnTriggerPointerEntered;
        trigger.PointerExited -= OnTriggerPointerExited;
        trigger.FocusChanged -= OnTriggerFocusChanged;
    }

    private void UpdateCurrent()
    {
        var current = Current;
        foreach (var trigger in _triggers)
        {
            trigger.SetCurrent(current is not null && trigger.Item?.Value == current);
        }
    }

    private void UpdateCompact()
    {
        var compact = IsCompact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        _bar?.SetCompact(compact);
        _brand?.SetCompact(compact);
        _brand?.SetBare(compact || _collapsed);
        _searchButton?.SetCompact(compact);
        _action?.SetCompact(compact);
        _menuTrigger?.SetCompact(compact);
        foreach (var trigger in _triggers)
        {
            trigger.SetCompact(compact);
        }
        _searchBar?.SetCompact(compact);
    }

    private void UpdateTools()
    {
        if (_brand is not null)
        {
            _brand.IsVisible = BrandName is { Length: > 0 } || BrandMark is not null;
        }
        if (_searchButton is not null)
        {
            _searchButton.IsVisible = SearchEntries.Count > 0;
        }
        if (_action is not null)
        {
            _action.IsVisible = ActionLabel is { Length: > 0 };
        }
    }

    private void UpdateNames()
    {
        if (_searchButton is not null)
        {
            AutomationProperties.SetName(_searchButton, SearchLabel);
        }
        if (_searchBar is not null)
        {
            _searchBar.Placeholder = SearchPlaceholder;
            if (_searchBar.Input is { } input)
            {
                AutomationProperties.SetName(input, SearchLabel);
            }
            if (_searchBar.CloseButton is { } close)
            {
                AutomationProperties.SetName(close, CloseSearchLabel);
                Tooltip.SetTip(close, CloseSearchLabel);
            }
        }
        if (_searchBody is not null)
        {
            AutomationProperties.SetName(_searchBody.List, SearchResultsLabel);
        }
    }

    #endregion

    #region Pointer and hover intent

    private static bool IsMouse(PointerEventArgs e) => e.Pointer.Type == PointerType.Mouse;

    private void OnTriggerPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not MorphNavTrigger trigger || !IsMouse(e))
        {
            return;
        }
        _hovered = trigger;
        if (_searchOpen)
        {
            return;
        }
        _closeTimer?.Stop();
        if (trigger.HasPanel)
        {
            if (_openTrigger is not null)
            {
                OpenPanel(trigger, focusFirst: false);
            }
            else
            {
                StartOpenIntent(trigger);
            }
        }
        else
        {
            _openTimer?.Stop();
            _pendingOpen = null;
            if (_openTrigger is not null)
            {
                ClosePanel(returnFocus: false);
            }
        }
        UpdateHighlight();
    }

    private void OnTriggerPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not MorphNavTrigger trigger)
        {
            return;
        }
        if (ReferenceEquals(_hovered, trigger))
        {
            _hovered = null;
        }
        if (ReferenceEquals(_pendingOpen, trigger))
        {
            _openTimer?.Stop();
            _pendingOpen = null;
        }
        UpdateHighlight();
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (IsMouse(e))
        {
            _closeTimer?.Stop();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsMouse(e))
        {
            return;
        }
        _openTimer?.Stop();
        _pendingOpen = null;
        _hovered = null;
        if (_openTrigger is not null)
        {
            _closeTimer ??= NewTimer(MorphNavMotion.CloseIntent, OnCloseIntent);
            _closeTimer.Stop();
            _closeTimer.Start();
        }
        UpdateHighlight();
    }

    private void StartOpenIntent(MorphNavTrigger trigger)
    {
        _pendingOpen = trigger;
        _openTimer ??= NewTimer(MorphNavMotion.OpenIntent, OnOpenIntent);
        _openTimer.Stop();
        _openTimer.Start();
    }

    private static DispatcherTimer NewTimer(TimeSpan interval, EventHandler tick)
    {
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += tick;
        return timer;
    }

    private void OnOpenIntent(object? sender, EventArgs e)
    {
        _openTimer?.Stop();
        CompleteOpenIntent();
    }

    private void OnCloseIntent(object? sender, EventArgs e)
    {
        _closeTimer?.Stop();
        CompleteCloseIntent();
    }

    /// <summary>The hover intent has held: the pending panel opens (tests call this instead of waiting for the timer).</summary>
    internal void CompleteOpenIntent()
    {
        _openTimer?.Stop();
        var trigger = _pendingOpen;
        _pendingOpen = null;
        if (trigger is not null && !_searchOpen && trigger.IsVisible)
        {
            OpenPanel(trigger, focusFirst: false);
        }
    }

    /// <summary>The pointer stayed away: the panel closes (tests call this instead of waiting for the timer).</summary>
    internal void CompleteCloseIntent()
    {
        _closeTimer?.Stop();
        if (_openTrigger is not null && !IsPointerOver)
        {
            ClosePanel(returnFocus: false);
        }
    }

    private void CancelTimers()
    {
        _openTimer?.Stop();
        _closeTimer?.Stop();
        _pendingOpen = null;
    }

    private void WatchOutsidePresses(bool watch)
    {
        if (_pressRoot is not null)
        {
            _pressRoot.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
            _pressRoot = null;
        }
        if (watch && TopLevel.GetTopLevel(this) is { } root)
        {
            _pressRoot = root;
            root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)))
        {
            return;
        }
        if (_searchOpen)
        {
            HideSearch(returnFocus: false);
        }
        if (_openTrigger is not null)
        {
            ClosePanel(returnFocus: false);
        }
    }

    private void CloseIfFocusLeft()
    {
        if (!IsKeyboardFocusWithin && _openTrigger is not null && !IsPointerOver)
        {
            ClosePanel(returnFocus: false);
        }
    }

    #endregion

    #region Highlight

    private void OnTriggerFocusChanged(object? sender, EventArgs e) => UpdateHighlight();

    private void OnTrackArranged(object? sender, EventArgs e) => UpdateHighlight(snap: true);

    /// <summary>
    /// Moves the highlight to the open item, else the hovered one, else the one with keyboard focus; hides it when there
    /// is none or search covers the bar.
    /// </summary>
    private void UpdateHighlight(bool snap = false)
    {
        if (_track is null)
        {
            return;
        }
        var target = _searchOpen ? null : _openTrigger ?? _hovered ?? FocusedTrigger();
        var highlight = _track.Highlight;
        if (target is null || !target.IsVisible || target.Bounds.Width <= 0)
        {
            if (!snap && highlight.HasTarget)
            {
                highlight.Hide();
            }
            return;
        }
        var rect = target.Bounds;
        if (snap)
        {
            // The items changed size (compact, a new label): the highlight follows without a glide.
            if (highlight.HasTarget && highlight.CurrentRect != rect)
            {
                highlight.MoveTo(rect, animate: false);
            }
            return;
        }
        highlight.MoveTo(rect, MorphNavMotion.Glide);
    }

    private MorphNavTrigger? FocusedTrigger()
    {
        if (_menuTrigger is { IsFocusVisible: true })
        {
            return _menuTrigger;
        }
        foreach (var trigger in _triggers)
        {
            if (trigger.IsFocusVisible)
            {
                return trigger;
            }
        }
        return null;
    }

    #endregion

    #region Panels

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MorphNavTrigger trigger)
        {
            return;
        }
        e.Handled = true;
        if (!trigger.HasPanel)
        {
            if (_openTrigger is not null)
            {
                ClosePanel(returnFocus: false);
            }
            if (trigger.Item is { } item)
            {
                RaiseNavigate(item.Label ?? item.Value ?? string.Empty, item.Href, item.Value, item);
            }
            return;
        }
        if (SpecialFocusModality.LastWasPointer(this) == true)
        {
            if (ReferenceEquals(_openTrigger, trigger))
            {
                ClosePanel(returnFocus: false);
            }
            else
            {
                OpenPanel(trigger, focusFirst: false);
            }
        }
        else
        {
            OpenPanel(trigger, focusFirst: true);
        }
    }

    /// <summary>Assistive technology asked a trigger to expand.</summary>
    internal void OpenFromAutomation(MorphNavTrigger trigger) => OpenPanel(trigger, focusFirst: false);

    private int VisibleIndex(MorphNavTrigger trigger)
    {
        if (ReferenceEquals(trigger, _menuTrigger))
        {
            return -1;
        }
        return _triggers.IndexOf(trigger);
    }

    /// <summary>
    /// Opens <paramref name="trigger"/>'s panel. From the bar the panel drops in; from a neighbour it slides in from that
    /// side while the old one slides the other way.
    /// </summary>
    internal void OpenPanel(MorphNavTrigger trigger, bool focusFirst)
    {
        CancelTimers();
        if (_layers is null || !trigger.HasPanel)
        {
            return;
        }
        if (_searchOpen)
        {
            HideSearch(returnFocus: false);
        }
        if (ReferenceEquals(_openTrigger, trigger) && _face is not null)
        {
            if (focusFirst)
            {
                FocusFirstInPanel();
            }
            return;
        }
        var direction = 0;
        if (_openTrigger is not null && _face is not null)
        {
            direction = Math.Sign(VisibleIndex(trigger) - VisibleIndex(_openTrigger));
        }
        var old = _face;
        _openTrigger?.SetOpen(false);
        _openTrigger = trigger;
        trigger.SetOpen(true);
        OpenValue = trigger.Item?.Value;
        _face = BuildFace(trigger);
        if (old is not null)
        {
            Retire(old, direction == 0 ? new Point(0, -MorphNavMotion.LiftOut) : new Point(-direction * MorphNavMotion.SlideOut, 0));
        }
        _layers.Children.Add(_face);
        _face.Motion.StartHidden();
        _face.Motion.Show(direction == 0 ? new Point(0, -MorphNavMotion.DropIn) : new Point(direction * MorphNavMotion.SlideIn, 0),
            1, MorphNavMotion.EnterDelay);
        _pendingAnimate = true;
        _layers?.InvalidateMeasure();
        UpdateOpenState();
        UpdateHighlight();
        WatchOutsidePresses(true);
        if (focusFirst)
        {
            FocusFirstInPanel();
        }
    }

    /// <summary>Closes the open panel; the surface shrinks back to the bar.</summary>
    internal void ClosePanel(bool returnFocus)
    {
        CancelTimers();
        if (_openTrigger is null)
        {
            return;
        }
        var trigger = _openTrigger;
        var focusInside = _face?.IsKeyboardFocusWithin == true;
        if ((returnFocus || focusInside) && trigger.IsVisible)
        {
            _focusRequest++;
            trigger.Focus(SpecialFocusModality.For(this));
        }
        trigger.SetOpen(false);
        _openTrigger = null;
        OpenValue = null;
        if (_face is not null)
        {
            Retire(_face, new Point(0, -MorphNavMotion.LiftOut));
            _face = null;
        }
        _pendingAnimate = true;
        _layers?.InvalidateMeasure();
        UpdateOpenState();
        UpdateHighlight();
        WatchOutsidePresses(_searchOpen);
    }

    /// <summary>A face leaves: it stops taking input at once and is removed once it has faded out.</summary>
    private async void Retire(MorphNavFace face, Point to)
    {
        face.IsHitTestVisible = false;
        KeyboardNavigation.SetTabNavigation(face, KeyboardNavigationMode.None);
        if (await face.Motion.Hide(to, 1, MorphNavMotion.ExitFade))
        {
            _layers?.Children.Remove(face);
        }
    }

    private void FocusFirstInPanel()
    {
        var request = ++_focusRequest;
        var face = _face;
        Dispatcher.UIThread.Post(() =>
        {
            if (request != _focusRequest || face is null || !ReferenceEquals(face, _face))
            {
                return;
            }
            FocusablesOf(face).FirstOrDefault()?.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Loaded);
    }

    private static IEnumerable<InputElement> FocusablesOf(MorphNavFace face) =>
        face.GetVisualDescendants().OfType<AvaloniaButton>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled && b.Focusable);

    private MorphNavFace BuildFace(MorphNavTrigger trigger)
    {
        var face = new MorphNavFace(trigger.Key);
        AutomationProperties.SetName(face, trigger.Label);
        AutomationProperties.SetLabeledBy(face, trigger);
        face.AddHandler(AvaloniaButton.ClickEvent, OnFaceClick, RoutingStrategies.Bubble, handledEventsToo: true);
        face.Child = trigger.Item is { } item ? BuildPanel(item) : BuildMenu();
        return face;
    }

    private Control BuildPanel(MorphNavItem item)
    {
        var columns = item.Columns > 0 ? item.Columns : item.Links.Count > 3 ? 2 : 1;
        if (_tiny)
        {
            columns = 1;
        }
        var body = new MorphNavPanelBody(columns);
        foreach (var link in item.Links)
        {
            var button = new MorphNavLinkButton(link, link.Label, link.Description, link.Icon, dense: false, tiny: false);
            button.Invoked += (_, _) => RaiseNavigate(link.Label ?? string.Empty, link.Href, item.Value, link);
            body.AddLink(button);
        }
        if (item.Feature is { } feature)
        {
            if (feature is MorphNavStory story)
            {
                var card = new MorphNavStoryButton(story);
                card.Invoked += (_, _) => RaiseNavigate(story.Title ?? string.Empty, story.Href, item.Value, story);
                body.SetFeature(card);
            }
            else
            {
                body.SetFeature(Present(feature, item.FeatureTemplate));
            }
        }
        if (item.Footer is { } footerContent)
        {
            var footer = new MorphNavFooter();
            if (footerContent is MorphNavLink link)
            {
                var button = new MorphNavFooterLinkButton(link);
                button.Invoked += (_, _) => RaiseNavigate(link.Label ?? string.Empty, link.Href, item.Value, link);
                footer.Child = button;
            }
            else
            {
                footer.Child = Present(footerContent, item.FooterTemplate);
            }
            body.SetFooter(footer);
        }
        return body;
    }

    private static Control Present(object content, IDataTemplate? template)
    {
        var presenter = new ContentPresenter { Content = content };
        if (template is not null)
        {
            presenter.ContentTemplate = template;
        }
        return presenter;
    }

    private Control BuildMenu()
    {
        var body = new MorphNavMenuBody();
        StackPanel? plain = null;
        foreach (var item in _watched)
        {
            if (item.HasPanel)
            {
                var group = new StackPanel { Classes = { "morph-nav-group" } };
                group.Children.Add(new MorphNavHeading { Text = item.Label, Classes = { _tiny ? "tiny" : "menu" } });
                var list = new StackPanel { Classes = { "morph-nav-list" } };
                foreach (var link in item.Links)
                {
                    var button = new MorphNavLinkButton(link, link.Label, null, link.Icon, dense: true, tiny: _tiny);
                    button.Invoked += (_, _) => RaiseNavigate(link.Label ?? string.Empty, link.Href, item.Value, link);
                    list.Children.Add(button);
                }
                group.Children.Add(list);
                body.Children.Add(group);
            }
            else
            {
                plain ??= new StackPanel { Classes = { "morph-nav-list" } };
                var button = new MorphNavLinkButton(item, item.Label, null, null, dense: true, tiny: _tiny);
                button.Invoked += (_, _) => RaiseNavigate(item.Label ?? item.Value ?? string.Empty, item.Href, item.Value, item);
                plain.Children.Add(button);
            }
        }
        if (plain is not null)
        {
            body.Children.Add(plain);
        }
        return body;
    }

    /// <summary>Any button in a panel closes it: links navigate first, then their click reaches here.</summary>
    private void OnFaceClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MorphNavFace face && ReferenceEquals(face, _face))
        {
            ClosePanel(returnFocus: true);
        }
    }

    private void UpdateOpenState()
    {
        PseudoClasses.Set(OpenPseudoClass, _openTrigger is not null || _searchOpen);
        PseudoClasses.Set(SearchingPseudoClass, _searchOpen);
    }

    #endregion

    #region Search

    private void OnSearchEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTools();
        if (_searchOpen)
        {
            Rerank();
        }
        else if (SearchEntries.Count == 0)
        {
            HideSearch(returnFocus: false);
        }
    }

    private void OnSearchButtonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ShowSearch();
    }

    private void EnsureSearch()
    {
        if (_layers is null || _searchBar is not null)
        {
            return;
        }
        _searchBar = new MorphNavSearchBar { IsVisible = false, Placeholder = SearchPlaceholder };
        _searchBar.PartsReady += OnSearchPartsReady;
        _searchBar.SetCompact(IsCompact);
        _layers.SearchBar = _searchBar;
        _layers.Children.Insert(Math.Min(1, _layers.Children.Count), _searchBar);
        _searchBar.Motion.StartHidden();
        _searchBody = new MorphNavSearchBody(this);
        _searchFace = new MorphNavFace(SearchKey, fillsSearchWidth: true) { Child = _searchBody, IsVisible = false };
        _searchFace.Motion.StartHidden();
        _layers.Children.Add(_searchFace);
        UpdateNames();
    }

    private void OnSearchPartsReady(object? sender, EventArgs e)
    {
        if (_searchBar?.Input is { } input)
        {
            input.TextChanged -= OnQueryChanged;
            input.TextChanged += OnQueryChanged;
            input.RemoveHandler(KeyDownEvent, OnSearchKeyDown);
            input.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        }
        if (_searchBar?.CloseButton is { } close)
        {
            close.Click -= OnCloseSearchClick;
            close.Click += OnCloseSearchClick;
        }
        UpdateNames();
    }

    private void ShowSearch()
    {
        if (_layers is null || SearchEntries.Count == 0)
        {
            return;
        }
        EnsureSearch();
        if (_searchOpen)
        {
            FocusSearchSoon();
            return;
        }
        CancelTimers();
        if (_openTrigger is not null)
        {
            _openTrigger.SetOpen(false);
            _openTrigger = null;
            OpenValue = null;
            if (_face is not null)
            {
                Retire(_face, new Point(0, -MorphNavMotion.LiftOut));
                _face = null;
            }
        }
        IsSearchOpen = true;
        if (_searchBar!.Input is { } input)
        {
            input.Text = string.Empty;
        }
        Rerank();
        if (_bar is not null)
        {
            KeyboardNavigation.SetTabNavigation(_bar, KeyboardNavigationMode.None);
        }
        _ = _barMotion?.Hide(default, MorphNavMotion.BarScale, MorphNavMotion.LayerExitFade);
        _searchBar.IsVisible = true;
        _searchBar.Motion.Show(default, MorphNavMotion.SearchScale, MorphNavMotion.LayerEnterDelay);
        _searchFace!.IsVisible = true;
        _searchFace.Motion.Show(new Point(0, -MorphNavMotion.DropIn), 1, MorphNavMotion.EnterDelay);
        _pendingAnimate = true;
        _layers?.InvalidateMeasure();
        UpdateOpenState();
        UpdateHighlight();
        WatchOutsidePresses(true);
        FocusSearchSoon();
    }

    private void HideSearch(bool returnFocus)
    {
        if (!_searchOpen || _searchBar is null || _searchFace is null)
        {
            return;
        }
        var focusInside = _searchBar.IsKeyboardFocusWithin || _searchFace.IsKeyboardFocusWithin;
        IsSearchOpen = false;
        if (_bar is not null)
        {
            KeyboardNavigation.SetTabNavigation(_bar, KeyboardNavigationMode.Continue);
        }
        _barMotion?.Show(default, MorphNavMotion.BarScale, MorphNavMotion.LayerEnterDelay);
        if ((returnFocus || focusInside) && _searchButton is not null)
        {
            _focusRequest++;
            _searchButton.Focus(SpecialFocusModality.For(this));
        }
        HideLayer(_searchBar, _searchBar.Motion.Hide(default, MorphNavMotion.SearchScale, MorphNavMotion.LayerExitFade));
        HideLayer(_searchFace, _searchFace.Motion.Hide(new Point(0, -MorphNavMotion.LiftOut), 1, MorphNavMotion.ExitFade));
        _pendingAnimate = true;
        _layers?.InvalidateMeasure();
        UpdateOpenState();
        UpdateHighlight();
        WatchOutsidePresses(_openTrigger is not null);
    }

    private async void HideLayer(Control layer, Task<bool> hidden)
    {
        if (await hidden && !_searchOpen)
        {
            layer.IsVisible = false;
            if (ReferenceEquals(layer, _searchBar) && _searchBar.Input is { } input)
            {
                input.Text = string.Empty;
            }
        }
    }

    private void OnCloseSearchClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        HideSearch(returnFocus: true);
    }

    private void FocusSearchSoon()
    {
        var request = ++_focusRequest;
        Dispatcher.UIThread.Post(() =>
        {
            if (request == _focusRequest && _searchOpen)
            {
                _searchBar?.Input?.Focus(NavigationMethod.Tab);
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnQueryChanged(object? sender, TextChangedEventArgs e)
    {
        if (_searchOpen)
        {
            Rerank();
        }
    }

    /// <summary>Ranks the entries for the current query and shows the results, the first one active.</summary>
    private void Rerank()
    {
        if (_searchBody is null)
        {
            return;
        }
        var query = _searchBar?.Input?.Text?.Trim() ?? string.Empty;
        MorphNavSearch.Rank(SearchEntries, query, _results);
        string heading;
        if (query.Length == 0)
        {
            heading = SuggestedLabel;
        }
        else if (_results.Count == 0)
        {
            heading = string.Format(CultureInfo.CurrentCulture, NoResultsFormat, query);
        }
        else if (_results.Count == 1)
        {
            heading = SingleResultLabel;
        }
        else
        {
            heading = string.Format(CultureInfo.CurrentCulture, ResultsFormat, _results.Count);
        }
        _searchBody.Show(_results, heading, _results.Count > 0 ? 0 : -1);
    }

    /// <summary>The results on show, best first.</summary>
    internal IReadOnlyList<MorphNavSearchEntry> Results => _results;

    internal void SetActiveResult(int index)
    {
        if (_searchBody is not null && index >= 0 && index < _results.Count)
        {
            _searchBody.SetActive(index);
        }
    }

    internal void InvokeResult(int index)
    {
        if (index < 0 || index >= _results.Count)
        {
            return;
        }
        var entry = _results[index];
        RaiseNavigate(entry.Label ?? string.Empty, entry.Href, null, entry);
        HideSearch(returnFocus: true);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (_searchBody is null)
        {
            return;
        }
        var count = _results.Count;
        switch (e.Key)
        {
            case Key.Down when count > 0:
                _searchBody.SetActive((_searchBody.ActiveIndex + 1 + count) % count);
                e.Handled = true;
                break;
            case Key.Up when count > 0:
                _searchBody.SetActive((_searchBody.ActiveIndex - 1 + count) % count);
                e.Handled = true;
                break;
            case Key.Enter:
                InvokeResult(_searchBody.ActiveIndex);
                e.Handled = true;
                break;
            case Key.Escape:
                HideSearch(returnFocus: true);
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Brand, action and navigation

    private void OnBrandClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseNavigate(BrandName ?? string.Empty, BrandHref, null, null);
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(ActionInvokedEvent));
        if (ActionCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void RaiseNavigate(string label, string? href, string? section, object? destination) =>
        RaiseEvent(new MorphNavNavigateEventArgs(NavigateEvent, label, href, section, destination));

    #endregion

    #region Keyboard

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source)
        {
            base.OnKeyDown(e);
            return;
        }
        if (source.FindAncestorOfType<MorphNavTrigger>(includeSelf: true) is { } trigger && _track?.IsVisualAncestorOf(trigger) == true)
        {
            OnTriggerKey(trigger, e);
        }
        else if (_face is not null && (ReferenceEquals(source, _face) || _face.IsVisualAncestorOf(source)))
        {
            OnPanelKey(source, e);
        }
        else if (e.Key == Key.Escape)
        {
            if (_searchOpen)
            {
                HideSearch(returnFocus: true);
                e.Handled = true;
            }
            else if (_openTrigger is not null)
            {
                ClosePanel(returnFocus: true);
                e.Handled = true;
            }
        }
        base.OnKeyDown(e);
    }

    private IReadOnlyList<MorphNavTrigger> VisibleTriggers() =>
        _collapsed && _menuTrigger is not null ? [_menuTrigger] : _triggers;

    private void OnTriggerKey(MorphNavTrigger trigger, KeyEventArgs e)
    {
        var triggers = VisibleTriggers();
        var index = -1;
        for (var i = 0; i < triggers.Count; i++)
        {
            if (ReferenceEquals(triggers[i], trigger))
            {
                index = i;
            }
        }
        var target = e.Key switch
        {
            Key.Right when triggers.Count > 0 => (index + 1) % triggers.Count,
            Key.Left when triggers.Count > 0 => (index - 1 + triggers.Count) % triggers.Count,
            Key.Home when triggers.Count > 0 => 0,
            Key.End when triggers.Count > 0 => triggers.Count - 1,
            _ => -1
        };
        if (target >= 0)
        {
            e.Handled = true;
            var next = triggers[target];
            next.Focus(NavigationMethod.Directional);
            if (_openTrigger is not null && !ReferenceEquals(next, _openTrigger))
            {
                if (next.HasPanel)
                {
                    OpenPanel(next, focusFirst: false);
                }
                else
                {
                    ClosePanel(returnFocus: false);
                }
            }
            UpdateHighlight();
            return;
        }
        if (e.Key == Key.Down && trigger.HasPanel)
        {
            e.Handled = true;
            OpenPanel(trigger, focusFirst: true);
        }
        else if (e.Key == Key.Escape && _openTrigger is not null)
        {
            e.Handled = true;
            ClosePanel(returnFocus: true);
        }
    }

    private void OnPanelKey(Visual source, KeyEventArgs e)
    {
        if (_face is null)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ClosePanel(returnFocus: true);
            return;
        }
        if (e.Key is not (Key.Up or Key.Down))
        {
            return;
        }
        var focusables = FocusablesOf(_face).ToList();
        var current = source.FindAncestorOfType<AvaloniaButton>(includeSelf: true);
        var index = current is null ? -1 : focusables.IndexOf(current);
        if (index < 0)
        {
            return;
        }
        e.Handled = true;
        if (e.Key == Key.Down)
        {
            focusables[Math.Min(index + 1, focusables.Count - 1)].Focus(NavigationMethod.Directional);
        }
        else if (index == 0)
        {
            _openTrigger?.Focus(NavigationMethod.Directional);
        }
        else
        {
            focusables[index - 1].Focus(NavigationMethod.Directional);
        }
    }

    #endregion

    /// <summary>The nav is a navigation landmark named by <see cref="Label"/>.</summary>
    private sealed class NavPeer(MorphNav owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
