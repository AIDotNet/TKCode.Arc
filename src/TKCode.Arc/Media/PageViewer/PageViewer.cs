using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Zooms, pans and pages through a set of pages: pictures from <see cref="ItemsSource"/> or pages rendered on demand from
/// an <see cref="IPageSource"/>. One self-drawn surface draws only the visible pages, so a document of ten thousand pages
/// costs no more than a short one; a toolbar moves between pages and picks the zoom and the layout, and
/// <see cref="Layers"/> draw over the pages and take pointer input. Pages are always white paper, in dark themes too.
/// Ctrl / Cmd + wheel, a trackpad magnify and a touch pinch zoom about the pointer; the toolbar and the keyboard zoom about
/// the viewport center on a spring; the middle button or holding Space pans in any mode.
/// <para>
/// Designed to be derived from: <see cref="ToolbarStart"/> and <see cref="ToolbarEnd"/> extend the toolbar;
/// <see cref="LeadingPane"/> and <see cref="ContentOverlay"/> add a side panel and floating parts without a new template
/// (give the derived control a theme that is <c>BasedOn</c> this one); <see cref="OnViewerKeyDown"/> takes
/// keys first; <see cref="GetPageBitmap"/> shares the render cache with other views of the same pages.
/// </para>
/// </summary>
[TemplatePart(ToolbarPart, typeof(Control))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(PresenterPart, typeof(Control))]
[TemplatePart(LeadingPanePart, typeof(ContentPresenter))]
[TemplatePart(OverlayPart, typeof(ContentPresenter))]
[PseudoClasses(LoadingPseudoClass, PanPseudoClass, PanningPseudoClass, EmptyPseudoClass)]
public class PageViewer : TemplatedControl
{
    public const string ToolbarPart = "PART_Toolbar";
    public const string ScrollPart = "PART_Scroll";
    public const string PresenterPart = "PART_Presenter";
    public const string LeadingPanePart = "PART_LeadingPane";
    public const string OverlayPart = "PART_Overlay";

    /// <summary>Set while <see cref="IsLoading"/> is true.</summary>
    public const string LoadingPseudoClass = ":loading";

    /// <summary>Set while <see cref="InteractionMode"/> is <see cref="PageInteractionMode.Pan"/>.</summary>
    public const string PanPseudoClass = ":pan";

    /// <summary>Set while the pointer is panning the view.</summary>
    public const string PanningPseudoClass = ":panning";

    /// <summary>Set while there are no pages.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>How far an arrow key scrolls.</summary>
    private const double ArrowKeyStep = 40;

    #region Properties

    /// <summary>Pictures to show as pages (see <see cref="ImagePageSource"/>); a set <see cref="PageSource"/> wins over it.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<PageViewer, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Pages rendered on demand.</summary>
    public static readonly StyledProperty<IPageSource?> PageSourceProperty =
        AvaloniaProperty.Register<PageViewer, IPageSource?>(nameof(PageSource));

    /// <summary>How many pages there are.</summary>
    public static readonly DirectProperty<PageViewer, int> PageCountProperty =
        AvaloniaProperty.RegisterDirect<PageViewer, int>(nameof(PageCount), o => o.PageCount);

    /// <summary>The page at the middle of the viewport (two-way, from 0); setting it jumps there.</summary>
    public static readonly StyledProperty<int> CurrentPageIndexProperty =
        AvaloniaProperty.Register<PageViewer, int>(nameof(CurrentPageIndex), 0, defaultBindingMode: BindingMode.TwoWay, coerce: CoerceCurrentPageIndex);

    /// <summary>The zoom (two-way): 1 shows pages at their own size. Clamped to the limits; a change from outside sets <see cref="FitMode"/> to None.</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<PageViewer, double>(nameof(Zoom), 1, defaultBindingMode: BindingMode.TwoWay, coerce: CoerceZoom);

    /// <summary>The smallest zoom (default 0.1).</summary>
    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<PageViewer, double>(nameof(MinZoom), 0.1);

    /// <summary>The largest zoom (default 8).</summary>
    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<PageViewer, double>(nameof(MaxZoom), 8);

    /// <summary>Keeps the zoom fitted to the viewport (default Width); the zoom is worked out again when the viewport changes.</summary>
    public static readonly StyledProperty<PageFitMode> FitModeProperty =
        AvaloniaProperty.Register<PageViewer, PageFitMode>(nameof(FitMode), PageFitMode.Width);

    /// <summary>All pages in a column (Continuous, the default) or one at a time (Single).</summary>
    public static readonly StyledProperty<PageLayoutMode> LayoutModeProperty =
        AvaloniaProperty.Register<PageViewer, PageLayoutMode>(nameof(LayoutMode));

    /// <summary>What a drag does: Select leaves it to the layers (default), Pan moves the view.</summary>
    public static readonly StyledProperty<PageInteractionMode> InteractionModeProperty =
        AvaloniaProperty.Register<PageViewer, PageInteractionMode>(nameof(InteractionMode));

    /// <summary>Shows the toolbar (default true).</summary>
    public static readonly StyledProperty<bool> IsToolbarVisibleProperty =
        AvaloniaProperty.Register<PageViewer, bool>(nameof(IsToolbarVisible), true);

    /// <summary>Content at the left end of the toolbar.</summary>
    public static readonly StyledProperty<object?> ToolbarStartProperty =
        AvaloniaProperty.Register<PageViewer, object?>(nameof(ToolbarStart));

    /// <summary>Content at the right end of the toolbar.</summary>
    public static readonly StyledProperty<object?> ToolbarEndProperty =
        AvaloniaProperty.Register<PageViewer, object?>(nameof(ToolbarEnd));

    /// <summary>How many bytes of rendered bitmaps to keep (default 192 MB). Least recently used go first; what visible pages draw stays.</summary>
    public static readonly StyledProperty<long> RenderCacheBytesProperty =
        AvaloniaProperty.Register<PageViewer, long>(nameof(RenderCacheBytes), 192L * 1024 * 1024);

    /// <summary>True while a visible page waits for its first bitmap.</summary>
    public static readonly DirectProperty<PageViewer, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<PageViewer, bool>(nameof(IsLoading), o => o.IsLoading);

    /// <summary>Raised when <see cref="CurrentPageIndex"/> changes.</summary>
    public static readonly RoutedEvent<PageChangedEventArgs> CurrentPageChangedEvent =
        RoutedEvent.Register<PageViewer, PageChangedEventArgs>(nameof(CurrentPageChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="PageSourceProperty"/>
    public IPageSource? PageSource { get => GetValue(PageSourceProperty); set => SetValue(PageSourceProperty, value); }

    /// <inheritdoc cref="PageCountProperty"/>
    public int PageCount
    {
        get => _pageCount;
        private set => SetAndRaise(PageCountProperty, ref _pageCount, value);
    }

    /// <inheritdoc cref="CurrentPageIndexProperty"/>
    public int CurrentPageIndex { get => GetValue(CurrentPageIndexProperty); set => SetValue(CurrentPageIndexProperty, value); }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    /// <inheritdoc cref="MinZoomProperty"/>
    public double MinZoom { get => GetValue(MinZoomProperty); set => SetValue(MinZoomProperty, value); }

    /// <inheritdoc cref="MaxZoomProperty"/>
    public double MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    /// <inheritdoc cref="FitModeProperty"/>
    public PageFitMode FitMode { get => GetValue(FitModeProperty); set => SetValue(FitModeProperty, value); }

    /// <inheritdoc cref="LayoutModeProperty"/>
    public PageLayoutMode LayoutMode { get => GetValue(LayoutModeProperty); set => SetValue(LayoutModeProperty, value); }

    /// <inheritdoc cref="InteractionModeProperty"/>
    public PageInteractionMode InteractionMode { get => GetValue(InteractionModeProperty); set => SetValue(InteractionModeProperty, value); }

    /// <inheritdoc cref="IsToolbarVisibleProperty"/>
    public bool IsToolbarVisible { get => GetValue(IsToolbarVisibleProperty); set => SetValue(IsToolbarVisibleProperty, value); }

    /// <inheritdoc cref="ToolbarStartProperty"/>
    public object? ToolbarStart { get => GetValue(ToolbarStartProperty); set => SetValue(ToolbarStartProperty, value); }

    /// <inheritdoc cref="ToolbarEndProperty"/>
    public object? ToolbarEnd { get => GetValue(ToolbarEndProperty); set => SetValue(ToolbarEndProperty, value); }

    /// <inheritdoc cref="RenderCacheBytesProperty"/>
    public long RenderCacheBytes { get => GetValue(RenderCacheBytesProperty); set => SetValue(RenderCacheBytesProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    /// <summary>Layers drawn over the pages, in order; pointer events go from the last to the first.</summary>
    public IList<PageLayer> Layers => _layers;

    /// <inheritdoc cref="CurrentPageChangedEvent"/>
    public event EventHandler<PageChangedEventArgs>? CurrentPageChanged
    {
        add => AddHandler(CurrentPageChangedEvent, value);
        remove => RemoveHandler(CurrentPageChangedEvent, value);
    }

    #endregion

    /// <summary>How the next change of <see cref="Zoom"/> came about. The default is a change from outside: no anchor point, no animation.</summary>
    private readonly record struct ZoomChange(Point? Anchor, bool Animate, bool KeepsFit, bool CallerRelayouts);

    private readonly PageLayoutEngine _layout = new();
    private readonly PageRenderScheduler _scheduler;
    private readonly AvaloniaList<PageLayer> _layers = new();
    private IPageSource? _source;
    private ImagePageSource? _itemsWrapper;
    private bool _subscribed;
    private PageViewerPresenter? _presenter;
    private PageViewerToolbar? _toolbar;
    private ContentPresenter? _leadingHost;
    private ContentPresenter? _overlayHost;
    private object? _leadingPane;
    private object? _contentOverlay;
    private MotionValue<double>? _zoomMotion;
    private PageAnchor? _zoomAnchor;
    private ZoomChange _zoomChange;
    private int _pageCount;
    private bool _isLoading;
    private bool _updatingCurrent;
    private bool _pendingJump;
    private bool _nextJumpAnimates;
    private bool _holdsCurrent;
    private Vector _heldOffset;

    static PageViewer()
    {
        ClipToBoundsProperty.OverrideDefaultValue<PageViewer>(true);
    }

    public PageViewer()
    {
        _scheduler = new PageRenderScheduler(_layout);
        _scheduler.Changed += OnSchedulerChanged;
        _scheduler.LoadingChanged += OnSchedulerLoadingChanged;
        _scheduler.AuxiliaryReady += OnPageBitmapReady;
        _layers.CollectionChanged += OnLayersChanged;
        PseudoClasses.Set(EmptyPseudoClass, true);
    }

    internal PageLayoutEngine Layout => _layout;

    internal PageRenderScheduler Scheduler => _scheduler;

    internal IReadOnlyList<PageLayer> LayerList => _layers;

    /// <summary>A side panel to the left of the pages, under the toolbar. The pages take what it leaves.</summary>
    protected object? LeadingPane
    {
        get => _leadingPane;
        set
        {
            _leadingPane = value;
            if (_leadingHost is { } host)
            {
                host.Content = value;
            }
        }
    }

    /// <summary>Content over the pages, under the toolbar, such as a floating search bar or a state cover. Empty space lets input through.</summary>
    protected object? ContentOverlay
    {
        get => _contentOverlay;
        set
        {
            _contentOverlay = value;
            if (_overlayHost is { } host)
            {
                host.Content = value;
            }
        }
    }

    /// <summary>The source on show: <see cref="PageSource"/>, or the wrapper around <see cref="ItemsSource"/>.</summary>
    protected IPageSource? EffectivePageSource => _source;

    #region Public methods

    /// <summary>Goes to a page (clamped to the range), with a spring when <paramref name="animate"/> is true and motion allows.</summary>
    public void GoToPage(int index, bool animate = true)
    {
        var clamped = Math.Clamp(index, 0, Math.Max(0, PageCount - 1));
        if (clamped != CurrentPageIndex)
        {
            _nextJumpAnimates = animate;
            SetCurrentValue(CurrentPageIndexProperty, clamped);
            return;
        }
        JumpToPage(clamped, animate);
    }

    /// <summary>Goes to the next page.</summary>
    public void NextPage() => GoToPage(CurrentPageIndex + 1);

    /// <summary>Goes to the previous page.</summary>
    public void PreviousPage() => GoToPage(CurrentPageIndex - 1);

    /// <summary>Zooms to the next step (10, 25, 50, 75, 100, 125, 150, 200, 300, 400, 600, 800%) about the viewport center.</summary>
    public void ZoomIn() => ChangeZoom(PageLayoutEngine.NextZoomStep(Zoom), null, animate: true);

    /// <summary>Zooms to the previous step about the viewport center.</summary>
    public void ZoomOut() => ChangeZoom(PageLayoutEngine.PreviousZoomStep(Zoom), null, animate: true);

    /// <summary>Zooms to 100% about the viewport center.</summary>
    public void ResetZoom() => ChangeZoom(1, null, animate: true);

    /// <summary>
    /// Scrolls so that a part of a page (in page DIPs) is on screen, by the shortest way; if it already is, nothing moves.
    /// Used for search results and link targets.
    /// </summary>
    public void ScrollIntoView(int pageIndex, Rect pageRegion, bool animate = true)
    {
        if (PageCount == 0)
        {
            return;
        }
        var page = Math.Clamp(pageIndex, 0, PageCount - 1);
        if (LayoutMode == PageLayoutMode.Single && page != CurrentPageIndex)
        {
            GoToPage(page, animate: false);
        }
        if (_presenter is not { } presenter || presenter.ViewportSize.Width <= 0 || presenter.ViewportSize.Height <= 0)
        {
            return;
        }
        var viewport = presenter.ViewportSize;
        var topLeft = _layout.ToContent(page, pageRegion.TopLeft, viewport);
        var bottomRight = _layout.ToContent(page, pageRegion.BottomRight, viewport);
        var offset = presenter.Offset;
        var x = ShiftIntoView(offset.X, viewport.Width, topLeft.X, bottomRight.X);
        var y = ShiftIntoView(offset.Y, viewport.Height, topLeft.Y, bottomRight.Y);
        if (x != offset.X || y != offset.Y)
        {
            // A place on a page is no jump to a page: from here on the middle of the viewport says which page is current.
            _holdsCurrent = false;
            presenter.ScrollTo(new Vector(x, y), animate);
        }
    }

    /// <summary>The page under a point of this control and the point in page DIPs; false over the canvas, the toolbar or outside.</summary>
    public bool PageHitTest(Point pointInViewer, out int pageIndex, out Point pointInPage)
    {
        pageIndex = -1;
        pointInPage = default;
        if (_presenter is not { } presenter || this.TranslatePoint(pointInViewer, presenter) is not { } local
            || !new Rect(presenter.Bounds.Size).Contains(local))
        {
            return false;
        }
        return _layout.HitTest(local + presenter.Offset, presenter.ViewportSize, out pageIndex, out pointInPage);
    }

    #endregion

    #region Protected extension points

    /// <summary>
    /// Sees keys first, while the viewport or the toolbar has focus. Return true to take a key away from the viewer. Search,
    /// copy and selection keys of a derived viewer belong here.
    /// </summary>
    protected virtual bool OnViewerKeyDown(KeyEventArgs e) => false;

    /// <summary>Gives the keyboard focus to the pages (a tool over them closed).</summary>
    protected void FocusViewport(NavigationMethod method = NavigationMethod.Unspecified) => _presenter?.Focus(method);

    /// <summary>The source on show was replaced (<see cref="EffectivePageSource"/>).</summary>
    protected virtual void OnPageSourceChanged(IPageSource? oldSource, IPageSource? newSource)
    {
    }

    /// <summary>The page count or a page size changed.</summary>
    protected virtual void OnPagesChanged()
    {
    }

    /// <summary>
    /// A bitmap of a page at any size from the shared render cache, for other views of the pages such as thumbnails. Null
    /// means it is on its way, behind what the main view needs: <see cref="OnPageBitmapReady"/> tells when to ask again.
    /// Do not keep the result; draw it and ask again next time, because it may be evicted and disposed.
    /// </summary>
    protected Bitmap? GetPageBitmap(int pageIndex, PixelSize targetSize) => _scheduler.GetAuxiliaryBitmap(pageIndex, targetSize);

    /// <summary>
    /// The same bitmap as <see cref="GetPageBitmap"/> if the cache has it, without asking for anything: safe in a render pass,
    /// where a request (which can finish inline and invalidate visuals) is not.
    /// </summary>
    protected Bitmap? TryGetPageBitmap(int pageIndex, PixelSize targetSize) => _scheduler.PeekAuxiliaryBitmap(pageIndex, targetSize);

    /// <summary>Takes back one request of <see cref="GetPageBitmap"/> that has not started (its view scrolled away).</summary>
    protected void CancelPageBitmapRequest(int pageIndex, PixelSize targetSize) => _scheduler.DropQueuedAuxiliary(pageIndex, targetSize);

    /// <summary>A request of <see cref="GetPageBitmap"/> finished (with or without a bitmap).</summary>
    protected virtual void OnPageBitmapReady(int pageIndex, PixelSize targetSize)
    {
    }

    /// <summary>Forgets what <see cref="GetPageBitmap"/> asked for and has not received (the other view was hidden).</summary>
    protected void CancelPageBitmapRequests() => _scheduler.CancelAuxiliary();

    #endregion

    #region Template and tree

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _presenter?.SetOwner(null);
        _toolbar?.SetOwner(null);
        _presenter = e.NameScope.Find<PageViewerPresenter>(PresenterPart);
        _toolbar = e.NameScope.Find<PageViewerToolbar>(ToolbarPart);
        _leadingHost = e.NameScope.Find<ContentPresenter>(LeadingPanePart);
        _overlayHost = e.NameScope.Find<ContentPresenter>(OverlayPart);
        if (_leadingHost is { } leading)
        {
            leading.Content = _leadingPane;
        }
        if (_overlayHost is { } overlay)
        {
            overlay.Content = _contentOverlay;
        }
        _toolbar?.SetOwner(this);
        _presenter?.SetOwner(this);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
        // The collection or source may have moved on while the viewer was away, and its bitmaps were dropped on the way out.
        ReloadPages();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
        if (_zoomMotion is { IsAnimating: true })
        {
            _zoomMotion.Jump(_zoomMotion.Target);
        }
        _scheduler.Reset();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageViewerAutomationPeer(this);

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == PageSourceProperty)
        {
            UpdateSource(change.Property == ItemsSourceProperty);
        }
        else if (change.Property == ZoomProperty)
        {
            OnZoomChanged(change.GetNewValue<double>());
        }
        else if (change.Property == MinZoomProperty || change.Property == MaxZoomProperty)
        {
            _zoomChange = new ZoomChange(null, false, KeepsFit: true, CallerRelayouts: false);
            CoerceValue(ZoomProperty);
            _zoomChange = default;
        }
        else if (change.Property == FitModeProperty)
        {
            ApplyFit(animate: VisualRoot is not null);
            _toolbar?.Sync();
        }
        else if (change.Property == LayoutModeProperty)
        {
            OnLayoutModeChanged(change.GetNewValue<PageLayoutMode>());
        }
        else if (change.Property == InteractionModeProperty)
        {
            PseudoClasses.Set(PanPseudoClass, change.GetNewValue<PageInteractionMode>() == PageInteractionMode.Pan);
            _presenter?.OnInteractionModeChanged();
            _toolbar?.Sync();
        }
        else if (change.Property == CurrentPageIndexProperty)
        {
            OnCurrentPageIndexChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (change.Property == RenderCacheBytesProperty)
        {
            _scheduler.Budget = change.GetNewValue<long>();
        }
        else if (change.Property == PageCountProperty)
        {
            PseudoClasses.Set(EmptyPseudoClass, change.GetNewValue<int>() == 0);
        }
        else if (change.Property == IsLoadingProperty)
        {
            PseudoClasses.Set(LoadingPseudoClass, change.GetNewValue<bool>());
        }
    }

    private static int CoerceCurrentPageIndex(AvaloniaObject target, int value)
    {
        var viewer = (PageViewer)target;
        // While there are no pages the value waits, so XAML may set it before the source.
        var count = viewer.PageCount;
        return count == 0 ? Math.Max(0, value) : Math.Clamp(value, 0, count - 1);
    }

    private static double CoerceZoom(AvaloniaObject target, double value)
    {
        var viewer = (PageViewer)target;
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return viewer.Zoom;
        }
        var min = Math.Max(0.001, viewer.MinZoom);
        return Math.Clamp(value, min, Math.Max(min, viewer.MaxZoom));
    }

    private void UpdateSource(bool itemsChanged)
    {
        if (itemsChanged)
        {
            _itemsWrapper = ItemsSource is { } items ? new ImagePageSource(items) : null;
        }
        IPageSource? source = PageSource ?? _itemsWrapper;
        if (ReferenceEquals(source, _source))
        {
            return;
        }
        var old = _source;
        Unsubscribe();
        _source = source;
        _scheduler.SetSource(source);
        if (VisualRoot is not null)
        {
            Subscribe();
        }
        ReloadPages();
        OnPageSourceChanged(old, source);
    }

    private void Subscribe()
    {
        if (!_subscribed && _source is { } source)
        {
            source.PagesChanged += OnSourcePagesChanged;
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed && _source is { } source)
        {
            source.PagesChanged -= OnSourcePagesChanged;
        }
        _subscribed = false;
    }

    private void OnSourcePagesChanged(object? sender, EventArgs e)
    {
        _scheduler.Reset();
        var anchor = CenterAnchor();
        ReloadPages(anchor);
        OnPagesChanged();
    }

    private PageAnchor? CenterAnchor()
    {
        if (_presenter is { } presenter && _layout.PageCount > 0 && presenter.ViewportSize.Height > 0)
        {
            return presenter.CaptureAnchor(new Point(presenter.ViewportSize.Width / 2, presenter.ViewportSize.Height / 2));
        }
        return null;
    }

    /// <summary>Reads the pages from the source and brings the layout, the zoom and the toolbar in line.</summary>
    private void ReloadPages(PageAnchor? anchor = null)
    {
        var source = _source;
        var count = source?.PageCount ?? 0;
        _layout.SetPages(count, i => source!.GetPageSize(i));
        PageCount = count;
        CoerceValue(CurrentPageIndexProperty);
        _layout.SingleIndex = CurrentPageIndex;
        _layout.Mode = LayoutMode;
        ApplyFit(animate: false);
        // Without a point to keep in place, the view starts at the current page.
        _pendingJump = anchor is null;
        if (_presenter is { } presenter)
        {
            presenter.OnLayoutChanged(anchor);
            RunPendingJump(presenter);
        }
        _toolbar?.Sync();
    }

    private void RunPendingJump(PageViewerPresenter presenter)
    {
        if (_pendingJump && presenter.ViewportSize.Height > 0 && _layout.PageCount > 0)
        {
            _pendingJump = false;
            JumpToPage(CurrentPageIndex, animate: false);
        }
    }

    private void OnLayoutModeChanged(PageLayoutMode mode)
    {
        _layout.Mode = mode;
        _layout.SingleIndex = CurrentPageIndex;
        _holdsCurrent = false;
        ApplyFit(animate: false);
        _presenter?.OnLayoutChanged();
        JumpToPage(CurrentPageIndex, animate: false);
        _toolbar?.Sync();
    }

    private void OnCurrentPageIndexChanged(int oldIndex, int newIndex)
    {
        if (!_updatingCurrent)
        {
            var animate = _nextJumpAnimates;
            _nextJumpAnimates = false;
            JumpToPage(newIndex, animate);
        }
        _toolbar?.Sync();
        RaiseEvent(new PageChangedEventArgs(CurrentPageChangedEvent, oldIndex, newIndex));
        if (ControlAutomationPeer.FromElement(this) is PageViewerAutomationPeer peer)
        {
            peer.OnCurrentPageChanged();
        }
    }

    private void JumpToPage(int index, bool animate)
    {
        if (_presenter is not { } presenter || presenter.ViewportSize.Height <= 0 || _layout.PageCount == 0)
        {
            _layout.SingleIndex = index;
            _pendingJump = true;
            return;
        }
        if (LayoutMode == PageLayoutMode.Single)
        {
            _layout.SingleIndex = index;
            ApplyFit(animate: false);
            presenter.StopScrollMotion();
            presenter.OnLayoutChanged();
            presenter.ScrollTo(new Vector(presenter.Offset.X, 0), animate: false);
            return;
        }
        var rect = _layout.GetPageRect(index, presenter.ViewportSize);
        var target = presenter.ClampOffset(new Vector(presenter.Offset.X, rect.Y - _layout.Padding));
        // Until the view moves for another reason this page stays current, even when another page ends up at the middle
        // (small pages, or the last page, which cannot scroll to the top).
        _holdsCurrent = true;
        _heldOffset = target;
        presenter.ScrollTo(target, animate);
    }

    private static double ShiftIntoView(double offset, double viewportLength, double start, double end)
    {
        if (start >= offset && end <= offset + viewportLength)
        {
            return offset;
        }
        return end - start > viewportLength || start < offset ? start : end - viewportLength;
    }

    #endregion

    #region Zoom

    /// <summary>Zooms to a value on a spring about the viewport center (the toolbar's zoom menu).</summary>
    internal void ZoomToStep(double zoom) => ChangeZoom(zoom, null, animate: true);

    /// <summary>Multiplies the zoom about a point of the presenter, at once (wheel and pinch follow the hand).</summary>
    internal void ZoomBy(double factor, Point anchor) => ChangeZoom(Zoom * factor, anchor, animate: false);

    private void ChangeZoom(double target, Point? anchor, bool animate)
    {
        var coerced = CoerceZoom(this, target);
        if (coerced == Zoom)
        {
            SetCurrentValue(FitModeProperty, PageFitMode.None);
            return;
        }
        _zoomChange = new ZoomChange(anchor, animate, KeepsFit: false, CallerRelayouts: false);
        SetCurrentValue(ZoomProperty, coerced);
        _zoomChange = default;
    }

    private void OnZoomChanged(double zoom)
    {
        var change = _zoomChange;
        _zoomChange = default;
        if (!change.KeepsFit && FitMode != PageFitMode.None)
        {
            SetCurrentValue(FitModeProperty, PageFitMode.None);
        }
        if (!change.CallerRelayouts)
        {
            ApplyDisplayZoom(zoom, change.Anchor, change.Animate);
        }
        _toolbar?.Sync();
    }

    /// <summary>
    /// Moves the zoom on screen. Code-driven (<see cref="MotionValue{T}"/>, spec section 8): the zoom decides the virtual
    /// layout and the anchor, which a ControlTheme transition cannot reach. <see cref="Zoom"/> itself holds the target.
    /// </summary>
    private void ApplyDisplayZoom(double zoom, Point? anchorPoint, bool animate)
    {
        if (_presenter is not { } presenter || presenter.ViewportSize.Width <= 0 || presenter.ViewportSize.Height <= 0 || _layout.PageCount == 0)
        {
            _zoomMotion?.Stop();
            _layout.Zoom = zoom;
            return;
        }
        var point = anchorPoint ?? new Point(presenter.ViewportSize.Width / 2, presenter.ViewportSize.Height / 2);
        _zoomAnchor = presenter.CaptureAnchor(point);
        if (animate && VisualRoot is not null)
        {
            if (_zoomMotion is null)
            {
                _zoomMotion = new MotionValue<double>(this, _layout.Zoom, MotionVectors.Double);
                _zoomMotion.Changed += SetDisplayZoom;
            }
            else if (!_zoomMotion.IsAnimating)
            {
                _zoomMotion.Jump(_layout.Zoom);
            }
            _ = _zoomMotion.AnimateTo(zoom, MotionSpec.Spring(ArcSpring.Snappy));
            return;
        }
        if (_zoomMotion is { IsAnimating: true })
        {
            _zoomMotion.Stop();
        }
        SetDisplayZoom(zoom);
    }

    private void SetDisplayZoom(double zoom)
    {
        _layout.Zoom = zoom;
        _presenter?.OnLayoutChanged(_zoomAnchor);
    }

    /// <summary>
    /// Works the zoom out from <see cref="FitMode"/> for the current viewport. With <paramref name="animate"/> it moves about
    /// the viewport center; otherwise the layout takes the new zoom at once and the caller lays out (keeping its own anchor).
    /// </summary>
    private void ApplyFit(bool animate)
    {
        if (FitMode == PageFitMode.None || _presenter is not { } presenter || _layout.GetFitZoom(FitMode, presenter.ViewportSize) is not { } fit)
        {
            return;
        }
        var zoom = CoerceZoom(this, fit);
        if (!animate)
        {
            if (_zoomMotion is { IsAnimating: true })
            {
                _zoomMotion.Stop();
            }
            _layout.Zoom = zoom;
        }
        if (zoom != Zoom)
        {
            _zoomChange = new ZoomChange(null, animate, KeepsFit: true, CallerRelayouts: !animate);
            SetCurrentValue(ZoomProperty, zoom);
            _zoomChange = default;
        }
    }

    #endregion

    #region Presenter callbacks

    /// <summary>The presenter got a new size (or new canvas metrics).</summary>
    internal void OnPresenterResized(PageAnchor? anchor)
    {
        ApplyFit(animate: false);
        if (_presenter is { } presenter)
        {
            presenter.OnLayoutChanged(anchor);
            RunPendingJump(presenter);
        }
    }

    /// <summary>The view moved: plan rendering and work out the current page.</summary>
    internal void OnPresenterViewChanged()
    {
        if (_presenter is not { } presenter)
        {
            return;
        }
        var viewport = presenter.ViewportSize;
        var offset = presenter.Offset;
        var (first, last) = _layout.GetVisibleRange(offset.Y, offset.Y + viewport.Height, viewport.Height);
        if (viewport.Width > 0 && viewport.Height > 0 && VisualRoot is not null)
        {
            _scheduler.Update(new PageViewState(first, last, _layout.Zoom, presenter.RenderScale, new Rect(offset.X, offset.Y, viewport.Width, viewport.Height)));
        }
        UpdateCurrentFromOffset(presenter);
    }

    private void UpdateCurrentFromOffset(PageViewerPresenter presenter)
    {
        if (LayoutMode == PageLayoutMode.Single || _layout.PageCount == 0 || presenter.ViewportSize.Height <= 0)
        {
            return;
        }
        if (_holdsCurrent)
        {
            if (presenter.IsScrollAnimating)
            {
                return;
            }
            var distance = presenter.Offset - _heldOffset;
            if (Math.Abs(distance.X) <= 0.5 && Math.Abs(distance.Y) <= 0.5)
            {
                return;
            }
            _holdsCurrent = false;
        }
        if (_pendingJump)
        {
            return;
        }
        var index = _layout.PageIndexAt(presenter.Offset.Y + presenter.ViewportSize.Height / 2, presenter.ViewportSize.Height);
        if (index != CurrentPageIndex)
        {
            _updatingCurrent = true;
            SetCurrentValue(CurrentPageIndexProperty, index);
            _updatingCurrent = false;
        }
    }

    /// <summary>The presenter started or ended a pointer pan.</summary>
    internal void SetPanning(bool panning) => PseudoClasses.Set(PanningPseudoClass, panning);

    /// <summary>A key on the viewport. True when it was used.</summary>
    internal bool HandleViewportKey(KeyEventArgs e)
    {
        if (OnViewerKeyDown(e) || HandleZoomKey(e))
        {
            return true;
        }
        if (e.KeyModifiers != KeyModifiers.None || _presenter is not { } presenter)
        {
            return false;
        }
        switch (e.Key)
        {
            case Key.Up:
                presenter.ScrollBy(new Vector(0, -ArrowKeyStep));
                return true;
            case Key.Down:
                presenter.ScrollBy(new Vector(0, ArrowKeyStep));
                return true;
            case Key.Left:
                presenter.ScrollBy(new Vector(-ArrowKeyStep, 0));
                return true;
            case Key.Right:
                presenter.ScrollBy(new Vector(ArrowKeyStep, 0));
                return true;
            case Key.PageUp:
                if (LayoutMode == PageLayoutMode.Single)
                {
                    GoToPage(CurrentPageIndex - 1, animate: false);
                }
                else
                {
                    presenter.ScrollBy(new Vector(0, -presenter.ViewportSize.Height));
                }
                return true;
            case Key.PageDown:
                if (LayoutMode == PageLayoutMode.Single)
                {
                    GoToPage(CurrentPageIndex + 1, animate: false);
                }
                else
                {
                    presenter.ScrollBy(new Vector(0, presenter.ViewportSize.Height));
                }
                return true;
            case Key.Home:
                GoToPage(0);
                return true;
            case Key.End:
                GoToPage(PageCount - 1);
                return true;
            default:
                return false;
        }
    }

    private bool HandleZoomKey(KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0)
        {
            return false;
        }
        switch (e.Key)
        {
            case Key.OemPlus:
            case Key.Add:
                ZoomIn();
                return true;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomOut();
                return true;
            case Key.D0:
            case Key.NumPad0:
                ResetZoom();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Keys that bubbled up from the toolbar: the zoom shortcuts work there too, and the extension point sees them.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // The presenter already offered its own keys to the same handlers.
        if (!e.Handled && e.Source is not PageViewerPresenter && (OnViewerKeyDown(e) || HandleZoomKey(e)))
        {
            e.Handled = true;
        }
    }

    private void OnSchedulerChanged() => _presenter?.InvalidateVisual();

    private void OnSchedulerLoadingChanged() => IsLoading = _scheduler.IsLoading;

    private void OnLayersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is { } oldItems)
        {
            foreach (PageLayer layer in oldItems)
            {
                layer.InvalidateRequested -= OnLayerInvalidated;
            }
        }
        if (e.NewItems is { } newItems)
        {
            foreach (PageLayer layer in newItems)
            {
                layer.InvalidateRequested += OnLayerInvalidated;
            }
        }
        _presenter?.InvalidateVisual();
    }

    private void OnLayerInvalidated() => _presenter?.OnLayerInvalidated();

    #endregion
}

/// <summary>Exposes a <see cref="PageViewer"/> as a document named after its position ("Page 3 of 12"), also through the value pattern.</summary>
public class PageViewerAutomationPeer : ControlAutomationPeer, IValueProvider
{
    /// <summary>Creates the peer of a viewer.</summary>
    public PageViewerAutomationPeer(PageViewer owner)
        : base(owner)
    {
    }

    /// <summary>The viewer.</summary>
    public new PageViewer Owner => (PageViewer)base.Owner;

    /// <inheritdoc />
    public bool IsReadOnly => true;

    /// <inheritdoc />
    public string? Value => PositionText();

    /// <inheritdoc />
    public void SetValue(string? value) => throw new InvalidOperationException("The value of a page viewer is read-only.");

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

    /// <inheritdoc />
    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : PositionText();

    /// <inheritdoc />
    protected override bool IsContentElementCore() => true;

    /// <inheritdoc />
    protected override bool IsControlElementCore() => true;

    internal void OnCurrentPageChanged()
    {
        var text = PositionText();
        RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, null, text);
        RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, null, text);
    }

    private string PositionText()
    {
        var viewer = Owner;
        return PageViewerLangResources.Format(PageViewerLangResourceKind.PageOfTotal,
            viewer.PageCount == 0 ? 0 : viewer.CurrentPageIndex + 1, viewer.PageCount);
    }
}
