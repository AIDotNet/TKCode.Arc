using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The scroll region of an <see cref="ExplodedView"/> in container mode: a native <c>ScrollViewer</c> over an empty track
/// (wheel, touch and scroll bars stay native) with the block's key model: ↑/↓ 40 px, PageUp/PageDown/Space/Shift+Space
/// the viewport less 40 px, Home/End the two ends; ←/→ do nothing. No snapping.
/// </summary>
internal sealed class ExplodedViewScrollViewer : ScrollViewer
{
    /// <summary>Distance of an arrow key.</summary>
    public const double LineStep = 40;

    /// <summary>A page key scrolls the viewport height less this.</summary>
    public const double PageOverlap = 40;

    public ExplodedViewScrollViewer()
    {
        // Tunnel, so the frame answers before its presenter (Avalonia's 50 px step): a notch scrolls 100 px, as in a
        // browser, and the frame keeps the wheel at its ends instead of passing it to the page (overscroll contained).
        AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
    }

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled || VerticalScrollBarVisibility == ScrollBarVisibility.Disabled || e.Delta.Y == 0)
        {
            return;
        }
        var scrollable = Extent.Height - Viewport.Height;
        if (scrollable <= 0)
        {
            return;
        }
        Offset = new Vector(Offset.X, Math.Clamp(Offset.Y + ShowpieceWheel.WebDeltaY(e.Delta.Y), 0, scrollable));
        e.Handled = true;
    }

    /// <summary>Raised when keyboard focus arrives (true) or focus leaves (false); pointer focus shows no ring.</summary>
    public event Action<bool>? FocusRingChanged;

    /// <summary>The offset a key moves to from <paramref name="offset"/>, or null when the key does not scroll.</summary>
    internal static double? KeyTarget(Key key, KeyModifiers modifiers, double offset, double viewport, double scrollable)
    {
        var page = Math.Max(LineStep, viewport - PageOverlap);
        double? target = key switch
        {
            Key.Up => offset - LineStep,
            Key.Down => offset + LineStep,
            Key.PageUp => offset - page,
            Key.PageDown => offset + page,
            Key.Space when (modifiers & KeyModifiers.Shift) != 0 => offset - page,
            Key.Space => offset + page,
            Key.Home => 0,
            Key.End => scrollable,
            _ => null
        };
        return target is { } t ? Math.Clamp(t, 0, Math.Max(0, scrollable)) : null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && IsEnabled && VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
        {
            if (e.Key is Key.Left or Key.Right)
            {
                e.Handled = true;
                return;
            }
            var scrollable = Extent.Height - Viewport.Height;
            if (KeyTarget(e.Key, e.KeyModifiers, Offset.Y, Viewport.Height, scrollable) is { } target)
            {
                Offset = new Vector(Offset.X, target);
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Focus events bubble: a stop button inside the frame taking focus is not the frame's own focus.
        FocusRingChanged?.Invoke(ReferenceEquals(e.Source, this) && (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional));
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        FocusRingChanged?.Invoke(IsFocused && Classes.Contains(":focus-visible"));
    }
}

/// <summary>
/// The scroll track: as tall as the scroll distance needs, with the sticky part (one child) kept in view by a translation
/// (Avalonia has no <c>position: sticky</c>). The sticky part is a descendant of the scroll viewer, so wheel, touch and
/// keys over it still scroll natively. Transparent so the whole frame takes the wheel.
/// </summary>
internal sealed class ExplodedViewTrack : Panel
{
    private readonly TranslateTransform _pin = new();
    private double _trackHeight;
    private double _stickyHeight;

    public ExplodedViewTrack()
    {
        Background = Brushes.Transparent;
    }

    /// <summary>The track's height (the scroll extent in container mode).</summary>
    public double TrackHeight
    {
        get => _trackHeight;
        set
        {
            if (!_trackHeight.Equals(value))
            {
                _trackHeight = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>Height of the pinned part.</summary>
    public double StickyHeight
    {
        get => _stickyHeight;
        set
        {
            if (!_stickyHeight.Equals(value))
            {
                _stickyHeight = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>How far the pinned part is moved down the track (the scroll offset, clamped).</summary>
    public double StickyOffset => _pin.Y;

    public void SetStickyOffset(double offset)
    {
        var clamped = Math.Clamp(offset, 0, Math.Max(0, _trackHeight - _stickyHeight));
        if (!_pin.Y.Equals(clamped))
        {
            _pin.Y = clamped;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        foreach (var child in Children)
        {
            child.RenderTransform = _pin;
            child.Measure(new Size(width, _stickyHeight));
        }
        return new Size(width, _trackHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, _stickyHeight));
        }
        return finalSize;
    }
}

/// <summary>
/// The pinned part's layout: intro, stage, caption and stop row placed by <see cref="ExplodedViewLayout"/> for the current
/// breakpoint. Children are found by type; the remaining child is the intro.
/// </summary>
internal sealed class ExplodedViewLayoutPanel : Panel
{
    private ExplodedViewMetrics _metrics = ExplodedViewLayout.Metrics(745);
    private int _stopCount = 2;

    public ExplodedViewMetrics Metrics
    {
        get => _metrics;
        set
        {
            if (_metrics != value)
            {
                _metrics = value;
                InvalidateMeasure();
            }
        }
    }

    public int StopCount
    {
        get => _stopCount;
        set
        {
            if (_stopCount != value)
            {
                _stopCount = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The last arranged regions (for tests and the stage's composition).</summary>
    public ExplodedViewRects Rects { get; private set; }

    private ExplodedViewRects Compute(Size size, out Control? intro, out ExplodedViewStage? stage, out ExplodedViewCaptions? captions,
                                      out ExplodedViewStops? stops, bool measure)
    {
        intro = null;
        stage = null;
        captions = null;
        stops = null;
        foreach (var child in Children)
        {
            switch (child)
            {
                case ExplodedViewStage s:
                    stage = s;
                    break;
                case ExplodedViewCaptions c:
                    captions = c;
                    break;
                case ExplodedViewStops t:
                    stops = t;
                    break;
                default:
                    intro ??= child;
                    break;
            }
        }
        var introWidth = ExplodedViewLayout.IntroWidth(_metrics, size.Width);
        var captionWidth = ExplodedViewLayout.CaptionWidth(_metrics, size.Width, _stopCount);
        if (measure)
        {
            intro?.Measure(new Size(introWidth, double.PositiveInfinity));
            captions?.Measure(new Size(captionWidth, double.PositiveInfinity));
        }
        var introHeight = intro is { IsVisible: true } ? intro.DesiredSize.Height : 0;
        var captionHeight = captions?.DesiredSize.Height ?? 0;
        return ExplodedViewLayout.Arrange(_metrics, size, introHeight, captionHeight, _stopCount);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
        var rects = Compute(size, out _, out var stage, out _, out var stops, measure: true);
        stage?.Measure(rects.Stage.Size);
        if (stops is not null)
        {
            stops.ItemWidth = rects.StopWidth;
            stops.Measure(rects.Stops.Size);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rects = Compute(finalSize, out var intro, out var stage, out var captions, out var stops, measure: false);
        Rects = rects;
        intro?.Arrange(rects.Intro);
        stage?.Arrange(rects.Stage);
        captions?.Arrange(rects.Caption);
        stops?.Arrange(rects.Stops);
        return finalSize;
    }
}

/// <summary>
/// The 3D stage: one <see cref="ExplodedViewLayerHost"/> per layer, drawn through a projective <c>RenderTransform</c>
/// (layers are parallel planes, painted bottom first), and one flat <see cref="ExplodedViewLabel"/> per layer placed at the
/// projection of its anchor (text never tilts). Not hit-testable and hidden from automation (the block lists the layers
/// in text). The fit is recomputed only when the stage size, the composition or the label widths change.
/// </summary>
/// <remarks>
/// <para>
/// While the scene moves (<see cref="IsMoving"/>), every layer's picture and outline are drawn from a bitmap cache rendered
/// flat at the stage's largest on-screen scale. Under a perspective matrix the GPU backend cannot use its fast paths for
/// text, rounded clips, strokes, dashes and shadows (each falls back to software masks or paths every frame, about 10 ms
/// for the sample at 2x); a cached layer is one textured quad. At rest the layers are vectors again, so resting poses stay
/// sharp and the bitmaps are released.
/// </para>
/// <para>
/// A dimmed layer keeps its picture cached at rest too: drawn from a bitmap it fades as one picture (CSS group opacity, as
/// in Arc), the same while moving and at rest, whereas Avalonia's default opacity (multiplied into every brush) lets the
/// overlapping parts of a dimmed layer show through each other. Asking for full opacity handling instead would turn every
/// opacity inside the layers into an offscreen layer under the perspective (about three times the frame cost).
/// </para>
/// </remarks>
internal sealed class ExplodedViewStage : Panel
{
    /// <summary>The largest magnification perspective adds to a layer point (the near edge of the stack, about 1.22).</summary>
    private const double PerspectiveMagnification = 1.25;

    private readonly List<ExplodedViewLayerHost> _hosts = [];
    private readonly List<ExplodedViewLabel> _labels = [];
    private readonly List<ExplodedLayer> _layers = [];
    private readonly BitmapCache _cache = new();
    private bool _isMoving;
    private bool _allowCache;
    private ExplodedFit _fit;
    private bool _fitValid;
    private Size _fitSize;
    private double _fitLabelWidth;
    private bool _isTall;
    private double _tilt;
    private double _spread;
    private int _active = -1;
    private Size _layerSize = new(280, 620);
    private double _perspective = ExplodedViewGeometry.DefaultPerspective;
    private ExplodedComposition _wide = ExplodedComposition.Wide;
    private ExplodedComposition _tall = ExplodedComposition.Tall;
    private bool _isCompact;
    private double _dimmedOpacity = 0.5;
    private double _cornerRadius = 40;
    private Size _size;

    public ExplodedViewStage()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        Focusable = false;
        // A picture, like Arc's aria-hidden stage: no pointer, no Tab stop inside the layers, no automation subtree.
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HiddenAutomationPeer(this);

    /// <summary>Exposes nothing below the stage (the block lists the layers in text instead).</summary>
    private sealed class HiddenAutomationPeer(Control owner) : ControlAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        protected override bool IsContentElementCore() => false;

        protected override bool IsControlElementCore() => false;
    }

    /// <summary>Raised when the composition switches between wide and tall (compact control, stage taller than wide).</summary>
    public event Action<bool>? TallChanged;

    public bool IsTall => _isTall;

    public ExplodedComposition Composition => _isTall ? _tall : _wide;

    public ExplodedFit Fit => _fit;

    public double Tilt => _tilt;

    public double Spread => _spread;

    public int ActiveLayer => _active;

    public IReadOnlyList<ExplodedViewLayerHost> Hosts => _hosts;

    public IReadOnlyList<ExplodedViewLabel> Labels => _labels;

    /// <summary>Layout passes of the stage itself (tests: scrolling and the per-frame pose never lay the stage out).</summary>
    public int MeasureCount { get; private set; }

    /// <inheritdoc cref="MeasureCount"/>
    public int ArrangeCount { get; private set; }

    /// <summary>The cache shared by the layers; its scale follows the fit.</summary>
    public BitmapCache Cache => _cache;

    /// <summary>
    /// True while the scene moves: the layers draw from bitmap caches. False at rest: vectors, except the pictures of dimmed
    /// layers.
    /// </summary>
    public bool IsMoving
    {
        get => _isMoving;
        set
        {
            if (_isMoving != value)
            {
                _isMoving = value;
                ApplyCaching();
            }
        }
    }

    /// <summary>
    /// Whether the layers may be drawn from bitmaps at all: true when the compositor renders on a GPU (set by the view,
    /// see <see cref="ShowpieceRenderer"/>); false keeps every layer a vector, as before.
    /// </summary>
    public bool AllowCache
    {
        get => _allowCache;
        set
        {
            if (_allowCache != value)
            {
                _allowCache = value;
                ApplyCaching();
            }
        }
    }

    /// <summary>Whether layer <paramref name="index"/> is dimmed (another layer has the focus and dimming is on).</summary>
    public bool IsDimmed(int index) => _active >= 0 && index != _active && _dimmedOpacity < 1;

    private void ApplyCaching()
    {
        for (var i = 0; i < _hosts.Count; i++)
        {
            var picture = _allowCache && (_isMoving || IsDimmed(i));
            var outline = _allowCache && _isMoving;
            _hosts[i].SetCache(picture ? _cache : null, outline ? _cache : null);
        }
    }

    public Size LayerSize
    {
        get => _layerSize;
        set
        {
            if (_layerSize != value)
            {
                _layerSize = value;
                foreach (var host in _hosts)
                {
                    host.Width = value.Width;
                    host.Height = value.Height;
                }
                Invalidate();
            }
        }
    }

    public double Perspective
    {
        get => _perspective;
        set
        {
            if (!_perspective.Equals(value))
            {
                _perspective = value;
                Invalidate();
            }
        }
    }

    public double LayerCornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (!_cornerRadius.Equals(value))
            {
                _cornerRadius = value;
                foreach (var host in _hosts)
                {
                    host.CornerRadius = new CornerRadius(value);
                }
            }
        }
    }

    public double DimmedOpacity
    {
        get => _dimmedOpacity;
        set
        {
            if (!_dimmedOpacity.Equals(value))
            {
                _dimmedOpacity = value;
                ApplyActive();
            }
        }
    }

    public void SetCompositions(ExplodedComposition wide, ExplodedComposition tall)
    {
        if (_wide != wide || _tall != tall)
        {
            _wide = wide;
            _tall = tall;
            Invalidate();
        }
    }

    /// <summary>Compact controls hide the label notes and may use the tall composition.</summary>
    public bool IsCompact
    {
        get => _isCompact;
        set
        {
            if (_isCompact != value)
            {
                _isCompact = value;
                foreach (var label in _labels)
                {
                    label.IsNoteVisible = !value;
                }
                Invalidate();
            }
        }
    }

    private void Invalidate()
    {
        _fitValid = false;
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>Replaces the layers (hosts and labels are rebuilt; the old hosts let go of their content first).</summary>
    public void SetLayers(IReadOnlyList<ExplodedLayer> layers)
    {
        foreach (var host in _hosts)
        {
            // A host builds the template it still has when its content changes: drop the template first (a template reading its
            // data would be built with null).
            host.ContentTemplate = null;
            host.Content = null;
        }
        Children.Clear();
        _hosts.Clear();
        _labels.Clear();
        _layers.Clear();
        _layers.AddRange(layers);
        for (var i = 0; i < layers.Count; i++)
        {
            var host = new ExplodedViewLayerHost
            {
                Width = _layerSize.Width,
                Height = _layerSize.Height,
                CornerRadius = new CornerRadius(_cornerRadius)
            };
            _hosts.Add(host);
            Children.Add(host);
        }
        for (var i = 0; i < layers.Count; i++)
        {
            var label = new ExplodedViewLabel { IsNoteVisible = !_isCompact, Opacity = 0 };
            _labels.Add(label);
            Children.Add(label);
        }
        for (var i = 0; i < layers.Count; i++)
        {
            RefreshLayer(i);
        }
        _active = -1;
        Invalidate();
        ApplyActive();
    }

    /// <summary>Re-reads one layer's kind, content and texts.</summary>
    public void RefreshLayer(int index)
    {
        if (index < 0 || index >= _layers.Count)
        {
            return;
        }
        var layer = _layers[index];
        var host = _hosts[index];
        host.Kind = layer.Kind;
        // Template last and only with content: a template attached to empty content is built with null.
        var template = layer.Content is null ? null : layer.ContentTemplate;
        if (!ReferenceEquals(host.Content, layer.Content))
        {
            host.ContentTemplate = null;
            host.Content = layer.Content;
            host.ContentTemplate = template;
        }
        else
        {
            host.ContentTemplate = template;
        }
        var label = _labels[index];
        label.LabelName = layer.Name;
        label.Note = layer.Note;
        _fitValid = false;
        InvalidateArrange();
    }

    /// <summary>Sets the pose of the scene and writes the per-frame transforms (no layout).</summary>
    public void SetPose(double tilt, double spread)
    {
        _tilt = tilt;
        _spread = spread;
        ApplyPose();
    }

    /// <summary>Focuses a layer (−1: none). The others dim to <see cref="DimmedOpacity"/>.</summary>
    public void SetActive(int index)
    {
        if (_active != index)
        {
            _active = index;
            ApplyActive();
        }
    }

    private void ApplyActive()
    {
        for (var i = 0; i < _hosts.Count; i++)
        {
            var isActive = i == _active;
            _hosts[i].IsActive = isActive;
            _hosts[i].Opacity = _active < 0 || isActive ? 1 : _dimmedOpacity;
            _labels[i].IsActive = isActive;
        }
        ApplyCaching();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        MeasureCount++;
        foreach (var host in _hosts)
        {
            host.Measure(_layerSize);
        }
        var widest = 0.0;
        foreach (var label in _labels)
        {
            label.Measure(Size.Infinity);
            widest = Math.Max(widest, label.DesiredSize.Width);
        }
        if (!widest.Equals(_fitLabelWidth))
        {
            _fitValid = false;
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : _layerSize.Width,
            double.IsFinite(availableSize.Height) ? availableSize.Height : _layerSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ArrangeCount++;
        foreach (var host in _hosts)
        {
            host.Arrange(new Rect(_layerSize));
        }
        foreach (var label in _labels)
        {
            label.Arrange(new Rect(label.DesiredSize));
        }
        // Bounds are only updated after this returns, so the pose uses the arranged size.
        _size = finalSize;
        EnsureFit(finalSize);
        ApplyPose();
        return finalSize;
    }

    private void EnsureFit(Size size)
    {
        var tall = _isCompact && size.Height > size.Width;
        if (tall != _isTall)
        {
            _isTall = tall;
            _fitValid = false;
            TallChanged?.Invoke(tall);
        }
        if (_fitValid && _fitSize == size)
        {
            return;
        }
        var widest = 0.0;
        foreach (var label in _labels)
        {
            widest = Math.Max(widest, label.DesiredSize.Width);
        }
        var composition = Composition;
        var layers = _layers;
        var bounds = ExplodedViewGeometry.MeasureExploded(_layerSize, layers.Count, composition, _perspective,
            i => layers[i].AnchorFromBottom);
        _fit = ExplodedViewGeometry.Fit(size, _layerSize, bounds, widest);
        _fitSize = size;
        _fitLabelWidth = widest;
        _fitValid = true;
        var renderAtScale = CacheScale(_fit);
        if (!_cache.RenderAtScale.Equals(renderAtScale))
        {
            _cache.RenderAtScale = renderAtScale;
        }
    }

    /// <summary>
    /// The scale the layer bitmaps are rendered at (times the screen's scaling): the largest a layer appears on the stage
    /// (flat, or exploded and magnified by perspective), rounded up to a quarter so small resizes keep the bitmaps.
    /// </summary>
    internal static double CacheScale(in ExplodedFit fit) =>
        ShowpieceRenderer.CacheScale(Math.Max(fit.FlatScale, fit.ExplodedScale * PerspectiveMagnification));

    private void ApplyPose()
    {
        var size = _size;
        if (_hosts.Count == 0 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (!_fitValid || _fitSize != size)
        {
            EnsureFit(size);
        }
        var composition = Composition;
        var count = _hosts.Count;
        var scale = _fit.ScaleAt(_tilt);
        var offset = _fit.OffsetAt(_tilt);
        var origin = new Point(size.Width / 2 + offset.X, size.Height / 2 + offset.Y);
        var tiltDegrees = composition.Tilt * _tilt;
        var turnDegrees = composition.Turn * _tilt;
        for (var i = 0; i < count; i++)
        {
            var layer = _layers[i];
            var host = _hosts[i];
            var z = ExplodedViewProgress.LayerDepth(i, count, composition.Gap, _spread);
            host.SetMatrix(ExplodedViewGeometry.LayerMatrix(_layerSize, z, tiltDegrees, turnDegrees, _perspective, scale, origin));
            host.SetSpread(_spread);
            var anchor = ExplodedViewGeometry.Project(_layerSize.Width / 2, _layerSize.Height / 2 - layer.AnchorFromBottom, z,
                tiltDegrees, turnDegrees, _perspective);
            _labels[i].Place(origin.X + scale * anchor.X, origin.Y + scale * anchor.Y,
                ExplodedViewProgress.LabelDraw(i, count, _spread));
        }
    }
}

/// <summary>
/// One layer of the stage: its content at the layer size, an opaque plate (kind <see cref="ExplodedLayerKind.Plate"/>), and
/// for sheets a dashed outline under the content (opacity 0.9 × separation; solid accent while focused). Layer opacity
/// (dimming) and the outline color transition in the theme; the projective matrix and the separation are set per frame.
/// </summary>
/// <remarks>
/// Template: <c>PART_Outline</c> (the outline) under <c>PART_Cache</c> (a wrapper around the clipped plate and the content).
/// While the stage moves both take a bitmap cache, and the picture keeps it while the layer is dimmed (see
/// <see cref="ExplodedViewStage"/>). Everything that changes per frame stays outside the cached pictures: the matrix and the
/// dimming on the host, the outline's fade on the outline, the grid's fade on <c>PART_Cache</c>. Moving never re-renders a
/// cache, and the plate's rounded clip is drawn into the bitmap rather than applied under the perspective.
/// </remarks>
[TemplatePart(OutlinePart, typeof(ExplodedViewLayerOutline))]
[TemplatePart(CachePart, typeof(Control))]
[PseudoClasses(PlatePseudoClass, SheetPseudoClass, GridPseudoClass, ActivePseudoClass)]
internal sealed class ExplodedViewLayerHost : ContentControl
{
    public const string PlatePseudoClass = ":plate";
    public const string SheetPseudoClass = ":sheet";
    public const string GridPseudoClass = ":grid";
    public const string ActivePseudoClass = ":active";
    public const string OutlinePart = "PART_Outline";
    public const string CachePart = "PART_Cache";

    /// <summary>Outline color (BorderStrong; Accent while focused).</summary>
    public static readonly StyledProperty<IBrush?> OutlineBrushProperty =
        AvaloniaProperty.Register<ExplodedViewLayerHost, IBrush?>(nameof(OutlineBrush));

    /// <summary>Dashed outline (false while focused: the line turns solid at once).</summary>
    public static readonly StyledProperty<bool> IsOutlineDashedProperty =
        AvaloniaProperty.Register<ExplodedViewLayerHost, bool>(nameof(IsOutlineDashed), true);

    private readonly MatrixTransform _transform = new();
    private ExplodedLayerKind _kind;
    private bool _isActive;
    private double _spread = -1;
    private ExplodedViewLayerOutline? _outline;
    private Control? _cachePart;
    private CacheMode? _pictureCache;
    private CacheMode? _outlineCache;

    static ExplodedViewLayerHost()
    {
        FocusableProperty.OverrideDefaultValue<ExplodedViewLayerHost>(false);
    }

    public ExplodedViewLayerHost()
    {
        RenderTransformOrigin = RelativePoint.TopLeft;
        RenderTransform = _transform;
        IsHitTestVisible = false;
        UpdatePseudoClasses();
    }

    protected override Type StyleKeyOverride => typeof(ExplodedViewLayerHost);

    public IBrush? OutlineBrush
    {
        get => GetValue(OutlineBrushProperty);
        set => SetValue(OutlineBrushProperty, value);
    }

    public bool IsOutlineDashed
    {
        get => GetValue(IsOutlineDashedProperty);
        set => SetValue(IsOutlineDashedProperty, value);
    }

    public Matrix Matrix => _transform.Matrix;

    public double OutlineOpacity => _kind == ExplodedLayerKind.Plate ? 0 : ExplodedViewProgress.OutlineOpacity * Math.Max(0, _spread);

    /// <summary>The outline part (null before the template is applied).</summary>
    public ExplodedViewLayerOutline? Outline => _outline;

    /// <summary>The cached wrapper around the plate and the content (null before the template is applied).</summary>
    public Control? CachedPart => _cachePart;

    /// <summary>True while the picture is drawn from a bitmap cache.</summary>
    public bool IsCached => _pictureCache is not null;

    /// <summary>True while the outline is drawn from a bitmap cache.</summary>
    public bool IsOutlineCached => _outlineCache is not null;

    public ExplodedLayerKind Kind
    {
        get => _kind;
        set
        {
            if (_kind != value)
            {
                _kind = value;
                UpdatePseudoClasses();
                ApplyContentOpacity();
                ApplyOutline();
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                PseudoClasses.Set(ActivePseudoClass, value);
            }
        }
    }

    public void SetMatrix(in Matrix matrix)
    {
        if (_transform.Matrix != matrix)
        {
            _transform.Matrix = matrix;
        }
    }

    public void SetSpread(double spread)
    {
        if (_spread.Equals(spread))
        {
            return;
        }
        _spread = spread;
        ApplyContentOpacity();
        ApplyOutlineOpacity();
    }

    /// <summary>Draws the picture and the outline from these caches (null: as vectors).</summary>
    public void SetCache(CacheMode? picture, CacheMode? outline)
    {
        if (ReferenceEquals(_pictureCache, picture) && ReferenceEquals(_outlineCache, outline))
        {
            return;
        }
        _pictureCache = picture;
        _outlineCache = outline;
        ApplyCache();
    }

    private void ApplyCache()
    {
        ShowpieceRenderer.SetCache(_cachePart, _pictureCache);
        ShowpieceRenderer.SetCache(_outline, _outlineCache);
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(PlatePseudoClass, _kind == ExplodedLayerKind.Plate);
        PseudoClasses.Set(SheetPseudoClass, _kind == ExplodedLayerKind.Sheet);
        PseudoClasses.Set(GridPseudoClass, _kind == ExplodedLayerKind.Grid);
    }

    private void ApplyContentOpacity()
    {
        // On the cached wrapper, not inside it: the grid fades with the separation without re-rendering its bitmap.
        if (_cachePart is not null)
        {
            _cachePart.Opacity = _kind == ExplodedLayerKind.Grid ? Math.Clamp(_spread, 0, 1) : 1;
        }
    }

    private void ApplyOutline()
    {
        _outline?.SetLook(OutlineBrush, IsOutlineDashed, CornerRadius.TopLeft);
        ApplyOutlineOpacity();
    }

    private void ApplyOutlineOpacity()
    {
        if (_outline is not null)
        {
            var opacity = OutlineOpacity;
            // Below the compositor's cut-off nothing is drawn (and no bitmap is made for it).
            _outline.Opacity = opacity <= 0.001 ? 0 : opacity;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_cachePart is not null)
        {
            _cachePart.CacheMode = null;
        }
        if (_outline is not null)
        {
            _outline.CacheMode = null;
        }
        _cachePart = e.NameScope.Find<Control>(CachePart);
        _outline = e.NameScope.Find<ExplodedViewLayerOutline>(OutlinePart);
        ApplyContentOpacity();
        ApplyOutline();
        ApplyCache();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OutlineBrushProperty || change.Property == IsOutlineDashedProperty || change.Property == CornerRadiusProperty)
        {
            ApplyOutline();
        }
    }
}

/// <summary>
/// A layer's outline: a 1 px rounded rectangle along the layer's edge, dashed 3/3 or solid. Its own control (not part of the
/// host's drawing) so it can be cached apart from the picture while its fade follows the separation.
/// </summary>
internal sealed class ExplodedViewLayerOutline : Control
{
    private static readonly ImmutableDashStyle s_dash = new([3, 3], 0);

    private IBrush? _brush;
    private bool _isDashed = true;
    private double _radius;
    private IPen? _pen;

    public ExplodedViewLayerOutline()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    public IBrush? Brush => _brush;

    public bool IsDashed => _isDashed;

    public void SetLook(IBrush? brush, bool dashed, double radius)
    {
        if (ReferenceEquals(_brush, brush) && _isDashed == dashed && _radius.Equals(radius))
        {
            return;
        }
        _brush = brush;
        _isDashed = dashed;
        _radius = radius;
        _pen = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_brush is null)
        {
            return;
        }
        _pen ??= new ImmutablePen(_brush.ToImmutable(), 1, _isDashed ? s_dash : null);
        context.DrawRectangle(null, _pen, new RoundedRect(new Rect(Bounds.Size).Deflate(0.5), Math.Max(0, _radius - 0.5)));
    }
}

/// <summary>
/// A floating label beside a layer: a 7px dot whose left edge sits on the anchor, a 24px leader that draws out with the
/// label, the layer name and a note (hidden when compact). Placed in flat stage coordinates (never tilted): translation and
/// opacity per frame; colors transition in the theme when the layer is focused.
/// </summary>
[TemplatePart(LeaderPart, typeof(Control))]
[PseudoClasses(ActivePseudoClass)]
internal sealed class ExplodedViewLabel : TemplatedControl
{
    public const string LeaderPart = "PART_Leader";
    public const string ActivePseudoClass = ":active";

    public static readonly StyledProperty<string?> LabelNameProperty =
        AvaloniaProperty.Register<ExplodedViewLabel, string?>(nameof(LabelName));

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<ExplodedViewLabel, string?>(nameof(Note));

    public static readonly StyledProperty<bool> IsNoteVisibleProperty =
        AvaloniaProperty.Register<ExplodedViewLabel, bool>(nameof(IsNoteVisible), true);

    private readonly TranslateTransform _translate = new();
    private readonly ScaleTransform _leaderScale = new(0, 1);
    private bool _isActive;
    private double _draw = -1;

    public ExplodedViewLabel()
    {
        RenderTransform = _translate;
        IsHitTestVisible = false;
        Focusable = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    protected override Type StyleKeyOverride => typeof(ExplodedViewLabel);

    public string? LabelName
    {
        get => GetValue(LabelNameProperty);
        set => SetValue(LabelNameProperty, value);
    }

    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public bool IsNoteVisible
    {
        get => GetValue(IsNoteVisibleProperty);
        set => SetValue(IsNoteVisibleProperty, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                PseudoClasses.Set(ActivePseudoClass, value);
            }
        }
    }

    /// <summary>The anchor in stage coordinates (the dot's left edge, the text's center line).</summary>
    public Point Anchor => new(_translate.X, _translate.Y + DesiredSize.Height / 2);

    /// <summary>The draw-in: leader scale and label opacity.</summary>
    public double Draw => Math.Max(0, _draw);

    public double LeaderScale => _leaderScale.ScaleX;

    /// <summary>Places the label: its dot's left edge at (x, y) with the text centered on y; opacity and leader follow the draw-in.</summary>
    public void Place(double x, double y, double draw)
    {
        var top = y - DesiredSize.Height / 2;
        if (!_translate.X.Equals(x))
        {
            _translate.X = x;
        }
        if (!_translate.Y.Equals(top))
        {
            _translate.Y = top;
        }
        if (!_draw.Equals(draw))
        {
            _draw = draw;
            // Opacity 0 stands in for visibility: hidden, but still measured, so the fit never jumps as labels appear.
            Opacity = draw;
            _leaderScale.ScaleX = draw;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Control>(LeaderPart) is { } leader)
        {
            leader.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
            leader.RenderTransform = _leaderScale;
        }
    }
}

/// <summary>The row of stop buttons: fixed 36px buttons, or the row shared evenly when compact.</summary>
internal sealed class ExplodedViewStops : Panel
{
    private double _itemWidth = ExplodedViewLayout.StopWidth;

    public ExplodedViewStops()
    {
        Background = Brushes.Transparent;
    }

    /// <summary>Width of one button.</summary>
    public double ItemWidth
    {
        get => _itemWidth;
        set
        {
            if (!_itemWidth.Equals(value))
            {
                _itemWidth = value;
                InvalidateMeasure();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(_itemWidth, ExplodedViewLayout.StopHeight));
        }
        return new Size(_itemWidth * Children.Count, ExplodedViewLayout.StopHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(i * _itemWidth, 0, _itemWidth, ExplodedViewLayout.StopHeight));
        }
        return finalSize;
    }
}

/// <summary>A stop button: a transparent 44px target with a tick (20 px, 8 px at the ends) that thickens and takes the accent when current.</summary>
[PseudoClasses(CurrentPseudoClass, EndPseudoClass)]
internal sealed class ExplodedViewStopButton : AvaloniaButton
{
    public const string CurrentPseudoClass = ":current";
    public const string EndPseudoClass = ":end";

    private bool _isCurrent;
    private bool _isEnd;

    protected override Type StyleKeyOverride => typeof(ExplodedViewStopButton);

    public int Stop { get; init; }

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent != value)
            {
                _isCurrent = value;
                PseudoClasses.Set(CurrentPseudoClass, value);
            }
        }
    }

    public bool IsEnd
    {
        get => _isEnd;
        set
        {
            if (_isEnd != value)
            {
                _isEnd = value;
                PseudoClasses.Set(EndPseudoClass, value);
            }
        }
    }
}

/// <summary>A caption: a display-face title and one or two sentences.</summary>
internal sealed class ExplodedViewCaptionView : TemplatedControl
{
    public static readonly StyledProperty<string?> CaptionTitleProperty =
        AvaloniaProperty.Register<ExplodedViewCaptionView, string?>(nameof(CaptionTitle));

    public static readonly StyledProperty<string?> CaptionBodyProperty =
        AvaloniaProperty.Register<ExplodedViewCaptionView, string?>(nameof(CaptionBody));

    public static readonly StyledProperty<bool> HasBodyProperty =
        AvaloniaProperty.Register<ExplodedViewCaptionView, bool>(nameof(HasBody));

    protected override Type StyleKeyOverride => typeof(ExplodedViewCaptionView);

    public string? CaptionTitle
    {
        get => GetValue(CaptionTitleProperty);
        set => SetValue(CaptionTitleProperty, value);
    }

    public string? CaptionBody
    {
        get => GetValue(CaptionBodyProperty);
        set => SetValue(CaptionBodyProperty, value);
    }

    public bool HasBody
    {
        get => GetValue(HasBodyProperty);
        set => SetValue(HasBodyProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CaptionBodyProperty)
        {
            HasBody = !string.IsNullOrEmpty(CaptionBody);
        }
    }
}

/// <summary>
/// The caption area: every caption is laid out in one cell, so the area is as tall as the tallest and never jumps; only the
/// current one shows. A change crossfades: the old caption leaves in 180 ms on the exit curve rising 8 px, the new one
/// arrives in 480 ms on the enter curve from 10 px below, both at once. Code-driven (<see cref="MotionValue{T}"/>): enter
/// and exit run on different curves and offsets and the leaving caption must restart from below next time, which one
/// ControlTheme property transition cannot express. Reduced and off motion replace the caption at once.
/// </summary>
internal sealed class ExplodedViewCaptions : Panel
{
    public const double EnterOffset = 10;
    public const double ExitOffset = -8;

    private readonly List<Entry> _entries = [];
    private int _current = -1;

    private sealed class Entry
    {
        public required ExplodedViewCaptionView View { get; init; }
        public required TranslateTransform Translate { get; init; }
        public MotionValue<double>? Motion { get; set; }
        public bool Entering { get; set; }
    }

    public int Current => _current;

    public IEnumerable<ExplodedViewCaptionView> Views => _entries.Select(e => e.View);

    /// <summary>Sets the captions (title, body) in stop order; the current one stays shown.</summary>
    public void SetCaptions(IReadOnlyList<(string? Title, string? Body)> captions)
    {
        while (_entries.Count > captions.Count)
        {
            var last = _entries[^1];
            last.Motion?.Stop();
            Children.Remove(last.View);
            _entries.RemoveAt(_entries.Count - 1);
        }
        while (_entries.Count < captions.Count)
        {
            var translate = new TranslateTransform();
            var view = new ExplodedViewCaptionView { RenderTransform = translate, Opacity = 0 };
            AutomationProperties.SetAccessibilityView(view, AccessibilityView.Raw);
            _entries.Add(new Entry { View = view, Translate = translate });
            Children.Add(view);
        }
        for (var i = 0; i < captions.Count; i++)
        {
            _entries[i].View.CaptionTitle = captions[i].Title;
            _entries[i].View.CaptionBody = captions[i].Body;
        }
        if (_current >= _entries.Count)
        {
            _current = -1;
        }
    }

    /// <summary>Shows caption <paramref name="index"/>; animated crossfade when <paramref name="animate"/>.</summary>
    public void Show(int index, bool animate)
    {
        if (index == _current)
        {
            return;
        }
        var previous = _current;
        _current = index;
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            AutomationProperties.SetAccessibilityView(entry.View, i == index ? AccessibilityView.Content : AccessibilityView.Raw);
            if (!animate)
            {
                entry.Motion?.Stop();
                Apply(entry, i == index ? 1 : 0, i == index);
                continue;
            }
            if (i == index)
            {
                StartEnter(entry);
            }
            else if (i == previous)
            {
                StartExit(entry);
            }
        }
    }

    private static void Apply(Entry entry, double t, bool entering)
    {
        entry.View.Opacity = t;
        entry.Translate.Y = (1 - t) * (entering ? EnterOffset : ExitOffset);
    }

    private static void StartEnter(Entry entry)
    {
        var motion = MotionOf(entry);
        entry.Entering = true;
        Apply(entry, motion.Value, true);
        _ = motion.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Enter));
    }

    private static void StartExit(Entry entry)
    {
        var motion = MotionOf(entry);
        entry.Entering = false;
        Apply(entry, motion.Value, false);
        _ = motion.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Exit));
    }

    private static MotionValue<double> MotionOf(Entry entry)
    {
        if (entry.Motion is null)
        {
            var motion = new MotionValue<double>(entry.View, entry.View.Opacity, MotionVectors.Double);
            motion.Changed += v => Apply(entry, v, entry.Entering);
            entry.Motion = motion;
        }
        return entry.Motion;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}
