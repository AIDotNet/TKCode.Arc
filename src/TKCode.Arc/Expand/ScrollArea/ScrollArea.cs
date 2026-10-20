using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Axes a <see cref="ScrollArea"/> scrolls.</summary>
public enum ScrollAreaOrientation
{
    Vertical,
    Horizontal,
    Both
}

/// <summary>When the overlay scrollbars of a <see cref="ScrollArea"/> show.</summary>
public enum ScrollAreaScrollbarMode
{
    /// <summary>While scrolling, dragging or hovering, fading away after <see cref="ScrollArea.HideDelay"/> (Arc default).</summary>
    Auto,

    /// <summary>Always, whenever the content overflows.</summary>
    Always
}

/// <summary>Which edges of a <see cref="ScrollArea"/> still have content beyond them.</summary>
public readonly record struct ScrollAreaEdges(bool Top, bool Bottom, bool Left, bool Right);

/// <summary>Raised when content starts or stops extending past an edge.</summary>
public sealed class ScrollAreaEdgesChangedEventArgs : RoutedEventArgs
{
    public ScrollAreaEdgesChangedEventArgs(RoutedEvent routedEvent, ScrollAreaEdges edges) : base(routedEvent) => Edges = edges;

    /// <summary>The new edge state.</summary>
    public ScrollAreaEdges Edges { get; }
}

/// <summary>
/// Arc scroll area: native scrolling (wheel, keyboard, touch inertia, snap points) with thin overlay scrollbars
/// that appear while scrolling or hovering and fade away at rest, and edge fades that appear only where content
/// continues, each exactly as long as the content hidden past that edge (up to <see cref="FadeLength"/>). The
/// thumb thickens under the pointer, can be dragged, and pressing the track pages toward the pointer.
/// </summary>
[TemplatePart(FadeHostPart, typeof(Border))]
[TemplatePart(VerticalTrackPart, typeof(Control))]
[TemplatePart(HorizontalTrackPart, typeof(Control))]
[PseudoClasses(OverflowPseudoClass, ScrollingPseudoClass)]
public class ScrollArea : ScrollViewer
{
    public const string FadeHostPart = "PART_FadeHost";
    public const string VerticalTrackPart = "PART_VerticalTrack";
    public const string HorizontalTrackPart = "PART_HorizontalTrack";
    public const string OverflowPseudoClass = ":overflow";
    public const string ScrollingPseudoClass = ":scrolling";

    /// <summary>Distance an arrow key scrolls (browser default ≈ 40px).</summary>
    private const double LineStep = 40;

    #region Properties

    /// <summary>Axes that scroll. Defaults to <see cref="ScrollAreaOrientation.Vertical"/>.</summary>
    public static readonly StyledProperty<ScrollAreaOrientation> OrientationProperty =
        AvaloniaProperty.Register<ScrollArea, ScrollAreaOrientation>(nameof(Orientation));

    /// <summary>Length of each edge fade in px (Arc: 28). 0 turns the fades off.</summary>
    public static readonly StyledProperty<double> FadeLengthProperty =
        AvaloniaProperty.Register<ScrollArea, double>(nameof(FadeLength), 28);

    /// <summary>When the overlay scrollbars show.</summary>
    public static readonly StyledProperty<ScrollAreaScrollbarMode> OverlayScrollbarModeProperty =
        AvaloniaProperty.Register<ScrollArea, ScrollAreaScrollbarMode>(nameof(OverlayScrollbarMode));

    /// <summary>How long scrollbars linger after scrolling stops (Arc: 900ms).</summary>
    public static readonly StyledProperty<TimeSpan> HideDelayProperty =
        AvaloniaProperty.Register<ScrollArea, TimeSpan>(nameof(HideDelay), TimeSpan.FromMilliseconds(900));

    /// <summary>Turns vertical wheel movement into horizontal scrolling for horizontal areas, only while there is room.</summary>
    public static readonly StyledProperty<bool> IsWheelToHorizontalEnabledProperty =
        AvaloniaProperty.Register<ScrollArea, bool>(nameof(IsWheelToHorizontalEnabled), true);

    /// <summary>Accessible name; a named area is announced as a region.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ScrollArea, string?>(nameof(Label));

    /// <summary>Which edges still have content beyond them.</summary>
    public static readonly DirectProperty<ScrollArea, ScrollAreaEdges> EdgesProperty =
        AvaloniaProperty.RegisterDirect<ScrollArea, ScrollAreaEdges>(nameof(Edges), o => o.Edges);

    public static readonly RoutedEvent<ScrollAreaEdgesChangedEventArgs> EdgesChangedEvent =
        RoutedEvent.Register<ScrollArea, ScrollAreaEdgesChangedEventArgs>(nameof(EdgesChanged), RoutingStrategies.Bubble);

    /// <summary>Thumb color at rest (theme: Foreground; drawn at 30%, 46% when hot).</summary>
    internal static readonly StyledProperty<IBrush?> ThumbBrushProperty =
        AvaloniaProperty.Register<ScrollArea, IBrush?>(nameof(ThumbBrush));

    /// <inheritdoc cref="OrientationProperty"/>
    public ScrollAreaOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <inheritdoc cref="FadeLengthProperty"/>
    public double FadeLength { get => GetValue(FadeLengthProperty); set => SetValue(FadeLengthProperty, value); }

    /// <inheritdoc cref="OverlayScrollbarModeProperty"/>
    public ScrollAreaScrollbarMode OverlayScrollbarMode { get => GetValue(OverlayScrollbarModeProperty); set => SetValue(OverlayScrollbarModeProperty, value); }

    /// <inheritdoc cref="HideDelayProperty"/>
    public TimeSpan HideDelay { get => GetValue(HideDelayProperty); set => SetValue(HideDelayProperty, value); }

    /// <inheritdoc cref="IsWheelToHorizontalEnabledProperty"/>
    public bool IsWheelToHorizontalEnabled { get => GetValue(IsWheelToHorizontalEnabledProperty); set => SetValue(IsWheelToHorizontalEnabledProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private ScrollAreaEdges _edges;

    /// <inheritdoc cref="EdgesProperty"/>
    public ScrollAreaEdges Edges
    {
        get => _edges;
        private set => SetAndRaise(EdgesProperty, ref _edges, value);
    }

    /// <summary>Raised when content starts or stops extending past an edge.</summary>
    public event EventHandler<ScrollAreaEdgesChangedEventArgs>? EdgesChanged
    {
        add => AddHandler(EdgesChangedEvent, value);
        remove => RemoveHandler(EdgesChangedEvent, value);
    }

    internal IBrush? ThumbBrush { get => GetValue(ThumbBrushProperty); set => SetValue(ThumbBrushProperty, value); }

    #endregion

    private Border? _fadeHost;
    private Border? _fadeHostY;
    private ScrollContentPresenter? _presenter;
    private ScrollAreaScrollbar? _vertical;
    private ScrollAreaScrollbar? _horizontal;
    private LinearGradientBrush? _fadeY;
    private LinearGradientBrush? _fadeX;
    private DispatcherTimer? _hideTimer;
    private MotionValue<Vector>? _scroll;
    private bool _scrolling;
    private bool _hovering;
    private bool _edgesKnown;
    private Vector _lastOffset;

    static ScrollArea()
    {
        FocusableProperty.OverrideDefaultValue<ScrollArea>(true);
        HorizontalScrollBarVisibilityProperty.OverrideDefaultValue<ScrollArea>(ScrollBarVisibility.Disabled);
        VerticalScrollBarVisibilityProperty.OverrideDefaultValue<ScrollArea>(ScrollBarVisibility.Hidden);
        AffectsArrange<ScrollArea>(OrientationProperty);
    }

    protected override Type StyleKeyOverride => typeof(ScrollArea);

    /// <summary>The overlay track of an axis (tests).</summary>
    internal ScrollAreaScrollbar? VerticalTrack => _vertical;

    internal ScrollAreaScrollbar? HorizontalTrack => _horizontal;

    /// <summary>True while the overlay scrollbars are shown.</summary>
    public bool AreScrollbarsShown => OverlayScrollbarMode == ScrollAreaScrollbarMode.Always || _scrolling || _hovering ||
                                      _vertical?.IsDragging == true || _horizontal?.IsDragging == true;

    /// <summary>Scrolls by <paramref name="delta"/>, smoothly under full motion.</summary>
    public void ScrollBy(Vector delta) => ScrollToOffset(TargetOffset + delta);

    /// <summary>Scrolls to <paramref name="offset"/> (clamped), smoothly under full motion.</summary>
    public void ScrollToOffset(Vector offset)
    {
        var max = MaxOffset;
        var target = new Vector(Math.Clamp(offset.X, 0, max.X), Math.Clamp(offset.Y, 0, max.Y));
        if (_scroll is null)
        {
            _scroll = new MotionValue<Vector>(this, Offset, MotionVectors.Vector);
            _scroll.Changed += v => Offset = v;
        }
        if (!_scroll.IsAnimating)
        {
            _scroll.Jump(Offset);
        }
        // Spring on full motion; Reduced and Off jump (MotionValue honors the level).
        _ = _scroll.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private Vector TargetOffset => _scroll is { IsAnimating: true } running ? running.Target : Offset;

    internal Vector MaxOffset => new(Math.Max(0, Extent.Width - Viewport.Width), Math.Max(0, Extent.Height - Viewport.Height));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _fadeHost = e.NameScope.Find<Border>(FadeHostPart);
        _fadeHostY = e.NameScope.Find<Border>("PART_FadeHostY");
        _presenter = e.NameScope.Find<ScrollContentPresenter>("PART_ContentPresenter");
        _vertical = e.NameScope.Find<ScrollAreaScrollbar>(VerticalTrackPart);
        _horizontal = e.NameScope.Find<ScrollAreaScrollbar>(HorizontalTrackPart);
        if (_vertical is not null)
        {
            _vertical.Owner = this;
            _vertical.Orientation = Avalonia.Layout.Orientation.Vertical;
        }
        if (_horizontal is not null)
        {
            _horizontal.Owner = this;
            _horizontal.Orientation = Avalonia.Layout.Orientation.Horizontal;
        }
        ApplyOrientation();
        Sync();
        UpdateTracks();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty)
        {
            ApplyOrientation();
            Sync();
        }
        else if (change.Property == OffsetProperty)
        {
            var offset = Offset;
            if (_lastOffset != offset)
            {
                _lastOffset = offset;
                Wake();
            }
            Sync();
        }
        else if (change.Property == ExtentProperty || change.Property == ViewportProperty || change.Property == FadeLengthProperty)
        {
            Sync();
        }
        else if (change.Property == OverlayScrollbarModeProperty)
        {
            UpdateTracks();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        // Fades are relative to the viewport size, which is only known after arrange.
        Sync();
        return size;
    }

    private void ApplyOrientation()
    {
        var orientation = Orientation;
        var vertical = orientation != ScrollAreaOrientation.Horizontal;
        var horizontal = orientation != ScrollAreaOrientation.Vertical;
        SetCurrentValue(VerticalScrollBarVisibilityProperty, vertical ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled);
        SetCurrentValue(HorizontalScrollBarVisibilityProperty, horizontal ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled);
        // Both axes: each track stops 13px short of the corner so they never cross.
        if (_vertical is not null)
        {
            _vertical.Margin = new Thickness(0, 3, 1, orientation == ScrollAreaOrientation.Both ? 13 : 3);
        }
        if (_horizontal is not null)
        {
            _horizontal.Margin = new Thickness(3, 0, orientation == ScrollAreaOrientation.Both ? 13 : 3, 1);
        }
    }

    /// <summary>Writes fades, thumbs and edges for the current offset (no layout).</summary>
    private void Sync()
    {
        var orientation = Orientation;
        var vertical = orientation != ScrollAreaOrientation.Horizontal;
        var horizontal = orientation != ScrollAreaOrientation.Vertical;
        var offset = Offset;
        var max = MaxOffset;
        var left = Math.Abs(offset.X);
        var edges = new ScrollAreaEdges(
            vertical && offset.Y > 0.5,
            vertical && max.Y - offset.Y > 0.5,
            horizontal && left > 0.5,
            horizontal && max.X - left > 0.5);
        var overflow = max.X > 0.5 || max.Y > 0.5;
        PseudoClasses.Set(OverflowPseudoClass, overflow);

        UpdateFades(vertical, horizontal, offset, max);
        _vertical?.Sync(vertical && max.Y > 0.5, offset.Y, max.Y, Viewport.Height, Extent.Height);
        _horizontal?.Sync(horizontal && max.X > 0.5, left, max.X, Viewport.Width, Extent.Width);

        if (!_edgesKnown || edges != _edges)
        {
            var changed = _edgesKnown;
            _edgesKnown = true;
            Edges = edges;
            if (changed || edges != default)
            {
                RaiseEvent(new ScrollAreaEdgesChangedEventArgs(EdgesChangedEvent, edges));
            }
        }
    }

    private void UpdateFades(bool vertical, bool horizontal, Vector offset, Vector max)
    {
        var fade = FadeLength;
        if (_presenter is null)
        {
            return;
        }
        var height = _presenter.Bounds.Height;
        var width = _presenter.Bounds.Width;
        var top = vertical && fade > 0 ? Math.Min(fade, offset.Y) : 0;
        var bottom = vertical && fade > 0 ? Math.Min(fade, max.Y - offset.Y) : 0;
        var start = horizontal && fade > 0 ? Math.Min(fade, Math.Abs(offset.X)) : 0;
        var end = horizontal && fade > 0 ? Math.Min(fade, max.X - Math.Abs(offset.X)) : 0;
        // The masks sit on wrappers: the presenter's own composition clip ignores an opacity mask.
        ApplyFade((Visual?)_fadeHostY ?? _presenter, ref _fadeY, top, bottom, height, verticalAxis: true);
        if (_fadeHost is not null)
        {
            ApplyFade(_fadeHost, ref _fadeX, start, end, width, verticalAxis: false);
        }
    }

    /// <summary>Each fade is exactly as long as the content hidden past that edge, so it grows in with the first pixels of scroll.</summary>
    /// <remarks>
    /// A new immutable brush is published only when a fade length changes, which happens within the first
    /// <see cref="FadeLength"/> pixels from an edge; elsewhere scrolling allocates nothing.
    /// </remarks>
    private static void ApplyFade(Visual target, ref LinearGradientBrush? brush, double head, double tail, double length, bool verticalAxis)
    {
        if ((head <= 0.01 && tail <= 0.01) || length <= 0)
        {
            if (target.OpacityMask is not null)
            {
                target.OpacityMask = null;
            }
            return;
        }
        if (brush is null)
        {
            brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = verticalAxis ? new RelativePoint(0, 1, RelativeUnit.Relative) : new RelativePoint(1, 0, RelativeUnit.Relative)
            };
            brush.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            brush.GradientStops.Add(new GradientStop(Colors.Black, 0));
            brush.GradientStops.Add(new GradientStop(Colors.Black, 1));
            brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        }
        var stops = brush.GradientStops;
        var start = Math.Round(Math.Clamp(head / length, 0, 0.5), 4);
        var end = Math.Round(Math.Clamp(1 - tail / length, 0.5, 1), 4);
        if (target.OpacityMask is not null && stops[1].Offset == start && stops[2].Offset == end)
        {
            return;
        }
        stops[1].Offset = start;
        stops[2].Offset = end;
        target.OpacityMask = new Avalonia.Media.Immutable.ImmutableLinearGradientBrush(brush);
    }

    #region Visibility of the bars

    private void Wake()
    {
        if (!_scrolling)
        {
            _scrolling = true;
            PseudoClasses.Set(ScrollingPseudoClass, true);
            UpdateTracks();
        }
        _hideTimer ??= new DispatcherTimer(DispatcherPriority.Normal);
        _hideTimer.Stop();
        _hideTimer.Interval = HideDelay;
        _hideTimer.Tick -= OnHideTick;
        _hideTimer.Tick += OnHideTick;
        _hideTimer.Start();
    }

    private void OnHideTick(object? sender, EventArgs e)
    {
        _hideTimer?.Stop();
        _scrolling = false;
        PseudoClasses.Set(ScrollingPseudoClass, false);
        UpdateTracks();
    }

    private (SnapPointsType X, SnapPointsType Y)? _snapBeforeDrag;

    /// <summary>Called by a track when a drag starts or ends; snapping is suspended while the thumb is dragged (Arc).</summary>
    internal void OnTrackDragChanged(bool dragging)
    {
        if (dragging)
        {
            _snapBeforeDrag ??= (HorizontalSnapPointsType, VerticalSnapPointsType);
            SetCurrentValue(HorizontalSnapPointsTypeProperty, SnapPointsType.None);
            SetCurrentValue(VerticalSnapPointsTypeProperty, SnapPointsType.None);
        }
        else
        {
            if (_snapBeforeDrag is { } snap)
            {
                _snapBeforeDrag = null;
                SetCurrentValue(HorizontalSnapPointsTypeProperty, snap.X);
                SetCurrentValue(VerticalSnapPointsTypeProperty, snap.Y);
            }
            Wake();
        }
        UpdateTracks();
    }

    private void UpdateTracks()
    {
        var shown = AreScrollbarsShown;
        _vertical?.SetShown(shown);
        _horizontal?.SetShown(shown);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Hover reveals the bars only for fine pointers (Arc: @media (hover: hover) and (pointer: fine)).
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _hovering = true;
            UpdateTracks();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovering = false;
        UpdateTracks();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _hideTimer?.Stop();
        _scrolling = false;
        _hovering = false;
        _scroll?.Stop();
    }

    #endregion

    #region Input

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        _scroll?.Stop();
        // A wheel over a horizontal strip moves it sideways, but only while there is room, so the page still scrolls at the ends.
        if (!e.Handled && Orientation == ScrollAreaOrientation.Horizontal && IsWheelToHorizontalEnabled &&
            (e.KeyModifiers & KeyModifiers.Control) == 0 && Math.Abs(e.Delta.X) < Math.Abs(e.Delta.Y))
        {
            var max = MaxOffset.X;
            var at = Offset.X;
            var down = e.Delta.Y < 0;
            if (max > 0 && !(down && at >= max - 0.5) && !(!down && at <= 0))
            {
                Offset = new Vector(Math.Clamp(at - e.Delta.Y * 50, 0, max), Offset.Y);
                e.Handled = true;
                return;
            }
        }
        base.OnPointerWheelChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source != this)
        {
            base.OnKeyDown(e);
            return;
        }
        var vertical = Orientation != ScrollAreaOrientation.Horizontal;
        var horizontal = Orientation != ScrollAreaOrientation.Vertical;
        var page = vertical ? Viewport.Height * 0.9 : Viewport.Width * 0.9;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        Vector? delta = e.Key switch
        {
            Key.Down when vertical => new Vector(0, LineStep),
            Key.Up when vertical => new Vector(0, -LineStep),
            Key.Right when horizontal => new Vector(LineStep, 0),
            Key.Left when horizontal => new Vector(-LineStep, 0),
            Key.PageDown => vertical ? new Vector(0, page) : new Vector(page, 0),
            Key.PageUp => vertical ? new Vector(0, -page) : new Vector(-page, 0),
            Key.Space => vertical ? new Vector(0, shift ? -page : page) : new Vector(shift ? -page : page, 0),
            _ => null
        };
        if (delta is { } d)
        {
            ScrollBy(d);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Home)
        {
            ScrollToOffset(vertical ? new Vector(Offset.X, 0) : new Vector(0, Offset.Y));
            e.Handled = true;
            return;
        }
        if (e.Key == Key.End)
        {
            var max = MaxOffset;
            ScrollToOffset(vertical ? new Vector(Offset.X, max.Y) : new Vector(max.X, Offset.Y));
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>Called by a track: pages 90% of the viewport toward the pointer.</summary>
    internal void PageToward(Avalonia.Layout.Orientation axis, int direction)
    {
        var amount = (axis == Avalonia.Layout.Orientation.Vertical ? Viewport.Height : Viewport.Width) * 0.9 * direction;
        ScrollBy(axis == Avalonia.Layout.Orientation.Vertical ? new Vector(0, amount) : new Vector(amount, 0));
    }

    /// <summary>Called by a track while its thumb is dragged.</summary>
    internal void DragTo(Avalonia.Layout.Orientation axis, double value)
    {
        _scroll?.Stop();
        Offset = axis == Avalonia.Layout.Orientation.Vertical ? new Vector(Offset.X, value) : new Vector(value, Offset.Y);
    }

    #endregion
}
