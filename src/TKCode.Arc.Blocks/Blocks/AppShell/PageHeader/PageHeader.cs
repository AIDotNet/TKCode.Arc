using System.Collections;
using System.Collections.Specialized;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="PageHeader.BreadcrumbClick"/>.</summary>
public sealed class PageHeaderBreadcrumbClickEventArgs : RoutedEventArgs
{
    public PageHeaderBreadcrumbClickEventArgs(RoutedEvent routedEvent, object? item, int index) : base(routedEvent)
    {
        Item = item;
        Index = index;
    }

    /// <summary>The chosen item of <see cref="PageHeader.Breadcrumbs"/>.</summary>
    public object? Item { get; }

    /// <summary>Index of the item in <see cref="PageHeader.Breadcrumbs"/> (0 is the root).</summary>
    public int Index { get; }
}

/// <summary>Arguments of <see cref="PageHeader.ActionInvoked"/>.</summary>
public sealed class PageHeaderActionEventArgs : RoutedEventArgs
{
    public PageHeaderActionEventArgs(RoutedEvent routedEvent, PageHeaderAction action, bool isFromMenu) : base(routedEvent)
    {
        Action = action;
        IsFromMenu = isFromMenu;
    }

    /// <summary>The chosen action.</summary>
    public PageHeaderAction Action { get; }

    /// <summary>True when it was chosen from the "More actions" menu rather than its button.</summary>
    public bool IsFromMenu { get; }
}

/// <summary>
/// The frame of a project or record page (Arc page header block): a fixed header with breadcrumbs, a large title with
/// a status badge, a description, a meta row, actions and tabs, over the scrolling body of the selected tab. Scrolling
/// the body past 16px folds the header into a compact bar (the breadcrumbs give way to the title and its status, which
/// scrolls back to the top when chosen); it opens again only near the top (4px), so it never flickers. Secondary
/// actions fold into the "More actions" menu when the breadcrumbs would get less than 240px, and return at 272px.
/// Tab counts roll digit by digit, a 2px indicator glides between tabs, and panels slide in from the side of travel.
/// Breakpoints follow the control's own width: below 560px the padding tightens, the root crumb and the compact
/// badge hide and the title steps down a size; below 460px the primary action shows only its icon.
/// <para>
/// Ported from Arc's free page-header block (MIT, Copyright (c) 2026 Elia Kuratli) and made data driven: tabs are
/// <see cref="PageHeaderTab"/> items (or data items with <see cref="ItemsControl.ItemTemplate"/> and
/// <see cref="ContentTemplate"/>), actions are <see cref="PageHeaderAction"/>s.
/// </para>
/// </summary>
[TemplatePart(BarPart, typeof(Grid))]
[TemplatePart(BreadcrumbPart, typeof(Breadcrumb))]
[TemplatePart(CompactTitlePart, typeof(AvaloniaButton))]
[TemplatePart(SecondarySlotPart, typeof(Decorator))]
[TemplatePart(SecondaryPanelPart, typeof(Panel))]
[TemplatePart(TrailingPart, typeof(Panel))]
[TemplatePart(MoreButtonPart, typeof(Button))]
[TemplatePart(PrimaryButtonPart, typeof(Button))]
[TemplatePart(IntroPart, typeof(SpringSizeDecorator))]
[TemplatePart(TitleRowPart, typeof(Control))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DetailsPart, typeof(Control))]
[TemplatePart(TabsViewportPart, typeof(ScrollViewer))]
[TemplatePart(TabStripPart, typeof(Panel))]
[TemplatePart(IndicatorPart, typeof(GlidingIndicator))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(BodyPart, typeof(Control))]
[PseudoClasses(CondensedPseudoClass, ActionsFoldedPseudoClass, NarrowPseudoClass, TightPseudoClass)]
public class PageHeader : SelectingItemsControl
{
    public const string BarPart = "PART_Bar";
    public const string BreadcrumbPart = "PART_Breadcrumb";
    public const string CompactTitlePart = "PART_CompactTitle";
    public const string SecondarySlotPart = "PART_SecondarySlot";
    public const string SecondaryPanelPart = "PART_SecondaryPanel";
    public const string TrailingPart = "PART_Trailing";
    public const string MoreButtonPart = "PART_MoreButton";
    public const string PrimaryButtonPart = "PART_PrimaryButton";
    public const string IntroPart = "PART_Intro";
    public const string TitleRowPart = "PART_TitleRow";
    public const string TitlePart = "PART_Title";
    public const string DetailsPart = "PART_Details";
    public const string TabsViewportPart = "PART_TabsViewport";
    public const string TabStripPart = "PART_TabStrip";
    public const string IndicatorPart = "PART_Indicator";
    public const string ScrollerPart = "PART_Scroller";
    public const string BodyPart = "PART_Body";

    /// <summary>Set while the header is folded into the compact bar.</summary>
    public const string CondensedPseudoClass = ":condensed";

    /// <summary>Set while the secondary actions are folded into the "More actions" menu.</summary>
    public const string ActionsFoldedPseudoClass = ":actions-folded";

    /// <summary>Set while the control is narrower than 560px.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the control is narrower than 460px (the primary action shows only its icon).</summary>
    public const string TightPseudoClass = ":tight";

    /// <summary>The header condenses once the body scrolls past this offset ...</summary>
    internal const double CondenseAt = 16;

    /// <summary>... and opens again only at or above this one.</summary>
    internal const double ExpandAt = 4;

    /// <summary>Secondary actions fold when the lead cell would get less room than this ...</summary>
    internal const double LeadMin = 240;

    /// <summary>... and return with a little slack.</summary>
    internal const double LeadReturn = 272;

    /// <summary>Below this width the secondary actions start folded until the first measurement (nothing flashes).</summary>
    internal const double PreMeasureFoldWidth = 720;

    internal const double NarrowWidth = 560;
    internal const double TightWidth = 460;

    /// <summary>Margin kept around a tab scrolled into view in the tab row (Arc: 12px).</summary>
    internal const double TabRevealMargin = 12;

    #region Properties

    /// <summary>The page title: the large heading, and the compact bar's title once the header condenses.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Title));

    /// <summary>One or two sentences under the title (wraps at about 60 characters).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Description));

    /// <summary>Status shown as a badge with a dot beside the title and in the compact bar; null hides it.</summary>
    public static readonly StyledProperty<string?> StatusProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Status));

    /// <summary>Tone of the status badge (default <see cref="BadgeTone.Success"/>).</summary>
    public static readonly StyledProperty<BadgeTone> StatusToneProperty =
        AvaloniaProperty.Register<PageHeader, BadgeTone>(nameof(StatusTone), BadgeTone.Success);

    /// <summary>
    /// The path to this page, root first; the last item is the current page. Strings, data items or
    /// <see cref="BreadcrumbItem"/>s, shown by a <see cref="Breadcrumb"/>. Choosing one raises <see cref="BreadcrumbClick"/>.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> BreadcrumbsProperty =
        AvaloniaProperty.Register<PageHeader, IEnumerable?>(nameof(Breadcrumbs));

    /// <summary>Meta row under the description (members, owner, dates): any content, drawn in the extra-small muted type.</summary>
    public static readonly StyledProperty<object?> MetaProperty =
        AvaloniaProperty.Register<PageHeader, object?>(nameof(Meta));

    /// <summary>The main action: a small primary button at the end of the header row (icon only below 460px).</summary>
    public static readonly StyledProperty<PageHeaderAction?> PrimaryActionProperty =
        AvaloniaProperty.Register<PageHeader, PageHeaderAction?>(nameof(PrimaryAction));

    /// <summary>Template for the body of data items (the tab label uses <see cref="ItemsControl.ItemTemplate"/>).</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<PageHeader, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>Accessible name of the overflow button (default "More actions").</summary>
    public static readonly StyledProperty<string> MoreActionsLabelProperty =
        AvaloniaProperty.Register<PageHeader, string>(nameof(MoreActionsLabel), "More actions");

    /// <summary>Added to the title in the accessible name of the compact bar (default "back to top").</summary>
    public static readonly StyledProperty<string> BackToTopLabelProperty =
        AvaloniaProperty.Register<PageHeader, string>(nameof(BackToTopLabel), "back to top");

    /// <summary>Accessible name of the tab row (default "Sections").</summary>
    public static readonly StyledProperty<string> TabsLabelProperty =
        AvaloniaProperty.Register<PageHeader, string>(nameof(TabsLabel), "Sections");

    /// <summary>True while the header is folded into the compact bar (the body scrolled past 16px, until it is back within 4px).</summary>
    public static readonly DirectProperty<PageHeader, bool> IsCondensedProperty =
        AvaloniaProperty.RegisterDirect<PageHeader, bool>(nameof(IsCondensed), o => o.IsCondensed);

    /// <summary>True while the secondary <see cref="Actions"/> are folded into the "More actions" menu.</summary>
    public static readonly DirectProperty<PageHeader, bool> AreActionsFoldedProperty =
        AvaloniaProperty.RegisterDirect<PageHeader, bool>(nameof(AreActionsFolded), o => o.AreActionsFolded);

    /// <summary>Raised when a breadcrumb is chosen (the current page is not a link).</summary>
    public static readonly RoutedEvent<PageHeaderBreadcrumbClickEventArgs> BreadcrumbClickEvent =
        RoutedEvent.Register<PageHeader, PageHeaderBreadcrumbClickEventArgs>(nameof(BreadcrumbClick), RoutingStrategies.Bubble);

    /// <summary>Raised when an action is chosen from its button or the "More actions" menu.</summary>
    public static readonly RoutedEvent<PageHeaderActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<PageHeader, PageHeaderActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> IndicatorInsetProperty =
        AvaloniaProperty.Register<PageHeader, double>(nameof(IndicatorInset), 10);

    internal static readonly StyledProperty<double> IndicatorHeightProperty =
        AvaloniaProperty.Register<PageHeader, double>(nameof(IndicatorHeight), 2);

    internal static readonly StyledProperty<double> TabsFadeWidthProperty =
        AvaloniaProperty.Register<PageHeader, double>(nameof(TabsFadeWidth), 28);

    internal static readonly StyledProperty<double> ActionGlyphSizeProperty =
        AvaloniaProperty.Register<PageHeader, double>(nameof(ActionGlyphSize), 16);

    internal static readonly StyledProperty<double> ActionContentGapProperty =
        AvaloniaProperty.Register<PageHeader, double>(nameof(ActionContentGap), 8);

    /// <summary>The page title: the large heading, and the compact bar's title once the header condenses.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>One or two sentences under the title (wraps at about 60 characters).</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Status shown as a badge with a dot beside the title and in the compact bar; null hides it.</summary>
    public string? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>Tone of the status badge (default <see cref="BadgeTone.Success"/>).</summary>
    public BadgeTone StatusTone
    {
        get => GetValue(StatusToneProperty);
        set => SetValue(StatusToneProperty, value);
    }

    /// <summary>
    /// The path to this page, root first; the last item is the current page. Strings, data items or
    /// <see cref="BreadcrumbItem"/>s, shown by a <see cref="Breadcrumb"/>. Choosing one raises <see cref="BreadcrumbClick"/>.
    /// </summary>
    public IEnumerable? Breadcrumbs
    {
        get => GetValue(BreadcrumbsProperty);
        set => SetValue(BreadcrumbsProperty, value);
    }

    /// <summary>Meta row under the description (members, owner, dates): any content, drawn in the extra-small muted type.</summary>
    public object? Meta
    {
        get => GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    /// <summary>The main action: a small primary button at the end of the header row (icon only below 460px).</summary>
    public PageHeaderAction? PrimaryAction
    {
        get => GetValue(PrimaryActionProperty);
        set => SetValue(PrimaryActionProperty, value);
    }

    /// <summary>Template for the body of data items (the tab label uses <see cref="ItemsControl.ItemTemplate"/>).</summary>
    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>Accessible name of the overflow button (default "More actions").</summary>
    public string MoreActionsLabel
    {
        get => GetValue(MoreActionsLabelProperty);
        set => SetValue(MoreActionsLabelProperty, value);
    }

    /// <summary>Added to the title in the accessible name of the compact bar (default "back to top").</summary>
    public string BackToTopLabel
    {
        get => GetValue(BackToTopLabelProperty);
        set => SetValue(BackToTopLabelProperty, value);
    }

    /// <summary>Accessible name of the tab row (default "Sections").</summary>
    public string TabsLabel
    {
        get => GetValue(TabsLabelProperty);
        set => SetValue(TabsLabelProperty, value);
    }

    private bool _isCondensed;

    /// <summary>True while the header is folded into the compact bar (the body scrolled past 16px, until it is back within 4px).</summary>
    public bool IsCondensed
    {
        get => _isCondensed;
        private set => SetAndRaise(IsCondensedProperty, ref _isCondensed, value);
    }

    private bool _areActionsFolded;

    /// <summary>True while the secondary <see cref="Actions"/> are folded into the "More actions" menu.</summary>
    public bool AreActionsFolded
    {
        get => _areActionsFolded;
        private set => SetAndRaise(AreActionsFoldedProperty, ref _areActionsFolded, value);
    }

    /// <summary>Secondary actions: small secondary buttons before the overflow button; they fold into the top of its menu when room runs out.</summary>
    public AvaloniaList<PageHeaderAction> Actions { get; } = new();

    /// <summary>Actions that always live in the "More actions" menu (after the folded secondary actions, separated by a line).</summary>
    public AvaloniaList<PageHeaderAction> MoreActions { get; } = new();

    /// <summary>Raised when a breadcrumb is chosen (the current page is not a link).</summary>
    public event EventHandler<PageHeaderBreadcrumbClickEventArgs>? BreadcrumbClick
    {
        add => AddHandler(BreadcrumbClickEvent, value);
        remove => RemoveHandler(BreadcrumbClickEvent, value);
    }

    /// <summary>Raised when an action is chosen from its button or the "More actions" menu.</summary>
    public event EventHandler<PageHeaderActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    internal double IndicatorInset
    {
        get => GetValue(IndicatorInsetProperty);
        set => SetValue(IndicatorInsetProperty, value);
    }

    internal double IndicatorHeight
    {
        get => GetValue(IndicatorHeightProperty);
        set => SetValue(IndicatorHeightProperty, value);
    }

    internal double TabsFadeWidth
    {
        get => GetValue(TabsFadeWidthProperty);
        set => SetValue(TabsFadeWidthProperty, value);
    }

    internal double ActionGlyphSize
    {
        get => GetValue(ActionGlyphSizeProperty);
        set => SetValue(ActionGlyphSizeProperty, value);
    }

    internal double ActionContentGap
    {
        get => GetValue(ActionContentGapProperty);
        set => SetValue(ActionContentGapProperty, value);
    }

    #endregion

    private Grid? _bar;
    private Breadcrumb? _breadcrumb;
    private PageHeaderCompactTitle? _compact;
    private PageHeaderFoldSlot? _secondarySlot;
    private Panel? _secondaryPanel;
    private Panel? _trailing;
    private Button? _moreButton;
    private Button? _primaryButton;
    private SpringSizeDecorator? _intro;
    private Control? _titleRow;
    private TextBlock? _title;
    private Control? _details;
    private ScrollViewer? _tabsViewport;
    private PageHeaderTabStrip? _tabStrip;
    private GlidingIndicator? _indicator;
    private ScrollViewer? _scroller;
    private PageHeaderBody? _body;

    private PageHeaderSwap? _crumbSwap;
    private PageHeaderSwap? _compactSwap;
    private PageHeaderSwap? _titleSwap;
    private PageHeaderSwap? _detailsSwap;

    private MenuSurface? _menu;
    private readonly Dictionary<PageHeaderAction, Button> _secondaryButtons = new();
    private readonly Dictionary<PageHeaderAction, MenuEntry> _menuEntries = new();
    private readonly HashSet<PageHeaderAction> _watched = new();
    private TextBlock? _primaryLabel;
    private bool _primaryHasIcon;

    private MotionValue<double>? _pageScroll;
    private MotionValue<double>? _tabScroll;
    private Rect _indicatorTarget;
    private int _focusIndex = -1;
    private bool _revealPending;
    private bool _foldMeasured;
    private bool _foldDirty;
    private double _lastBarWidth = -1;
    private bool _isNarrow;
    private bool _isTight;
    private double _tabsMaskWidth = -1;
    private INotifyCollectionChanged? _breadcrumbSource;

    static PageHeader()
    {
        SelectionModeProperty.OverrideDefaultValue<PageHeader>(SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<PageHeader>(false);
        // The tab row reveals the selected tab itself; the stock scroll-into-view would also scroll an outer page.
        AutoScrollToSelectedItemProperty.OverrideDefaultValue<PageHeader>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<PageHeader>(AutomationLandmarkType.Region);
    }

    public PageHeader()
    {
        Actions.CollectionChanged += OnActionsChanged;
        MoreActions.CollectionChanged += OnActionsChanged;
    }

    #region Public methods

    /// <summary>
    /// Scrolls the body back to the top (smoothly with full motion) and moves focus to the title. The compact bar
    /// does this when it is chosen.
    /// </summary>
    public void ScrollToTop()
    {
        ScrollPageTo(0, smooth: true);
        if (_title is not null)
        {
            _title.Focus(NavigationMethod.Unspecified);
        }
    }

    /// <summary>
    /// Selects the tab at <paramref name="index"/> and shows the top of its body, keeping the compact bar when the
    /// header is condensed (Arc: after creating an issue or sharing an update). Returns false for a missing or disabled tab.
    /// </summary>
    public bool RevealTab(int index)
    {
        if (index < 0 || index >= ItemCount || !IsTabEnabled(index))
        {
            return false;
        }
        SelectedIndex = index;
        RevealBodyTop();
        return true;
    }

    #endregion

    #region Containers

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new PageHeaderTab();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<PageHeaderTab>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is PageHeaderTab tab && !ReferenceEquals(container, item) && ContentTemplate is { } template &&
            !tab.IsSet(ContentControl.ContentTemplateProperty))
        {
            tab.ContentTemplate = template;
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (index == SelectedIndex)
        {
            UpdateContent(0, animate: false);
        }
        UpdateTabStops();
        UpdateTabsVisibility();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        UpdateTabStops();
        UpdateTabsVisibility();
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Unhook();
        base.OnApplyTemplate(e);

        _bar = e.NameScope.Find<Grid>(BarPart);
        _breadcrumb = e.NameScope.Find<Breadcrumb>(BreadcrumbPart);
        _compact = e.NameScope.Find<PageHeaderCompactTitle>(CompactTitlePart);
        _secondarySlot = e.NameScope.Find<PageHeaderFoldSlot>(SecondarySlotPart);
        _secondaryPanel = e.NameScope.Find<Panel>(SecondaryPanelPart);
        _trailing = e.NameScope.Find<Panel>(TrailingPart);
        _moreButton = e.NameScope.Find<Button>(MoreButtonPart);
        _primaryButton = e.NameScope.Find<Button>(PrimaryButtonPart);
        _intro = e.NameScope.Find<SpringSizeDecorator>(IntroPart);
        _titleRow = e.NameScope.Find<Control>(TitleRowPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _details = e.NameScope.Find<Control>(DetailsPart);
        _tabsViewport = e.NameScope.Find<ScrollViewer>(TabsViewportPart);
        _tabStrip = e.NameScope.Find<PageHeaderTabStrip>(TabStripPart);
        _indicator = e.NameScope.Find<GlidingIndicator>(IndicatorPart);
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        _body = e.NameScope.Find<PageHeaderBody>(BodyPart);
        _indicatorTarget = default;
        _crumbSwap = null;
        _compactSwap = null;
        _titleSwap = null;
        _detailsSwap = null;
        _foldMeasured = false;
        _lastBarWidth = -1;
        _tabsMaskWidth = -1;
        _pageScroll = null;
        _tabScroll = null;

        if (_bar is not null)
        {
            _bar.SizeChanged += OnBarSizeChanged;
        }
        if (_breadcrumb is not null)
        {
            _breadcrumb.ItemClick += OnBreadcrumbItemClick;
            _crumbSwap = new PageHeaderSwap(_breadcrumb, hiddenY: -8, hiddenBlur: 2);
        }
        if (_compact is not null)
        {
            _compact.Click += OnCompactClick;
            _compactSwap = new PageHeaderSwap(_compact, hiddenY: 14, hiddenBlur: 4);
        }
        if (_titleRow is not null)
        {
            _titleSwap = new PageHeaderSwap(_titleRow, hiddenY: -10, hiddenScale: 0.62, hiddenBlur: 2);
        }
        if (_details is not null)
        {
            _detailsSwap = new PageHeaderSwap(_details, hiddenY: -6);
        }
        if (_moreButton is not null)
        {
            DropdownMenu.SetMenu(_moreButton, EnsureMenu());
        }
        if (_primaryButton is not null)
        {
            _primaryButton.Click += OnPrimaryClick;
        }
        if (_tabsViewport is not null)
        {
            _tabsViewport.SizeChanged += OnTabsViewportSizeChanged;
            _tabsViewport.AddHandler(PointerWheelChangedEvent, OnTabsWheel, RoutingStrategies.Tunnel);
        }
        if (_tabStrip is not null)
        {
            _tabStrip.Arranged = OnTabStripArranged;
        }
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
            _scroller.AddHandler(PointerWheelChangedEvent, OnPageWheel, RoutingStrategies.Tunnel);
        }

        _revealPending = true;
        RebuildSecondary();
        UpdatePrimary();
        RebuildMenu();
        UpdateBreadcrumbs();
        UpdateNames();
        UpdateTabsVisibility();
        UpdateCondensedVisuals(animate: false);
        UpdateContent(0, animate: false);
    }

    private void Unhook()
    {
        if (_bar is not null)
        {
            _bar.SizeChanged -= OnBarSizeChanged;
        }
        if (_breadcrumb is not null)
        {
            _breadcrumb.ItemClick -= OnBreadcrumbItemClick;
            _breadcrumb.ItemsSource = null;
        }
        if (_compact is not null)
        {
            _compact.Click -= OnCompactClick;
        }
        if (_moreButton is not null)
        {
            DropdownMenu.SetMenu(_moreButton, null);
        }
        if (_primaryButton is not null)
        {
            _primaryButton.Click -= OnPrimaryClick;
            _primaryButton.Content = null;
        }
        if (_tabsViewport is not null)
        {
            _tabsViewport.SizeChanged -= OnTabsViewportSizeChanged;
            _tabsViewport.RemoveHandler(PointerWheelChangedEvent, OnTabsWheel);
        }
        if (_tabStrip is not null)
        {
            _tabStrip.Arranged = null;
        }
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
            _scroller.RemoveHandler(PointerWheelChangedEvent, OnPageWheel);
        }
        foreach (var button in _secondaryButtons.Values)
        {
            button.Click -= OnSecondaryClick;
        }
        _secondaryButtons.Clear();
        _secondaryPanel?.Children.Clear();
        _pageScroll?.Stop();
        _tabScroll?.Stop();
    }

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            var from = change.GetOldValue<int>();
            var to = change.GetNewValue<int>();
            var direction = from >= 0 && to >= 0 && from != to ? Math.Sign(to - from) : 0;
            _focusIndex = to;
            UpdateContent(direction, animate: true);
            UpdateIndicator(animate: true);
            UpdateTabStops();
            RevealTabInRow(to, instant: false);
            if (from >= 0 && to >= 0 && from != to)
            {
                // Switching tabs while condensed shows the top of the new panel and keeps the compact bar.
                RevealBodyTop();
            }
        }
        else if (change.Property == ContentTemplateProperty)
        {
            UpdateContent(0, animate: false);
        }
        else if (change.Property == IsCondensedProperty)
        {
            PseudoClasses.Set(CondensedPseudoClass, IsCondensed);
            UpdateCondensedVisuals(animate: true);
        }
        else if (change.Property == AreActionsFoldedProperty)
        {
            PseudoClasses.Set(ActionsFoldedPseudoClass, AreActionsFolded);
        }
        else if (change.Property == BreadcrumbsProperty)
        {
            if (_breadcrumbSource is not null)
            {
                _breadcrumbSource.CollectionChanged -= OnBreadcrumbSourceChanged;
            }
            _breadcrumbSource = change.NewValue as INotifyCollectionChanged;
            if (_breadcrumbSource is not null)
            {
                _breadcrumbSource.CollectionChanged += OnBreadcrumbSourceChanged;
            }
            UpdateBreadcrumbs();
        }
        else if (change.Property == PrimaryActionProperty)
        {
            if (change.OldValue is PageHeaderAction old)
            {
                Unwatch(old);
            }
            if (change.NewValue is PageHeaderAction action)
            {
                Watch(action);
            }
            UpdatePrimary();
        }
        else if (change.Property == TitleProperty || change.Property == BackToTopLabelProperty || change.Property == TabsLabelProperty ||
                 change.Property == MoreActionsLabelProperty)
        {
            UpdateNames();
        }
        else if (change.Property == ActionGlyphSizeProperty || change.Property == ActionContentGapProperty)
        {
            RebuildSecondary();
            UpdatePrimary();
        }
    }

    private void UpdateNames()
    {
        AutomationProperties.SetName(this, Title ?? string.Empty);
        if (_compact is not null)
        {
            AutomationProperties.SetName(_compact, string.IsNullOrEmpty(Title) ? BackToTopLabel : $"{Title}, {BackToTopLabel}");
        }
        if (_tabsViewport is not null)
        {
            AutomationProperties.SetName(_tabsViewport, TabsLabel);
        }
        if (_moreButton is not null)
        {
            AutomationProperties.SetName(_moreButton, MoreActionsLabel);
        }
    }

    #endregion

    #region Layout and breakpoints

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsInfinity(availableSize.Width))
        {
            UpdateBreakpoints(availableSize.Width);
            if (!_foldMeasured && _secondarySlot is not null && Actions.Count > 0)
            {
                // Before the first measurement, narrow headers already hide the actions that will fold.
                var folded = availableSize.Width < PreMeasureFoldWidth;
                if (folded != _secondarySlot.IsFolded)
                {
                    _secondarySlot.SetFolded(folded, animate: false);
                }
            }
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    private void UpdateBreakpoints(double width)
    {
        var narrow = width < NarrowWidth;
        var tight = width < TightWidth;
        if (narrow != _isNarrow)
        {
            _isNarrow = narrow;
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            UpdateBreadcrumbs();
            UpdateTabsMask();
        }
        if (tight != _isTight)
        {
            _isTight = tight;
            PseudoClasses.Set(TightPseudoClass, tight);
            UpdatePrimaryCompact();
        }
    }

    private void OnBarSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged || !_foldMeasured)
        {
            EvaluateFold(force: false);
        }
    }

    /// <summary>
    /// Only the header row width is observed, so a label that grows (Follow to Following) never folds the button that
    /// was just pressed. The first measurement applies at once; later width changes fold with the spring.
    /// </summary>
    private void EvaluateFold(bool force)
    {
        if (_bar is null || _secondarySlot is null || _trailing is null)
        {
            return;
        }
        var width = _bar.Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        if (!force && _foldMeasured && Math.Abs(width - _lastBarWidth) < 0.5)
        {
            return;
        }
        _lastBarWidth = width;
        var room = width - _secondarySlot.NaturalWidth - _trailing.Bounds.Width - _bar.ColumnSpacing;
        var folded = Actions.Count > 0 && (_foldMeasured && AreActionsFolded ? room < LeadReturn : room < LeadMin);
        var animate = _foldMeasured;
        _foldMeasured = true;
        if (folded != AreActionsFolded || folded != _secondarySlot.IsFolded)
        {
            _secondarySlot.SetFolded(folded, animate);
            AreActionsFolded = folded;
            RebuildMenu();
        }
    }

    private void RequestFoldEvaluation()
    {
        if (_foldDirty)
        {
            return;
        }
        _foldDirty = true;
        LayoutUpdated += OnLayoutUpdatedForFold;
        InvalidateMeasure();
    }

    private void OnLayoutUpdatedForFold(object? sender, EventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdatedForFold;
        _foldDirty = false;
        EvaluateFold(force: true);
    }

    #endregion

    #region Condensing

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroller is null)
        {
            return;
        }
        var top = _scroller.Offset.Y;
        if (IsCondensed)
        {
            if (top <= ExpandAt)
            {
                IsCondensed = false;
            }
            return;
        }
        if (top > CondenseAt && CanStayCondensed())
        {
            IsCondensed = true;
        }
    }

    /// <summary>
    /// Condensing gives the body the intro's height. The header only condenses when the body is long enough to stay
    /// scrolled past the expand line afterwards, so it never clamps back to the top and flickers (Arc keeps every
    /// panel taller than the condensed viewport for the same reason).
    /// </summary>
    private bool CanStayCondensed()
    {
        if (_scroller is null)
        {
            return false;
        }
        var introHeight = _intro?.Child?.DesiredSize.Height ?? 0;
        var maxAfter = _scroller.Extent.Height - _scroller.Viewport.Height - introHeight;
        return maxAfter > ExpandAt;
    }

    private void UpdateCondensedVisuals(bool animate)
    {
        var condensed = IsCondensed;
        _crumbSwap?.Set(!condensed, animate);
        _compactSwap?.Set(condensed, animate);
        _titleSwap?.Set(!condensed, animate);
        _detailsSwap?.Set(!condensed, animate);
        if (_intro is not null)
        {
            _intro.IsExpanded = !condensed;
            KeyboardNavigation.SetTabNavigation(_intro, condensed ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        }
    }

    private void OnCompactClick(object? sender, RoutedEventArgs e) => ScrollToTop();

    private void RevealBodyTop()
    {
        if (_scroller is { } scroller && scroller.Offset.Y > CondenseAt + 1)
        {
            _pageScroll?.Stop();
            scroller.Offset = new Vector(scroller.Offset.X, CondenseAt + 1);
        }
    }

    private void ScrollPageTo(double y, bool smooth)
    {
        if (_scroller is not { } scroller)
        {
            return;
        }
        if (_pageScroll is null)
        {
            _pageScroll = new MotionValue<double>(this, scroller.Offset.Y, MotionVectors.Double);
            _pageScroll.Changed += value =>
            {
                if (_scroller is not null)
                {
                    _scroller.Offset = new Vector(_scroller.Offset.X, Math.Max(0, value));
                }
            };
        }
        if (!_pageScroll.IsAnimating)
        {
            _pageScroll.Jump(scroller.Offset.Y);
        }
        if (!smooth)
        {
            _pageScroll.Jump(y);
            return;
        }
        // Arc scrolls smoothly (behavior "smooth"): here the smooth spring; Reduced and Off jump (MotionValue honors the level).
        _ = _pageScroll.AnimateTo(y, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private void OnPageWheel(object? sender, PointerWheelEventArgs e) => _pageScroll?.Stop();

    #endregion

    #region Breadcrumbs

    private void OnBreadcrumbSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isNarrow)
        {
            UpdateBreadcrumbs();
        }
    }

    /// <summary>Below 560px the root crumb is left out (Arc hides it); the breadcrumb shows the rest of the path.</summary>
    private void UpdateBreadcrumbs()
    {
        if (_breadcrumb is null)
        {
            return;
        }
        var source = Breadcrumbs;
        _breadcrumb.IsVisible = source is not null;
        if (source is null)
        {
            _breadcrumb.ItemsSource = null;
            return;
        }
        if (_isNarrow)
        {
            var rest = new List<object?>();
            var index = 0;
            foreach (var item in source)
            {
                if (index++ > 0)
                {
                    rest.Add(item);
                }
            }
            _breadcrumb.ItemsSource = index > 1 ? rest : source;
        }
        else
        {
            _breadcrumb.ItemsSource = source;
        }
    }

    private int RootOffset
    {
        get
        {
            if (!_isNarrow || Breadcrumbs is null)
            {
                return 0;
            }
            var count = 0;
            foreach (var _ in Breadcrumbs)
            {
                if (++count > 1)
                {
                    return 1;
                }
            }
            return 0;
        }
    }

    private void OnBreadcrumbItemClick(object? sender, BreadcrumbItemClickEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new PageHeaderBreadcrumbClickEventArgs(BreadcrumbClickEvent, e.Item, e.Index + RootOffset));
    }

    #endregion

    #region Actions

    private void OnActionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncWatched();
        if (ReferenceEquals(sender, Actions))
        {
            RebuildSecondary();
            RequestFoldEvaluation();
        }
        RebuildMenu();
    }

    private void SyncWatched()
    {
        var wanted = new HashSet<PageHeaderAction>(Actions);
        wanted.UnionWith(MoreActions);
        if (PrimaryAction is { } primary)
        {
            wanted.Add(primary);
        }
        foreach (var action in _watched.ToArray())
        {
            if (!wanted.Contains(action))
            {
                Unwatch(action);
            }
        }
        foreach (var action in wanted)
        {
            Watch(action);
        }
    }

    private void Watch(PageHeaderAction action)
    {
        if (_watched.Add(action))
        {
            action.PropertyChanged += OnActionPropertyChanged;
            action.StateChanged += OnActionStateChanged;
        }
    }

    private void Unwatch(PageHeaderAction action)
    {
        if (ReferenceEquals(action, PrimaryAction) || Actions.Contains(action) || MoreActions.Contains(action))
        {
            return;
        }
        if (_watched.Remove(action))
        {
            action.PropertyChanged -= OnActionPropertyChanged;
            action.StateChanged -= OnActionStateChanged;
        }
    }

    private void OnActionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not PageHeaderAction action)
        {
            return;
        }
        var p = e.Property;
        if (p == PageHeaderAction.LabelProperty || p == PageHeaderAction.IconProperty)
        {
            if (_secondaryButtons.TryGetValue(action, out var button))
            {
                // A new content object: the button blurs the old label out, the new one in, and springs its width.
                button.Content = BuildContent(action, out _);
                AutomationProperties.SetName(button, action.Label ?? string.Empty);
            }
            if (ReferenceEquals(action, PrimaryAction))
            {
                UpdatePrimary();
            }
        }
        if (p == PageHeaderAction.LabelProperty || p == PageHeaderAction.IconProperty || p == PageHeaderAction.MenuLabelProperty ||
            p == PageHeaderAction.MenuIconProperty || p == PageHeaderAction.IsDestructiveProperty)
        {
            if (_menuEntries.TryGetValue(action, out var entry))
            {
                UpdateEntry(entry, action);
            }
        }
        else if (p == PageHeaderAction.IsLoadingProperty)
        {
            if (_secondaryButtons.TryGetValue(action, out var button))
            {
                button.IsLoading = action.IsLoading;
            }
            if (ReferenceEquals(action, PrimaryAction) && _primaryButton is not null)
            {
                _primaryButton.IsLoading = action.IsLoading;
            }
        }
    }

    private void OnActionStateChanged(object? sender, EventArgs e)
    {
        if (sender is not PageHeaderAction action)
        {
            return;
        }
        if (_secondaryButtons.TryGetValue(action, out var button))
        {
            button.IsEnabled = IsButtonEnabled(action);
        }
        if (ReferenceEquals(action, PrimaryAction) && _primaryButton is not null)
        {
            _primaryButton.IsEnabled = IsButtonEnabled(action);
        }
        if (_menuEntries.TryGetValue(action, out var entry))
        {
            entry.IsEnabled = action.CanExecute;
        }
    }

    /// <summary>A loading button stays enabled (it keeps focus and swallows clicks); disabled actions and commands that cannot run disable it.</summary>
    private static bool IsButtonEnabled(PageHeaderAction action)
        => action.IsEnabled && (action.Command is not { } command || command.CanExecute(action.CommandParameter));

    private void RebuildSecondary()
    {
        if (_secondaryPanel is null)
        {
            return;
        }
        foreach (var old in _secondaryButtons.Values)
        {
            old.Click -= OnSecondaryClick;
        }
        _secondaryButtons.Clear();
        _secondaryPanel.Children.Clear();
        foreach (var action in Actions)
        {
            if (_secondaryButtons.ContainsKey(action))
            {
                continue;
            }
            var button = new Button
            {
                Variant = ButtonVariant.Secondary,
                Size = ControlSize.Small,
                Tag = action,
                IsEnabled = IsButtonEnabled(action),
                IsLoading = action.IsLoading,
                Content = BuildContent(action, out _)
            };
            AutomationProperties.SetName(button, action.Label ?? string.Empty);
            button.Click += OnSecondaryClick;
            _secondaryButtons[action] = button;
            _secondaryPanel.Children.Add(button);
        }
        SyncWatched();
    }

    private void UpdatePrimary()
    {
        if (_primaryButton is null)
        {
            return;
        }
        var action = PrimaryAction;
        _primaryButton.IsVisible = action is not null;
        if (action is null)
        {
            _primaryButton.Content = null;
            _primaryLabel = null;
            return;
        }
        _primaryButton.Content = BuildContent(action, out _primaryLabel);
        _primaryHasIcon = action.Icon is not null;
        _primaryButton.IsEnabled = IsButtonEnabled(action);
        _primaryButton.IsLoading = action.IsLoading;
        AutomationProperties.SetName(_primaryButton, action.Label ?? string.Empty);
        UpdatePrimaryCompact();
    }

    /// <summary>Below 460px the primary action shows only its icon in a square button (its label stays the accessible name).</summary>
    private void UpdatePrimaryCompact()
    {
        if (_primaryButton is null)
        {
            return;
        }
        var iconOnly = _isTight && _primaryHasIcon && _primaryLabel is not null;
        if (_primaryLabel is not null)
        {
            _primaryLabel.IsVisible = !iconOnly;
        }
        _primaryButton.Classes.Set("icon-only", iconOnly);
    }

    private Control BuildContent(PageHeaderAction action, out TextBlock? label)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ActionContentGap,
            VerticalAlignment = VerticalAlignment.Center
        };
        switch (action.Icon)
        {
            case Geometry geometry:
                panel.Children.Add(new ArcGlyph
                {
                    Data = geometry,
                    Size = ActionGlyphSize,
                    StrokeThickness = 1.75,
                    VerticalAlignment = VerticalAlignment.Center
                });
                break;
            case Control control:
                if (control.GetVisualParent() is Panel host)
                {
                    host.Children.Remove(control);
                }
                panel.Children.Add(control);
                break;
        }
        label = null;
        if (!string.IsNullOrEmpty(action.Label))
        {
            label = new TextBlock { Text = action.Label, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(label);
        }
        return panel;
    }

    private void OnSecondaryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PageHeaderAction action })
        {
            InvokeAction(action, fromMenu: false);
        }
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        if (PrimaryAction is { } action)
        {
            InvokeAction(action, fromMenu: false);
        }
    }

    private void InvokeAction(PageHeaderAction action, bool fromMenu)
    {
        if (!action.CanExecute)
        {
            return;
        }
        action.Invoke();
        RaiseEvent(new PageHeaderActionEventArgs(ActionInvokedEvent, action, fromMenu));
    }

    #endregion

    #region Overflow menu

    private MenuSurface EnsureMenu()
    {
        if (_menu is null)
        {
            _menu = new MenuSurface { Alignment = MenuAlignment.End, AnchorGap = 6 };
            _menu.ItemClicked += OnMenuItemClicked;
        }
        return _menu;
    }

    /// <summary>
    /// The menu lists the folded secondary actions first, then <see cref="MoreActions"/> after a separator. The overflow
    /// button hides while the menu would be empty.
    /// </summary>
    private void RebuildMenu()
    {
        var menu = EnsureMenu();
        _menuEntries.Clear();
        menu.Items.Clear();
        var folded = AreActionsFolded ? Actions.ToList() : new List<PageHeaderAction>();
        foreach (var action in folded)
        {
            AddEntry(menu, action, separatorBefore: false);
        }
        var first = true;
        foreach (var action in MoreActions)
        {
            AddEntry(menu, action, separatorBefore: first && folded.Count > 0);
            first = false;
        }
        if (_moreButton is not null)
        {
            _moreButton.IsVisible = menu.Items.Count > 0;
            if (menu.Items.Count == 0 && menu.IsOpen)
            {
                menu.Close(returnFocus: false);
            }
        }
    }

    private void AddEntry(MenuSurface menu, PageHeaderAction action, bool separatorBefore)
    {
        if (_menuEntries.ContainsKey(action))
        {
            return;
        }
        var entry = new MenuEntry { Tag = action, HasSeparatorBefore = separatorBefore };
        UpdateEntry(entry, action);
        _menuEntries[action] = entry;
        menu.Items.Add(entry);
    }

    private static void UpdateEntry(MenuEntry entry, PageHeaderAction action)
    {
        entry.Header = action.EffectiveMenuLabel;
        entry.Icon = action.EffectiveMenuIcon;
        entry.IsDestructive = action.IsDestructive;
        entry.IsEnabled = action.CanExecute;
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is PageHeaderAction action)
        {
            InvokeAction(action, fromMenu: true);
        }
    }

    #endregion

    #region Tabs

    private void UpdateTabsVisibility()
    {
        if (_tabsViewport is not null)
        {
            _tabsViewport.IsVisible = ItemCount > 0;
        }
    }

    /// <summary>Called by a <see cref="PageHeaderTab"/> when its content or template changes.</summary>
    internal void OnTabContentChanged(PageHeaderTab tab)
    {
        if (IndexFromContainer(tab) == SelectedIndex)
        {
            UpdateContent(0, animate: false);
        }
    }

    /// <summary>Called by a <see cref="PageHeaderTab"/> when it is pressed (mouse down, like Radix).</summary>
    internal void OnTabPressed(PageHeaderTab tab)
    {
        var index = IndexFromContainer(tab);
        if (index >= 0 && tab.IsEffectivelyEnabled)
        {
            _focusIndex = index;
            SelectedIndex = index;
        }
    }

    /// <summary>Called by a <see cref="PageHeaderTab"/> when it receives keyboard focus.</summary>
    internal void OnTabFocused(PageHeaderTab tab, NavigationMethod method)
    {
        var index = IndexFromContainer(tab);
        if (index < 0)
        {
            return;
        }
        _focusIndex = index;
        UpdateTabStops();
        RevealTabInRow(index, instant: false);
        if (method == NavigationMethod.Directional && index != SelectedIndex)
        {
            SelectedIndex = index;
        }
    }

    private void UpdateContent(int direction, bool animate)
    {
        var index = SelectedIndex;
        object? content = null;
        IDataTemplate? template = null;
        if (index >= 0 && ContainerFromIndex(index) is PageHeaderTab tab)
        {
            content = tab.Content;
            template = tab.ContentTemplate ?? ContentTemplate;
        }
        else if (index >= 0 && index < ItemCount && Items[index] is { } item and not PageHeaderTab)
        {
            content = item;
            template = ContentTemplate;
        }
        _body?.Show(content, template, direction, animate && IsLoaded);
    }

    private bool IsTabEnabled(int index) => ContainerFromIndex(index) is not { } container || container.IsEffectivelyEnabled;

    private void UpdateTabStops()
    {
        var count = ItemCount;
        var stop = _focusIndex >= 0 && _focusIndex < count ? _focusIndex : SelectedIndex;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is PageHeaderTab tab)
            {
                tab.IsTabStop = i == stop || (stop < 0 && i == 0);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source || FindTab(source) is not { } tab)
        {
            base.OnKeyDown(e);
            return;
        }
        var current = IndexFromContainer(tab);
        int? target = e.Key switch
        {
            Key.Left => Step(current, -1),
            Key.Right => Step(current, 1),
            Key.Home => FirstEnabled(0, 1),
            Key.End => FirstEnabled(ItemCount - 1, -1),
            _ => null
        };
        if (target is { } index && index >= 0)
        {
            e.Handled = true;
            if (ContainerFromIndex(index) is PageHeaderTab next)
            {
                _focusIndex = index;
                UpdateTabStops();
                next.Focus(NavigationMethod.Directional);
                SelectedIndex = index;
            }
            return;
        }
        if ((e.Key == Key.Enter || e.Key == Key.Space) && current >= 0)
        {
            e.Handled = true;
            SelectedIndex = current;
            return;
        }
        base.OnKeyDown(e);
    }

    private PageHeaderTab? FindTab(Visual source)
    {
        for (Visual? v = source; v is not null && v != this; v = v.GetVisualParent())
        {
            if (v is PageHeaderTab tab && IndexFromContainer(tab) >= 0)
            {
                return tab;
            }
        }
        return null;
    }

    private int Step(int current, int delta)
    {
        var count = ItemCount;
        if (count == 0)
        {
            return -1;
        }
        var index = current;
        for (var i = 0; i < count; i++)
        {
            // Radix tabs loop at the ends.
            index = ((index + delta) % count + count) % count;
            if (IsTabEnabled(index))
            {
                return index;
            }
        }
        return current;
    }

    private int FirstEnabled(int start, int step)
    {
        for (var i = start; i >= 0 && i < ItemCount; i += step)
        {
            if (IsTabEnabled(i))
            {
                return i;
            }
        }
        return -1;
    }

    private void OnTabStripArranged()
    {
        // Passive layout changes (fonts, counts, resizes) snap the indicator; an unchanged target leaves a glide alone.
        UpdateIndicator(animate: false, onlyWhenMoved: true);
        if (_revealPending && SelectedIndex >= 0 && ContainerFromIndex(SelectedIndex) is { Bounds.Width: > 0 })
        {
            _revealPending = false;
            RevealTabInRow(SelectedIndex, instant: true);
        }
    }

    private void UpdateIndicator(bool animate, bool onlyWhenMoved = false)
    {
        if (_indicator is null || _tabStrip is null)
        {
            return;
        }
        var index = SelectedIndex;
        if (index < 0 || ContainerFromIndex(index) is not { IsVisible: true } container || container.Bounds.Width <= 0)
        {
            _indicatorTarget = default;
            _indicator.Hide(animate: false);
            return;
        }
        if (container.TranslatePoint(default, _tabStrip) is not { } origin)
        {
            return;
        }
        var inset = IndicatorInset;
        var height = IndicatorHeight;
        var target = new Rect(origin.X + inset, origin.Y + container.Bounds.Height - height,
            Math.Max(0, container.Bounds.Width - 2 * inset), height);
        if (onlyWhenMoved && _indicator.HasTarget && Near(target, _indicatorTarget))
        {
            return;
        }
        _indicatorTarget = target;
        _indicator.MoveTo(target, animate && IsLoaded, ArcSpring.Morph);
    }

    private static bool Near(Rect a, Rect b)
        => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    /// <summary>Keeps the chosen tab in view (12px clear) when the tab row scrolls sideways on narrow headers.</summary>
    private void RevealTabInRow(int index, bool instant)
    {
        if (_tabsViewport is not { } viewport || index < 0 || ContainerFromIndex(index) is not { } tab || tab.Bounds.Width <= 0 ||
            tab.TranslatePoint(default, viewport) is not { } origin)
        {
            return;
        }
        var offset = viewport.Offset.X;
        var left = origin.X + offset;
        var start = left - TabRevealMargin;
        var end = left + tab.Bounds.Width + TabRevealMargin - viewport.Viewport.Width;
        double? target = offset > start ? start : offset < end ? end : null;
        if (target is not { } x)
        {
            return;
        }
        var max = Math.Max(0, viewport.Extent.Width - viewport.Viewport.Width);
        x = Math.Clamp(x, 0, max);
        if (_tabScroll is null)
        {
            _tabScroll = new MotionValue<double>(this, offset, MotionVectors.Double);
            _tabScroll.Changed += value =>
            {
                if (_tabsViewport is not null)
                {
                    _tabsViewport.Offset = new Vector(value, _tabsViewport.Offset.Y);
                }
            };
        }
        if (!_tabScroll.IsAnimating)
        {
            _tabScroll.Jump(offset);
        }
        if (instant || !IsLoaded)
        {
            _tabScroll.Jump(x);
            return;
        }
        // Smooth scroll on the smooth spring; Reduced and Off jump (MotionValue honors the level).
        _ = _tabScroll.AnimateTo(x, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private void OnTabsWheel(object? sender, PointerWheelEventArgs e) => _tabScroll?.Stop();

    private void OnTabsViewportSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateTabsMask();

    /// <summary>Below 560px the tab row's far edge fades over 28px, so the next tab reads as more to come.</summary>
    private void UpdateTabsMask()
    {
        if (_tabsViewport is null)
        {
            return;
        }
        var width = _tabsViewport.Bounds.Width;
        if (!_isNarrow || width <= 0)
        {
            _tabsViewport.OpacityMask = null;
            _tabsMaskWidth = -1;
            return;
        }
        if (_tabsViewport.OpacityMask is not null && Math.Abs(width - _tabsMaskWidth) < 0.5)
        {
            return;
        }
        _tabsMaskWidth = width;
        var fade = Math.Clamp(1 - TabsFadeWidth / width, 0, 1);
        var stops = new[]
        {
            new ImmutableGradientStop(0, Colors.Black),
            new ImmutableGradientStop(fade, Colors.Black),
            new ImmutableGradientStop(1, Colors.Transparent)
        };
        _tabsViewport.OpacityMask = new ImmutableLinearGradientBrush(stops, 1, null, null, GradientSpreadMethod.Pad,
            new RelativePoint(0, 0.5, RelativeUnit.Relative), new RelativePoint(1, 0.5, RelativeUnit.Relative));
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new PageHeaderAutomationPeer(this);

    private sealed class PageHeaderAutomationPeer : SelectingItemsControlAutomationPeer
    {
        public PageHeaderAutomationPeer(PageHeader owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
