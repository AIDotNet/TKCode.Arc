using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The three lists of the model menu.</summary>
internal enum ModelMenuPage
{
    /// <summary>Featured models, then Effort and More models.</summary>
    Main,

    /// <summary>The selected model's effort levels.</summary>
    Effort,

    /// <summary>The other models, grouped by provider.</summary>
    More
}

/// <summary>How the highlight gets to a row: the pointer glides it (and fades it in where it first lands), the keyboard jumps.</summary>
internal enum ModelMenuMove
{
    Pointer,
    Keyboard
}

/// <summary>One built list: its content and the rows the keyboard walks through, in order.</summary>
internal sealed class ModelMenuPageView
{
    public ModelMenuPageView(ModelMenuPage page, Control root, IReadOnlyList<ModelMenuItem> rows, ModelMenuActionRow? backRow)
    {
        Page = page;
        Root = root;
        Rows = rows;
        BackRow = backRow;
    }

    public ModelMenuPage Page { get; }

    public Control Root { get; }

    public IReadOnlyList<ModelMenuItem> Rows { get; }

    /// <summary>The back row of a page in a narrow window (null in a submenu and on the main page).</summary>
    public ModelMenuActionRow? BackRow { get; }

    /// <summary>The inline layout's "Effort" header (null elsewhere).</summary>
    public ModelMenuEffortHeader? EffortHeader { get; init; }

    /// <summary>The inline layout's segmented row of levels (null elsewhere).</summary>
    public ModelMenuEffortStrip? EffortStrip { get; init; }
}

/// <summary>
/// The surface of the model menu or of one of its submenus: a raised panel (1px border, panel radius, floating shadow
/// from the popup) whose list scrolls past 420px. One highlight per panel sits behind the rows (Core
/// <see cref="GlidingIndicator"/>): the pointer glides it on the snappy spring and fades it in or out in place in 80ms,
/// the keyboard jumps it. In a narrow window the main panel swaps its list for a page; the new page fades in from 12px
/// to the side it comes from while the panel height follows on the smooth spring (Core <see cref="SpringSizeDecorator"/>).
/// The main panel of the inline layout (<see cref="IsInline"/>) has the composer's 5px list padding and 20px highlight;
/// on its segmented row the highlight leaves (the row shows its own ring around the level).
/// </summary>
[TemplatePart(SizePart, typeof(SpringSizeDecorator))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(ListPart, typeof(Panel))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(PagePart, typeof(Decorator))]
internal sealed class ModelMenuPanel : TemplatedControl
{
    public const string SizePart = "PART_Size";
    public const string ScrollPart = "PART_Scroll";
    public const string ListPart = "PART_List";
    public const string HighlightPart = "PART_Highlight";
    public const string PagePart = "PART_Page";

    /// <summary>How far a new page travels in (12; the theme sets the token).</summary>
    public static readonly StyledProperty<double> PageTravelProperty =
        AvaloniaProperty.Register<ModelMenuPanel, double>(nameof(PageTravel), 12);

    /// <summary>The main panel of the inline (composer) layout: list padding 5, highlight radius 20.</summary>
    public static readonly StyledProperty<bool> IsInlineProperty =
        AvaloniaProperty.Register<ModelMenuPanel, bool>(nameof(IsInline));

    /// <summary>The main panel of the simple layout (the chat): the control radius (18), a 12px highlight.</summary>
    public static readonly StyledProperty<bool> IsSimpleProperty =
        AvaloniaProperty.Register<ModelMenuPanel, bool>(nameof(IsSimple));

    private static readonly Easing s_linear = new LinearEasing();

    private readonly TranslateTransform _pageShift = new();
    private SpringSizeDecorator? _size;
    private ScrollViewer? _scroll;
    private Panel? _list;
    private GlidingIndicator? _highlight;
    private Decorator? _pageHost;
    private ModelMenuPageView? _view;
    private ModelMenuItem? _active;
    private ModelMenuItem? _pending;
    private ModelMenuMove _pendingMove;
    private MotionValue<double>? _pageProgress;
    private int _pageDirection;

    public ModelMenuPanel(ModelMenu owner, ModelMenuPage role)
    {
        Owner = owner;
        Role = role;
        Typeahead = new ModelMenuTypeahead(() => owner.Now());
        // Tunnel: the menu's keys win over the scroll area between the focused row and the panel.
        AddHandler(KeyDownEvent, OnPanelKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    static ModelMenuPanel()
    {
        FocusableProperty.OverrideDefaultValue<ModelMenuPanel>(true);
        IsTabStopProperty.OverrideDefaultValue<ModelMenuPanel>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<ModelMenuPanel>(KeyboardNavigationMode.None);
    }

    public double PageTravel { get => GetValue(PageTravelProperty); set => SetValue(PageTravelProperty, value); }

    public bool IsInline { get => GetValue(IsInlineProperty); set => SetValue(IsInlineProperty, value); }

    public bool IsSimple { get => GetValue(IsSimpleProperty); set => SetValue(IsSimpleProperty, value); }

    public ModelMenu Owner { get; }

    /// <summary>Main for the menu itself (it shows pages in a narrow window), Effort or More for a submenu.</summary>
    public ModelMenuPage Role { get; }

    /// <summary>The list shown now.</summary>
    public ModelMenuPageView? View => _view;

    public ModelMenuPage Page => _view?.Page ?? Role;

    public IReadOnlyList<ModelMenuItem> Rows => _view?.Rows ?? [];

    /// <summary>The highlighted row (the pointer's or the keyboard's position), or null.</summary>
    public ModelMenuItem? Active => _active;

    public ModelMenuTypeahead Typeahead { get; }

    internal GlidingIndicator? Highlight => _highlight;

    internal ScrollViewer? Scroll => _scroll;

    internal SpringSizeDecorator? SizeDecorator => _size;

    internal Panel? List => _list;

    /// <summary>Direction of the last page change: 1 forward (from the right), −1 back (from the left), 0 none.</summary>
    internal int PageDirection => _pageDirection;

    /// <summary>The page enter's progress (0 → 1); 1 at rest.</summary>
    internal double PageProgress => _pageProgress?.Value ?? 1;

    internal TranslateTransform PageShift => _pageShift;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_list is not null)
        {
            _list.SizeChanged -= OnListSizeChanged;
            _list.LayoutUpdated -= OnListLayoutUpdated;
        }
        base.OnApplyTemplate(e);
        _size = e.NameScope.Find<SpringSizeDecorator>(SizePart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _list = e.NameScope.Find<Panel>(ListPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _pageHost = e.NameScope.Find<Decorator>(PagePart);
        if (_list is not null)
        {
            _list.RenderTransform = _pageShift;
            _list.SizeChanged += OnListSizeChanged;
        }
        if (_pageHost is not null && _view is not null)
        {
            _pageHost.Child = _view.Root;
        }
    }

    #region Pages

    /// <summary>
    /// Shows <paramref name="view"/>: the old list goes at once; with a <paramref name="direction"/> the new one fades in
    /// from 12px on that side (forward: from the right). The highlight starts hidden.
    /// </summary>
    public void ShowView(ModelMenuPageView view, int direction)
    {
        var changed = !ReferenceEquals(_view, view);
        _view = view;
        Typeahead.Reset();
        if (changed)
        {
            SetActive(null, ModelMenuMove.Keyboard);
            if (_pageHost is not null)
            {
                _pageHost.Child = view.Root;
            }
            if (_scroll is not null)
            {
                _scroll.Offset = default;
            }
        }
        _pageDirection = direction;
        PlayPageEnter(direction);
    }

    /// <summary>
    /// The page enter (Arc: opacity and 12px of travel, 160ms standard; reduced motion: a 120ms fade; off: none).
    /// Code-driven because the start side depends on the navigation direction and the motion starts from fixed values on
    /// content that was just swapped in; a ControlTheme transition only animates between two styled states.
    /// </summary>
    private void PlayPageEnter(int direction)
    {
        _pageProgress ??= CreatePageProgress();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (direction == 0 || level == ArcMotionLevel.Off || !IsLoaded)
        {
            _pageProgress.Jump(1);
            return;
        }
        _pageProgress.Jump(0);
        var spec = level == ArcMotionLevel.Full
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear);
        _ = _pageProgress.AnimateTo(1, spec);
    }

    private MotionValue<double> CreatePageProgress()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += ApplyPageProgress;
        return value;
    }

    private void ApplyPageProgress(double progress)
    {
        if (_list is null)
        {
            return;
        }
        _list.Opacity = Math.Clamp(progress, 0, 1);
        var travel = ArcMotion.IsFull(this) ? PageTravel : 0;
        _pageShift.X = _pageDirection * travel * (1 - progress);
    }

    #endregion

    #region Highlight

    /// <summary>
    /// Moves the highlight to <paramref name="row"/> (null hides it): the pointer glides it, or fades it in where it lands
    /// when it was hidden, and fades it out; the keyboard jumps and hides it at once, and scrolls the row into view
    /// (at once too).
    /// </summary>
    public void SetActive(ModelMenuItem? row, ModelMenuMove move)
    {
        _active = row;
        _pending = null;
        if (_highlight is null)
        {
            return;
        }
        if (row is null)
        {
            _highlight.Hide(animate: move == ModelMenuMove.Pointer);
            return;
        }
        if (row is ModelMenuEffortStrip)
        {
            // The segmented row draws its own position (a ring around the level under keyboard focus): no highlight over it.
            _highlight.Hide(animate: move == ModelMenuMove.Pointer);
            if (move == ModelMenuMove.Keyboard)
            {
                row.BringIntoView();
            }
            return;
        }
        if (RectOf(row) is { } rect)
        {
            _highlight.MoveTo(rect, animate: move == ModelMenuMove.Pointer, ArcSpring.Snappy);
            if (move == ModelMenuMove.Keyboard)
            {
                row.BringIntoView();
            }
            return;
        }
        // The row was just built (a new page): place the highlight (and scroll to it) after the next layout.
        _pending = row;
        _pendingMove = move;
        if (_list is not null)
        {
            _list.LayoutUpdated -= OnListLayoutUpdated;
            _list.LayoutUpdated += OnListLayoutUpdated;
        }
    }

    private void OnListLayoutUpdated(object? sender, EventArgs e)
    {
        if (_pending is not { } row)
        {
            _list!.LayoutUpdated -= OnListLayoutUpdated;
            return;
        }
        if (RectOf(row) is { } rect)
        {
            _list!.LayoutUpdated -= OnListLayoutUpdated;
            _pending = null;
            if (ReferenceEquals(_active, row))
            {
                _highlight?.MoveTo(rect, animate: _pendingMove == ModelMenuMove.Pointer, ArcSpring.Snappy);
                if (_pendingMove == ModelMenuMove.Keyboard)
                {
                    row.BringIntoView();
                }
            }
        }
    }

    private void OnListSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Rows moved (a model renamed, a row hidden): the highlight follows without animating.
        if (_active is { } row and not ModelMenuEffortStrip && _pending is null && RectOf(row) is { } rect)
        {
            _highlight?.MoveTo(rect, animate: false);
        }
    }

    /// <summary>The row's box in the highlight's coordinates, or null before it is laid out.</summary>
    internal Rect? RectOf(ModelMenuItem row)
    {
        if (_list is null || !row.IsVisible || !row.IsArrangeValid || row.Bounds.Height <= 0 ||
            row.TranslatePoint(default, _list) is not { } origin)
        {
            return null;
        }
        return new Rect(origin, row.Bounds.Size);
    }

    #endregion

    #region Hit testing

    /// <summary>True when <paramref name="point"/> (in <paramref name="topLevel"/> coordinates) is over this surface.</summary>
    public bool Contains(Point point, Visual topLevel) =>
        topLevel.TranslatePoint(point, this) is { } local && new Rect(Bounds.Size).Contains(local);

    /// <summary>The row under <paramref name="point"/> (in <paramref name="topLevel"/> coordinates), or null (padding, separator, heading).</summary>
    public ModelMenuItem? RowAt(Point point, Visual topLevel)
    {
        if (_scroll is null || topLevel.TranslatePoint(point, _scroll) is not { } inViewport ||
            !new Rect(_scroll.Bounds.Size).Contains(inViewport))
        {
            return null;
        }
        foreach (var row in Rows)
        {
            if (row.IsVisible && topLevel.TranslatePoint(point, row) is { } local && new Rect(row.Bounds.Size).Contains(local))
            {
                return row;
            }
        }
        return null;
    }

    #endregion

    #region Input

    internal void OnRowInvoked(ModelMenuItem row, NavigationMethod method) => Owner.OnRowInvoked(this, row, method);

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled)
        {
            Owner.OnPanelKeyDown(this, e);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ModelMenuPanelAutomationPeer(this);

    private sealed class ModelMenuPanelAutomationPeer(ModelMenuPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;

        protected override bool IsControlElementCore() => true;
    }
}
