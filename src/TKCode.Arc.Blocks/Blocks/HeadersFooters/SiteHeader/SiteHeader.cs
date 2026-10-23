// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli).
using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A website header in three layouts (Arc's site header block): it sticks to the top of its scroll view and turns solid
/// once the page scrolls, marks the current section with an indicator that glides between links, opens springy mega menu
/// panels whose content slides in from the side you moved toward, and folds into a menu sheet when it is narrower than
/// <see cref="CollapseWidth"/> (the block's own width, not the window's).
/// <para>
/// Data driven: the links are <see cref="SiteHeaderItem"/>s (or <see cref="ItemsSource"/>); without items the header shows
/// Arc's example set. Choosing any destination raises <see cref="Navigate"/> (and runs <see cref="NavigateCommand"/>); when
/// nobody handles the event and the destination has an absolute http, https or mailto address, it is opened.
/// </para>
/// </summary>
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(InnerPart, typeof(Panel))]
[TemplatePart(BarPart, typeof(Panel))]
[TemplatePart(BrandPart, typeof(AvaloniaButton))]
[TemplatePart(NavListPart, typeof(Border))]
[TemplatePart(NavItemsPart, typeof(Panel))]
[TemplatePart(HoverPart, typeof(GlidingIndicator))]
[TemplatePart(UnderlinePart, typeof(GlidingIndicator))]
[TemplatePart(CapsulePart, typeof(GlidingIndicator))]
[TemplatePart(SecondaryPart, typeof(AvaloniaButton))]
[TemplatePart(PrimaryPart, typeof(AvaloniaButton))]
[TemplatePart(MenuButtonPart, typeof(AvaloniaButton))]
[TemplatePart(PanelLayerPart, typeof(Panel))]
[TemplatePart(SheetLayerPart, typeof(Panel))]
[PseudoClasses(SolidPseudoClass, NarrowPseudoClass, CenteredPseudoClass, PanelOpenPseudoClass, MenuOpenPseudoClass)]
public partial class SiteHeader : TemplatedControl
{
    /// <summary>Name of the template's surface (background, border and shadow).</summary>
    public const string SurfacePart = "PART_Surface";

    /// <summary>Name of the template's inner region (bar and mega panel).</summary>
    public const string InnerPart = "PART_Inner";

    /// <summary>Name of the bar.</summary>
    public const string BarPart = "PART_Bar";

    /// <summary>Name of the brand button.</summary>
    public const string BrandPart = "PART_Brand";

    /// <summary>Name of the navigation list.</summary>
    public const string NavListPart = "PART_NavList";

    /// <summary>Name of the panel the navigation items are in.</summary>
    public const string NavItemsPart = "PART_NavItems";

    /// <summary>Name of the hover block.</summary>
    public const string HoverPart = "PART_Hover";

    /// <summary>Name of the current underline.</summary>
    public const string UnderlinePart = "PART_Underline";

    /// <summary>Name of the current capsule of the centered layout.</summary>
    public const string CapsulePart = "PART_Capsule";

    /// <summary>Name of the secondary action.</summary>
    public const string SecondaryPart = "PART_Secondary";

    /// <summary>Name of the primary action.</summary>
    public const string PrimaryPart = "PART_Primary";

    /// <summary>Name of the menu button.</summary>
    public const string MenuButtonPart = "PART_MenuButton";

    /// <summary>Name of the layer the mega panel is drawn in.</summary>
    public const string PanelLayerPart = "PART_PanelLayer";

    /// <summary>Name of the layer the sheet and scrim are drawn in.</summary>
    public const string SheetLayerPart = "PART_SheetLayer";

    /// <summary>Set while the bar is solid: scrolled, the menu sheet is open or a panel is open.</summary>
    public const string SolidPseudoClass = ":solid";

    /// <summary>Set while the header is narrower than <see cref="CollapseWidth"/> (links in the menu sheet).</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set for <see cref="SiteHeaderVariant.Centered"/>.</summary>
    public const string CenteredPseudoClass = ":centered";

    /// <summary>Set while a mega panel is open.</summary>
    public const string PanelOpenPseudoClass = ":panel-open";

    /// <summary>Set while the menu sheet is open.</summary>
    public const string MenuOpenPseudoClass = ":menu-open";

    /// <summary>Arc's hover intent: a panel opens this long after the pointer enters its trigger.</summary>
    internal static readonly TimeSpan HoverIntent = TimeSpan.FromMilliseconds(70);

    /// <summary>Arc's leave grace: a panel closes this long after the pointer left the bar and the panel.</summary>
    internal static readonly TimeSpan LeaveGrace = TimeSpan.FromMilliseconds(180);

    /// <summary>Identifies <see cref="Variant"/>.</summary>
    public static readonly StyledProperty<SiteHeaderVariant> VariantProperty =
        AvaloniaProperty.Register<SiteHeader, SiteHeaderVariant>(nameof(Variant));

    /// <summary>Identifies <see cref="BrandName"/>.</summary>
    public static readonly StyledProperty<string> BrandNameProperty =
        AvaloniaProperty.Register<SiteHeader, string>(nameof(BrandName), "Arc");

    /// <summary>Identifies <see cref="BrandHref"/>.</summary>
    public static readonly StyledProperty<string?> BrandHrefProperty =
        AvaloniaProperty.Register<SiteHeader, string?>(nameof(BrandHref));

    /// <summary>Identifies <see cref="BrandMark"/>.</summary>
    public static readonly StyledProperty<object?> BrandMarkProperty =
        AvaloniaProperty.Register<SiteHeader, object?>(nameof(BrandMark));

    /// <summary>Identifies <see cref="Items"/>.</summary>
    public static readonly DirectProperty<SiteHeader, AvaloniaList<SiteHeaderItem>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<SiteHeader, AvaloniaList<SiteHeaderItem>>(nameof(Items), o => o.Items);

    /// <summary>Identifies <see cref="ItemsSource"/>.</summary>
    public static readonly StyledProperty<IEnumerable<SiteHeaderItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<SiteHeader, IEnumerable<SiteHeaderItem>?>(nameof(ItemsSource));

    /// <summary>Identifies <see cref="Current"/>.</summary>
    public static readonly StyledProperty<string?> CurrentProperty =
        AvaloniaProperty.Register<SiteHeader, string?>(nameof(Current), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Identifies <see cref="PrimaryActionLabel"/>.</summary>
    public static readonly StyledProperty<string> PrimaryActionLabelProperty =
        AvaloniaProperty.Register<SiteHeader, string>(nameof(PrimaryActionLabel), "Get Arc");

    /// <summary>Identifies <see cref="PrimaryActionHref"/>.</summary>
    public static readonly StyledProperty<string?> PrimaryActionHrefProperty =
        AvaloniaProperty.Register<SiteHeader, string?>(nameof(PrimaryActionHref));

    /// <summary>Identifies <see cref="IsPrimaryActionVisible"/>.</summary>
    public static readonly StyledProperty<bool> IsPrimaryActionVisibleProperty =
        AvaloniaProperty.Register<SiteHeader, bool>(nameof(IsPrimaryActionVisible), true);

    /// <summary>Identifies <see cref="SecondaryActionLabel"/>.</summary>
    public static readonly StyledProperty<string> SecondaryActionLabelProperty =
        AvaloniaProperty.Register<SiteHeader, string>(nameof(SecondaryActionLabel), "Sign in");

    /// <summary>Identifies <see cref="SecondaryActionHref"/>.</summary>
    public static readonly StyledProperty<string?> SecondaryActionHrefProperty =
        AvaloniaProperty.Register<SiteHeader, string?>(nameof(SecondaryActionHref));

    /// <summary>Identifies <see cref="IsSecondaryActionVisible"/>.</summary>
    public static readonly StyledProperty<bool> IsSecondaryActionVisibleProperty =
        AvaloniaProperty.Register<SiteHeader, bool>(nameof(IsSecondaryActionVisible), true);

    /// <summary>Identifies <see cref="TrailingContent"/>.</summary>
    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<SiteHeader, object?>(nameof(TrailingContent));

    /// <summary>Identifies <see cref="IsSticky"/>.</summary>
    public static readonly StyledProperty<bool> IsStickyProperty =
        AvaloniaProperty.Register<SiteHeader, bool>(nameof(IsSticky), true);

    /// <summary>Identifies <see cref="ScrollSource"/>.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollSourceProperty =
        AvaloniaProperty.Register<SiteHeader, ScrollViewer?>(nameof(ScrollSource));

    /// <summary>Identifies <see cref="ScrollThreshold"/>.</summary>
    public static readonly StyledProperty<double> ScrollThresholdProperty =
        AvaloniaProperty.Register<SiteHeader, double>(nameof(ScrollThreshold), 8);

    /// <summary>Identifies <see cref="IsScrolled"/>.</summary>
    public static readonly DirectProperty<SiteHeader, bool> IsScrolledProperty =
        AvaloniaProperty.RegisterDirect<SiteHeader, bool>(nameof(IsScrolled), o => o.IsScrolled);

    /// <summary>Identifies <see cref="IsMenuOpen"/>.</summary>
    public static readonly StyledProperty<bool> IsMenuOpenProperty =
        AvaloniaProperty.Register<SiteHeader, bool>(nameof(IsMenuOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Identifies <see cref="OpenItemValue"/>.</summary>
    public static readonly DirectProperty<SiteHeader, string?> OpenItemValueProperty =
        AvaloniaProperty.RegisterDirect<SiteHeader, string?>(nameof(OpenItemValue), o => o.OpenItemValue);

    /// <summary>Identifies <see cref="CollapseWidth"/>.</summary>
    public static readonly StyledProperty<double> CollapseWidthProperty =
        AvaloniaProperty.Register<SiteHeader, double>(nameof(CollapseWidth), 760);

    /// <summary>Identifies <see cref="IsCollapsed"/>.</summary>
    public static readonly DirectProperty<SiteHeader, bool> IsCollapsedProperty =
        AvaloniaProperty.RegisterDirect<SiteHeader, bool>(nameof(IsCollapsed), o => o.IsCollapsed);

    /// <summary>Identifies <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SiteHeader, string>(nameof(Label), "Main");

    /// <summary>Identifies <see cref="OpenMenuLabel"/>.</summary>
    public static readonly StyledProperty<string> OpenMenuLabelProperty =
        AvaloniaProperty.Register<SiteHeader, string>(nameof(OpenMenuLabel), "Open menu");

    /// <summary>Identifies <see cref="CloseMenuLabel"/>.</summary>
    public static readonly StyledProperty<string> CloseMenuLabelProperty =
        AvaloniaProperty.Register<SiteHeader, string>(nameof(CloseMenuLabel), "Close menu");

    /// <summary>Identifies <see cref="NavigateCommand"/>.</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<SiteHeader, ICommand?>(nameof(NavigateCommand));

    /// <summary>Identifies <see cref="Navigate"/>.</summary>
    public static readonly RoutedEvent<SiteHeaderNavigateEventArgs> NavigateEvent =
        RoutedEvent.Register<SiteHeader, SiteHeaderNavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    private readonly AvaloniaList<SiteHeaderItem> _items = new();
    private readonly Dictionary<string, SiteNavButton> _buttons = new(StringComparer.Ordinal);
    private readonly List<SiteHeaderData> _watched = new();
    private List<SiteHeaderItem>? _defaults;
    private List<SiteHeaderItem> _resolved = new();
    private INotifyCollectionChanged? _sourceNotifier;
    private bool _isScrolled;
    private bool _isCollapsed;
    private string? _openValue;

    // Template parts.
    private Border? _surface;
    private Panel? _inner;
    private SiteHeaderBar? _bar;
    private SiteBrandButton? _brand;
    private Border? _navList;
    private Panel? _navItems;
    private GlidingIndicator? _hover;
    private GlidingIndicator? _underline;
    private GlidingIndicator? _capsule;
    private SiteActionButton? _secondary;
    private SiteActionButton? _primary;
    private SiteMenuButton? _menuButton;
    private SiteHeaderFloatLayer? _panelLayer;
    private SiteHeaderFloatLayer? _sheetLayer;
    private IDisposable? _markBinding;

    static SiteHeader()
    {
        FocusableProperty.OverrideDefaultValue<SiteHeader>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<SiteHeader>(AutomationLandmarkType.Banner);
        VariantProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.OnVariantChanged());
        BrandNameProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateBrand());
        BrandMarkProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateBrand());
        ItemsSourceProperty.Changed.AddClassHandler<SiteHeader>((h, e) => h.OnItemsSourceChanged(e));
        CurrentProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.OnCurrentChanged());
        PrimaryActionLabelProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateActions());
        SecondaryActionLabelProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateActions());
        IsPrimaryActionVisibleProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateActions());
        IsSecondaryActionVisibleProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateActions());
        LabelProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateAutomation());
        OpenMenuLabelProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateAutomation());
        CloseMenuLabelProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateAutomation());
        IsMenuOpenProperty.Changed.AddClassHandler<SiteHeader>((h, e) => h.OnMenuOpenChanged(e.GetNewValue<bool>()));
        CollapseWidthProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateCollapsed());
        ScrollSourceProperty.Changed.AddClassHandler<SiteHeader>((h, e) => h.OnScrollSourceChanged(e.GetOldValue<ScrollViewer?>(), e.GetNewValue<ScrollViewer?>()));
        IsStickyProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateSticky());
        ScrollThresholdProperty.Changed.AddClassHandler<SiteHeader>((h, _) => h.UpdateScrolled());
    }

    /// <summary>Creates a header with Arc's example items.</summary>
    public SiteHeader()
    {
        _items.CollectionChanged += (_, _) => RebuildItems();
        RenderTransform = _stick;
    }

    /// <summary>Raised when a destination is chosen: an item, a panel link, the feature card, the brand or an action. Bubbles.</summary>
    public event EventHandler<SiteHeaderNavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <summary>The layout: mega menu panels (default), plain links beside the brand, or links centered in a capsule.</summary>
    public SiteHeaderVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>The brand's name (default "Arc").</summary>
    public string BrandName { get => GetValue(BrandNameProperty); set => SetValue(BrandNameProperty, value); }

    /// <summary>The address the brand opens; without one choosing the brand only raises <see cref="Navigate"/>.</summary>
    public string? BrandHref { get => GetValue(BrandHrefProperty); set => SetValue(BrandHrefProperty, value); }

    /// <summary>The mark before the name: any content, or an <see cref="IImage"/> (drawn at 22 by 22 wherever the brand shows). Null draws Arc's arch mark.</summary>
    public object? BrandMark { get => GetValue(BrandMarkProperty); set => SetValue(BrandMarkProperty, value); }

    /// <summary>The links of the bar. Empty (and no <see cref="ItemsSource"/>) shows Arc's example items: Product, Resources, Pricing and Customers.</summary>
    [Content]
    public AvaloniaList<SiteHeaderItem> Items => _items;

    /// <summary>A source of items; when set it wins over <see cref="Items"/> and is observed when it raises collection changes.</summary>
    public IEnumerable<SiteHeaderItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>The <see cref="SiteHeaderItem.Value"/> of the item that holds the current page (two way). Choosing a destination inside an item sets it.</summary>
    public string? Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    /// <summary>The text of the primary action at the end of the bar (default "Get Arc").</summary>
    public string PrimaryActionLabel { get => GetValue(PrimaryActionLabelProperty); set => SetValue(PrimaryActionLabelProperty, value); }

    /// <summary>The address the primary action opens.</summary>
    public string? PrimaryActionHref { get => GetValue(PrimaryActionHrefProperty); set => SetValue(PrimaryActionHrefProperty, value); }

    /// <summary>False hides the primary action (Arc: <c>primaryAction = null</c>).</summary>
    public bool IsPrimaryActionVisible { get => GetValue(IsPrimaryActionVisibleProperty); set => SetValue(IsPrimaryActionVisibleProperty, value); }

    /// <summary>The text of the quiet action before the primary one (default "Sign in"); shown on the bar only when it is wide.</summary>
    public string SecondaryActionLabel { get => GetValue(SecondaryActionLabelProperty); set => SetValue(SecondaryActionLabelProperty, value); }

    /// <summary>The address the secondary action opens.</summary>
    public string? SecondaryActionHref { get => GetValue(SecondaryActionHrefProperty); set => SetValue(SecondaryActionHrefProperty, value); }

    /// <summary>False hides the secondary action (Arc: <c>secondaryAction = null</c>).</summary>
    public bool IsSecondaryActionVisible { get => GetValue(IsSecondaryActionVisibleProperty); set => SetValue(IsSecondaryActionVisibleProperty, value); }

    /// <summary>Your own controls at the end of the bar, before the actions (a search button, a language menu, a theme toggle). Stays on the bar when the links collapse into the menu.</summary>
    public object? TrailingContent { get => GetValue(TrailingContentProperty); set => SetValue(TrailingContentProperty, value); }

    /// <summary>True keeps the header at the top of the scroll view it is in (Avalonia has no <c>position: sticky</c>: the header follows the scroll offset). Default true.</summary>
    public bool IsSticky { get => GetValue(IsStickyProperty); set => SetValue(IsStickyProperty, value); }

    /// <summary>The scroll view that scrolls under the header. Null (default) uses the nearest ancestor <see cref="ScrollViewer"/>; set it when the header overlays a scroll view instead of being inside it.</summary>
    public ScrollViewer? ScrollSource { get => GetValue(ScrollSourceProperty); set => SetValue(ScrollSourceProperty, value); }

    /// <summary>Pixels of scroll before the bar turns solid (default 8); it turns transparent again only below half of that.</summary>
    public double ScrollThreshold { get => GetValue(ScrollThresholdProperty); set => SetValue(ScrollThresholdProperty, value); }

    /// <summary>True while the page is scrolled past <see cref="ScrollThreshold"/>.</summary>
    public bool IsScrolled { get => _isScrolled; private set => SetAndRaise(IsScrolledProperty, ref _isScrolled, value); }

    /// <summary>Whether the menu sheet of narrow layouts is open (two way).</summary>
    public bool IsMenuOpen { get => GetValue(IsMenuOpenProperty); set => SetValue(IsMenuOpenProperty, value); }

    /// <summary>The <see cref="SiteHeaderItem.Value"/> of the item whose mega panel is open, or null.</summary>
    public string? OpenItemValue { get => _openValue; private set => SetAndRaise(OpenItemValueProperty, ref _openValue, value); }

    /// <summary>Below this width of the header itself the links move into the menu sheet (default 760).</summary>
    public double CollapseWidth { get => GetValue(CollapseWidthProperty); set => SetValue(CollapseWidthProperty, value); }

    /// <summary>True while the header is narrower than <see cref="CollapseWidth"/>.</summary>
    public bool IsCollapsed { get => _isCollapsed; private set => SetAndRaise(IsCollapsedProperty, ref _isCollapsed, value); }

    /// <summary>The accessible name of the navigation landmark (default "Main").</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The menu button's accessible name while the sheet is closed (default "Open menu").</summary>
    public string OpenMenuLabel { get => GetValue(OpenMenuLabelProperty); set => SetValue(OpenMenuLabelProperty, value); }

    /// <summary>The menu button's accessible name while the sheet is open (default "Close menu").</summary>
    public string CloseMenuLabel { get => GetValue(CloseMenuLabelProperty); set => SetValue(CloseMenuLabelProperty, value); }

    /// <summary>Runs with the <see cref="SiteHeaderNavigateEventArgs"/> whenever a destination is chosen.</summary>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    /// <summary>
    /// Creates Arc's example items (Product, Resources, Pricing and Customers, with their links and icons; the Product feature
    /// card has no picture). A starting point for your own set: change the texts, add a picture, add links.
    /// </summary>
    /// <returns>A new list of items each call.</returns>
    public static IEnumerable<SiteHeaderItem> CreateExampleItems() => SiteHeaderExamples.Create().ToList();

    /// <summary>Opens the mega panel of the item with <paramref name="value"/> (ignored for items without links, in other variants and in the collapsed layout).</summary>
    /// <param name="value">The item's <see cref="SiteHeaderItem.Value"/>.</param>
    public void OpenPanel(string value) => OpenPanelCore(value, false);

    /// <summary>Closes the mega panel.</summary>
    public void ClosePanel() => ClosePanelCore(false);

    /// <summary>Opens the menu sheet.</summary>
    public void OpenMenu() => IsMenuOpen = true;

    /// <summary>Closes the menu sheet and folds its groups.</summary>
    public void CloseMenu() => IsMenuOpen = false;

    // ---- items ----------------------------------------------------------------------------------------------

    private IEnumerable<SiteHeaderItem> ResolveItems()
    {
        if (ItemsSource is { } source)
        {
            return source;
        }
        if (_items.Count > 0)
        {
            return _items;
        }
        return _defaults ??= SiteHeaderExamples.Create().ToList();
    }

    private void OnItemsSourceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_sourceNotifier is not null)
        {
            _sourceNotifier.CollectionChanged -= OnSourceCollectionChanged;
            _sourceNotifier = null;
        }
        if (e.NewValue is INotifyCollectionChanged notifier)
        {
            _sourceNotifier = notifier;
            notifier.CollectionChanged += OnSourceCollectionChanged;
        }
        RebuildItems();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();

    private void OnItemInvalidated(object? sender, EventArgs e) => RebuildItems();

    private void Rewatch()
    {
        foreach (var data in _watched)
        {
            data.Invalidated -= OnItemInvalidated;
        }
        _watched.Clear();
        foreach (var item in _resolved)
        {
            Watch(item);
            foreach (var link in item.Links)
            {
                Watch(link);
            }
            if (item.Feature is { } feature)
            {
                Watch(feature);
            }
        }
    }

    private void Watch(SiteHeaderData data)
    {
        data.Invalidated += OnItemInvalidated;
        _watched.Add(data);
    }

    private bool HasPanel(SiteHeaderItem item) => Variant == SiteHeaderVariant.Mega && item.Links.Count > 0;

    private SiteHeaderItem? ItemByValue(string? value)
        => value is null ? null : _resolved.FirstOrDefault(i => string.Equals(i.Value, value, StringComparison.Ordinal));

    /// <summary>Rebuilds everything that depends on the items.</summary>
    private void RebuildItems()
    {
        _resolved = ResolveItems().ToList();
        Rewatch();
        if (_navItems is null)
        {
            return;
        }
        if (_openValue is not null && (ItemByValue(_openValue) is not { } open || !HasPanel(open)))
        {
            ClosePanelCore(false);
        }
        BuildNav();
        if (IsMenuOpen)
        {
            RebuildSheet();
        }
        else if (_openValue is not null)
        {
            RefreshPanelFace();
        }
    }

    private void BuildNav()
    {
        if (_navItems is null)
        {
            return;
        }
        foreach (var old in _buttons.Values)
        {
            old.Click -= OnNavClick;
            old.FinePointerEntered -= OnNavPointerEntered;
            old.GotFocus -= OnNavGotFocus;
            old.PropertyChanged -= OnNavButtonPropertyChanged;
        }
        _navItems.Children.Clear();
        _buttons.Clear();
        var centered = Variant == SiteHeaderVariant.Centered;
        foreach (var item in _resolved)
        {
            var panel = HasPanel(item);
            var button = new SiteNavButton
            {
                Label = item.Label,
                HasChevron = panel,
                IsExpandable = panel,
                IsCapsule = centered,
                IsCurrent = string.Equals(item.Value, Current, StringComparison.Ordinal),
                IsExpanded = panel && string.Equals(item.Value, _openValue, StringComparison.Ordinal),
                Tag = item
            };
            AutomationProperties.SetName(button, item.Label);
            button.Click += OnNavClick;
            button.FinePointerEntered += OnNavPointerEntered;
            button.GotFocus += OnNavGotFocus;
            button.PropertyChanged += OnNavButtonPropertyChanged;
            _navItems.Children.Add(button);
            _buttons[item.Value] = button;
        }
        _hoveredValue = null;
        _hover?.Hide(false);
        _underline?.Hide(false);
        _capsule?.Hide(false);
        UpdateCurrentIndicator(false);
    }

    private SiteHeaderItem? ItemOf(Control? button) => button?.Tag as SiteHeaderItem;

    // ---- template -------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unwire();
        _surface = e.NameScope.Find<Border>(SurfacePart);
        _inner = e.NameScope.Find<Panel>(InnerPart);
        _bar = e.NameScope.Find<SiteHeaderBar>(BarPart);
        _brand = e.NameScope.Find<SiteBrandButton>(BrandPart);
        _navList = e.NameScope.Find<Border>(NavListPart);
        _navItems = e.NameScope.Find<Panel>(NavItemsPart);
        _hover = e.NameScope.Find<GlidingIndicator>(HoverPart);
        _underline = e.NameScope.Find<GlidingIndicator>(UnderlinePart);
        _capsule = e.NameScope.Find<GlidingIndicator>(CapsulePart);
        _secondary = e.NameScope.Find<SiteActionButton>(SecondaryPart);
        _primary = e.NameScope.Find<SiteActionButton>(PrimaryPart);
        _menuButton = e.NameScope.Find<SiteMenuButton>(MenuButtonPart);
        _panelLayer = e.NameScope.Find<SiteHeaderFloatLayer>(PanelLayerPart);
        _sheetLayer = e.NameScope.Find<SiteHeaderFloatLayer>(SheetLayerPart);

        if (_inner is not null)
        {
            // The region the pointer must stay in for a panel to stay open: the bar and the panel, whole (Arc: .inner).
            _inner.Background = Brushes.Transparent;
            _inner.PointerEntered += OnRegionPointerEntered;
            _inner.PointerExited += OnRegionPointerExited;
        }
        if (_navList is not null)
        {
            _navList.PointerExited += OnNavListPointerExited;
        }
        if (_brand is not null)
        {
            _brand.Click += OnBrandClick;
        }
        if (_secondary is not null)
        {
            _secondary.Click += OnSecondaryClick;
        }
        if (_primary is not null)
        {
            _primary.Click += OnPrimaryClick;
        }
        if (_menuButton is not null)
        {
            _menuButton.Click += OnMenuButtonClick;
        }
        _resolved = ResolveItems().ToList();
        Rewatch();
        BuildNav();
        UpdateBrand();
        UpdateActions();
        UpdateVariantParts();
        UpdateAutomation();
        UpdateCollapsed();
        UpdatePseudoClasses();
        SyncScrollSource();
    }

    private void Unwire()
    {
        if (_inner is not null)
        {
            _inner.PointerEntered -= OnRegionPointerEntered;
            _inner.PointerExited -= OnRegionPointerExited;
        }
        if (_navList is not null)
        {
            _navList.PointerExited -= OnNavListPointerExited;
        }
        if (_brand is not null)
        {
            _brand.Click -= OnBrandClick;
        }
        if (_secondary is not null)
        {
            _secondary.Click -= OnSecondaryClick;
        }
        if (_primary is not null)
        {
            _primary.Click -= OnPrimaryClick;
        }
        if (_menuButton is not null)
        {
            _menuButton.Click -= OnMenuButtonClick;
        }
        _markBinding?.Dispose();
        _markBinding = null;
        _panelView = null;
        _sheet = null;
        _scrim = null;
    }

    private void UpdateBrand()
    {
        if (_brand is null)
        {
            return;
        }
        _markBinding?.Dispose();
        _markBinding = null;
        Control mark;
        if (BrandMark is null)
        {
            var arch = new SiteArcMark();
            _markBinding = arch.Bind(SiteArcMark.ForegroundProperty, _brand.GetObservable(ForegroundProperty));
            mark = arch;
        }
        else if (BrandMark is IImage image)
        {
            // An image can be drawn by every place that shows the brand; a control can sit in only one.
            mark = new Image { Source = image, Width = 22, Height = 22, VerticalAlignment = VerticalAlignment.Center };
        }
        else
        {
            mark = new ContentPresenter { Content = BrandMark, VerticalAlignment = VerticalAlignment.Center };
        }
        var name = new TextBlock { Text = BrandName, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(name, TextBlock.LetterSpacingProperty, ArcDesignTokenKind.TrackingBody);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(mark);
        row.Children.Add(name);
        _brand.Content = row;
        AutomationProperties.SetName(_brand, BrandName);
    }

    private void UpdateActions()
    {
        if (_secondary is not null)
        {
            _secondary.Label = SecondaryActionLabel;
            _secondary.IsVisible = IsSecondaryActionVisible && !IsCollapsed;
            AutomationProperties.SetName(_secondary, SecondaryActionLabel);
        }
        if (_primary is not null)
        {
            _primary.Label = PrimaryActionLabel;
            _primary.IsVisible = IsPrimaryActionVisible;
            AutomationProperties.SetName(_primary, PrimaryActionLabel);
        }
        if (_menuButton is not null)
        {
            _menuButton.IsVisible = IsCollapsed;
        }
        if (_navList is not null)
        {
            _navList.IsVisible = !IsCollapsed && _resolved.Count > 0;
        }
        if (_sheetLayer is not null)
        {
            _sheetLayer.IsVisible = IsCollapsed;
        }
        if (IsMenuOpen)
        {
            RebuildSheetActions();
        }
    }

    private void UpdateAutomation()
    {
        if (_navList is not null)
        {
            AutomationProperties.SetName(_navList, Label);
            AutomationProperties.SetLandmarkType(_navList, AutomationLandmarkType.Navigation);
        }
        if (_menuButton is not null)
        {
            AutomationProperties.SetName(_menuButton, IsMenuOpen ? CloseMenuLabel : OpenMenuLabel);
            _menuButton.IsExpanded = IsMenuOpen;
        }
        if (_sheetNav is not null)
        {
            AutomationProperties.SetName(_sheetNav, Label);
        }
    }

    private void UpdateVariantParts()
    {
        if (_bar is not null)
        {
            _bar.NavInset = Variant == SiteHeaderVariant.Centered ? 0 : 16;
        }
        UpdatePseudoClasses();
    }

    private void OnVariantChanged()
    {
        if (_navItems is null)
        {
            return;
        }
        if (Variant != SiteHeaderVariant.Mega && _openValue is not null)
        {
            ClosePanelCore(false);
        }
        else if (_openValue is not null)
        {
            RefreshPanelFace();
        }
        UpdateVariantParts();
        BuildNav();
        if (IsMenuOpen)
        {
            RebuildSheet();
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(SolidPseudoClass, IsSolid);
        PseudoClasses.Set(NarrowPseudoClass, IsCollapsed);
        PseudoClasses.Set(CenteredPseudoClass, Variant == SiteHeaderVariant.Centered);
        PseudoClasses.Set(PanelOpenPseudoClass, _openValue is not null);
        PseudoClasses.Set(MenuOpenPseudoClass, IsMenuOpen);
    }

    /// <summary>The bar is solid once the page scrolled, while the sheet is open and while a panel is open.</summary>
    private bool IsSolid => IsScrolled || IsMenuOpen || _openValue is not null;

    private void OnCurrentChanged()
    {
        foreach (var (value, button) in _buttons)
        {
            button.IsCurrent = string.Equals(value, Current, StringComparison.Ordinal);
        }
        UpdateCurrentIndicator(true);
        UpdateSheetCurrent();
    }

    // ---- collapse -------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCollapsed();
    }

    private void UpdateCollapsed()
    {
        var width = Bounds.Width;
        // Before the first layout nothing is known: stay wide (the bar never flashes the menu button).
        var collapsed = width > 0 && width < CollapseWidth;
        if (collapsed == IsCollapsed)
        {
            return;
        }
        IsCollapsed = collapsed;
        if (collapsed)
        {
            // Arc: below the breakpoint the panel closes (the links are in the sheet).
            ClosePanelCore(false);
        }
        else if (IsMenuOpen)
        {
            // Widening closes the sheet and folds its groups.
            IsMenuOpen = false;
        }
        UpdateActions();
        UpdatePseudoClasses();
        UpdateCurrentIndicator(false);
    }

    // ---- choosing -------------------------------------------------------------------------------------------

    private void OnBrandClick(object? sender, RoutedEventArgs e)
        => Choose(SiteHeaderNavigateKind.Brand, BrandName, BrandHref, null, null, null);

    private void OnSecondaryClick(object? sender, RoutedEventArgs e)
    {
        Choose(SiteHeaderNavigateKind.SecondaryAction, SecondaryActionLabel, SecondaryActionHref, null, null, null, closePanel: false);
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        Choose(SiteHeaderNavigateKind.PrimaryAction, PrimaryActionLabel, PrimaryActionHref, null, null, null, closePanel: false);
    }

    private void OnMenuButtonClick(object? sender, RoutedEventArgs e) => IsMenuOpen = !IsMenuOpen;

    /// <summary>
    /// Arc's <c>choose</c>: sets <see cref="Current"/> to the item the destination belongs to, reports it, closes the panel
    /// and the sheet. Actions (no item) only close the sheet.
    /// </summary>
    internal void Choose(SiteHeaderNavigateKind kind, string label, string? href, SiteHeaderItem? item, SiteHeaderLink? link,
                         SiteHeaderFeature? feature, bool closePanel = true)
    {
        if (item is not null)
        {
            Current = item.Value;
        }
        var args = new SiteHeaderNavigateEventArgs(NavigateEvent, this, kind, label, href, item, link, feature);
        RaiseEvent(args);
        if (NavigateCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
        if (!args.Handled)
        {
            SiteLinks.TryLaunch(this, href);
        }
        if (closePanel)
        {
            ClosePanelCore(false);
        }
        CloseMenuCore(false);
    }

    private void OnNavClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not SiteNavButton button || ItemOf(button) is not { } item)
        {
            return;
        }
        if (HasPanel(item))
        {
            // A trigger toggles its panel; pending hover timers must not undo the click.
            StopTimers();
            if (string.Equals(_openValue, item.Value, StringComparison.Ordinal))
            {
                ClosePanelCore(false);
            }
            else
            {
                OpenPanelCore(item.Value, false);
            }
            return;
        }
        Choose(SiteHeaderNavigateKind.Item, item.Label, item.Href, item, null, null);
    }
}
