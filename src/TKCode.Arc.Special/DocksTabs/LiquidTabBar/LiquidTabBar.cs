using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Glyph tabs for a few sections of equal rank, each with a short label. A lens in the foreground color marks the
/// selected tab: it travels to the next tab as one pill, taking that tab's width on the way, and the tab under it is
/// painted again in reverse colors, which fills outline glyphs. Only one label shows at a time, the old one gone before
/// the new one fades in. The lens can be dragged or flicked to another tab, and it tips a little toward whichever tab
/// the mouse rests on.
/// </summary>
/// <remarks>
/// Tabs are <see cref="LiquidTabBarItem"/> elements in XAML, or data through <see cref="ItemsControl.ItemsSource"/>.
/// Read and set the selection with <see cref="SelectingItemsControl.SelectedValue"/> (each tab's
/// <see cref="LiquidTabBarItem.Value"/>), <see cref="SelectingItemsControl.SelectedIndex"/> or
/// <see cref="SelectingItemsControl.SelectedItem"/>; one tab is always selected while there are tabs. The arrow keys
/// move the selection and the focus together. Keep the default items panel: the lens follows the bar's own layout.
/// </remarks>
[TemplatePart(LensPart, typeof(Control))]
[PseudoClasses(DraggingPseudoClass)]
public class LiquidTabBar : SelectingItemsControl
{
    internal const string LensPart = "PART_Lens";
    internal const string DraggingPseudoClass = ":dragging";

    /// <summary>Where labels appear: on the selected tab only (default), on every tab, or nowhere.</summary>
    public static readonly StyledProperty<LiquidTabBarLabels> LabelsProperty =
        AvaloniaProperty.Register<LiquidTabBar, LiquidTabBarLabels>(nameof(Labels));

    /// <summary>Fill of the lens (and of the badges outside it). Defaults to the foreground color.</summary>
    public static readonly StyledProperty<IBrush?> LensBackgroundProperty =
        AvaloniaProperty.Register<LiquidTabBar, IBrush?>(nameof(LensBackground));

    /// <summary>Color of the glyphs, labels and badges redrawn inside the lens. Defaults to the background color.</summary>
    public static readonly StyledProperty<IBrush?> LensForegroundProperty =
        AvaloniaProperty.Register<LiquidTabBar, IBrush?>(nameof(LensForeground));

    /// <summary>Font size of the badge digits (from the token).</summary>
    internal static readonly StyledProperty<double> BadgeFontSizeProperty =
        AvaloniaProperty.Register<LiquidTabBar, double>(nameof(BadgeFontSize), 11);

    private static readonly FuncTemplate<Panel?> s_panel = new(() => new LiquidTabBarPanel());

    private readonly DragTracker _drag;
    private Control? _lens;

    // The tabs as laid out this frame (panel coordinates) and the track around them.
    private double[] _lefts = [];
    private double[] _rights = [];
    private double[] _centers = [];
    private int _count;
    private double _trackX;
    private double _trackWidth;

    // The lens: its left and right edges as fractional tab indexes, the lean toward a hovered tab and the press inset.
    private MotionValue<double>? _edgeA;
    private MotionValue<double>? _edgeB;
    private MotionValue<double>? _lean;
    private MotionValue<double>? _squeeze;
    private double _a;
    private double _b;
    private double _leanValue;
    private double _squeezeValue;
    private bool _placed;

    private int _hoverIndex = -1;
    private bool _pressed;
    private bool _dragging;
    private int _dragOrigin;
    private double _dragFrom;
    private double _dragStartIndex;
    private double _dragTarget;
    private Point _dragPanelOrigin;

    private readonly LiquidTabBarDigits _baseDigits = new();
    private readonly LiquidTabBarDigits _lensDigits = new();
    private Pen? _trackPen;
    private IBrush? _trackPenBrush;
    private Pen? _lensGlyphPen;
    private IBrush? _lensGlyphPenBrush;

    static LiquidTabBar()
    {
        SelectionModeProperty.OverrideDefaultValue<LiquidTabBar>(SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<LiquidTabBar>(false);
        ItemsPanelProperty.OverrideDefaultValue<LiquidTabBar>(s_panel);
        AffectsRender<LiquidTabBar>(BackgroundProperty, BorderBrushProperty);
    }

    /// <summary>Creates a bar.</summary>
    public LiquidTabBar()
    {
        SelectedValueBinding = LiquidTabBarValueBinding.Create();
        _drag = new DragTracker(this, new DragOptions { Threshold = LiquidTabBarMetrics.DragThreshold, Axis = DragAxis.Horizontal })
        {
            CanBegin = CanGrab
        };
        _drag.Pressed += OnGrabPressed;
        _drag.Started += OnDragStarted;
        _drag.Moved += OnDragMoved;
        _drag.Completed += OnDragCompleted;
        _drag.Cancelled += OnDragCancelled;
        _drag.Released += OnGrabReleased;
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public LiquidTabBarLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="LensBackgroundProperty"/>
    public IBrush? LensBackground { get => GetValue(LensBackgroundProperty); set => SetValue(LensBackgroundProperty, value); }

    /// <inheritdoc cref="LensForegroundProperty"/>
    public IBrush? LensForeground { get => GetValue(LensForegroundProperty); set => SetValue(LensForegroundProperty, value); }

    internal double BadgeFontSize { get => GetValue(BadgeFontSizeProperty); set => SetValue(BadgeFontSizeProperty, value); }

    /// <summary>The lens rectangle now, in the bar's coordinates (empty when there is no selection).</summary>
    internal Rect LensRect => LensVisible ? ComputeLensRect() : default;

    /// <summary>The lens edges as fractional tab indexes (tests).</summary>
    internal (double Left, double Right) LensEdges => (_a + Math.Min(_leanValue, 0), _b + Math.Max(_leanValue, 0));

    /// <summary>The lean toward a hovered tab now (tests).</summary>
    internal double Lean => _leanValue;

    /// <summary>How far the lens is drawn in by a press, 0 to 1 (tests).</summary>
    internal double Squeeze => _squeezeValue;

    /// <summary>Whether the lens is being dragged.</summary>
    internal bool IsDragging => _dragging;

    /// <summary>The track rectangle now (tests).</summary>
    internal Rect TrackRect => new(_trackX, 0, _trackWidth, LiquidTabBarMetrics.TrackHeight);

    /// <summary>The badge digits drawn on the track (lens foreground on lens background).</summary>
    internal LiquidTabBarDigits BaseDigits
    {
        get
        {
            _baseDigits.Update(LensForeground, DigitTypeface, BadgeFontSize);
            return _baseDigits;
        }
    }

    /// <summary>The badge digits drawn inside the lens (the colors swapped).</summary>
    internal LiquidTabBarDigits LensDigits
    {
        get
        {
            _lensDigits.Update(LensBackground, DigitTypeface, BadgeFontSize);
            return _lensDigits;
        }
    }

    private Typeface DigitTypeface => new(FontFamily, FontStyle.Normal, FontWeight);

    private bool LensVisible => _count > 0 && SelectedIndex >= 0;

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new LiquidTabBarItem();

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<LiquidTabBarItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (container is LiquidTabBarItem tab)
        {
            var shown = LabelShown(index);
            tab.SetOpenTarget(shown ? 1 : 0, shown, animate: false);
        }
        UpdateTabStops();
        ItemsPanelRoot?.InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        UpdateTabStops();
        ItemsPanelRoot?.InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _lens = e.NameScope.Find<Control>(LensPart);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            UpdateTabStops();
            UpdateOpenTargets(animate: IsLoaded);
            if (!_dragging)
            {
                MoveLensTo(SelectedIndex, animate: _placed && IsLoaded);
            }
            UpdateLean();
            ItemsPanelRoot?.InvalidateMeasure();
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateOpenTargets(animate: IsLoaded);
            ItemsPanelRoot?.InvalidateMeasure();
            InvalidateTabs();
        }
        else if (change.Property == LensBackgroundProperty || change.Property == LensForegroundProperty
                 || change.Property == FontFamilyProperty || change.Property == FontWeightProperty
                 || change.Property == BadgeFontSizeProperty)
        {
            InvalidateTabs();
            ItemsPanelRoot?.InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateOpenTargets(animate: false);
        MoveLensTo(SelectedIndex, animate: false);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A detached bar lands where it was going; nothing keeps animating out of view.
        EndDrag();
        _hoverIndex = -1;
        _pressed = false;
        MoveLensTo(SelectedIndex, animate: false);
        _lean?.Jump(0);
        _leanValue = 0;
        _squeeze?.Jump(0);
        _squeezeValue = 0;
        _baseDigits.Clear();
        _lensDigits.Clear();
    }

    // ---- Model ------------------------------------------------------------------------------------------------------

    /// <summary>Whether tab <paramref name="index"/> should show its label in the current mode and selection.</summary>
    private bool LabelShown(int index) => Labels switch
    {
        LiquidTabBarLabels.Always => true,
        LiquidTabBarLabels.None => false,
        _ => index == SelectedIndex
    };

    private void UpdateOpenTargets(bool animate)
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is LiquidTabBarItem tab)
            {
                var shown = LabelShown(i);
                tab.SetOpenTarget(shown ? 1 : 0, shown, animate);
            }
        }
    }

    /// <summary>The bar's size once every tab has settled: as wide as the tabs will be, so a frame never relayouts the page.</summary>
    internal Size MeasureTabs(Controls children)
    {
        var count = children.Count;
        if (count == 0)
        {
            return new Size(0, LiquidTabBarMetrics.TrackHeight);
        }
        var width = 2 * LiquidTabBarMetrics.TrackPadding + (count - 1) * LiquidTabBarMetrics.ItemSpacing;
        for (var i = 0; i < count; i++)
        {
            width += children[i] is LiquidTabBarItem tab
                ? tab.WidthAt(LabelShown(i) || tab.IsTextTab ? 1 : 0)
                : children[i].DesiredSize.Width;
        }
        return new Size(width, LiquidTabBarMetrics.TrackHeight);
    }

    /// <summary>
    /// Places the tabs at their current widths, anchored in the bar's box by its alignment, and records where they are for
    /// the lens and the track.
    /// </summary>
    internal void ArrangeTabs(Controls children, Size finalSize)
    {
        var count = children.Count;
        if (_lefts.Length < count)
        {
            _lefts = new double[count];
            _rights = new double[count];
            _centers = new double[count];
        }
        _count = count;
        var content = count == 0 ? 0 : 2 * LiquidTabBarMetrics.TrackPadding + (count - 1) * LiquidTabBarMetrics.ItemSpacing;
        for (var i = 0; i < count; i++)
        {
            content += WidthOf(children[i]);
        }
        var anchor = HorizontalAlignment switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Right => 1,
            _ => 0.5
        };
        _trackX = (finalSize.Width - content) * anchor;
        _trackWidth = content;
        var x = _trackX + LiquidTabBarMetrics.TrackPadding;
        var y = LiquidTabBarMetrics.TrackPadding;
        for (var i = 0; i < count; i++)
        {
            var width = WidthOf(children[i]);
            children[i].Arrange(new Rect(x, y, width, LiquidTabBarMetrics.ItemHeight));
            _lefts[i] = x;
            _rights[i] = x + width;
            _centers[i] = x + width / 2;
            x += width + LiquidTabBarMetrics.ItemSpacing;
        }
        if (!_placed && SelectedIndex >= 0 && count > 0)
        {
            _placed = true;
        }
        InvalidateVisual();
        _lens?.InvalidateVisual();
    }

    private static double WidthOf(Control child) => child is LiquidTabBarItem tab ? tab.CurrentWidth : child.DesiredSize.Width;

    private Rect ComputeLensRect()
    {
        ReadOnlySpan<double> lefts = _lefts.AsSpan(0, _count);
        ReadOnlySpan<double> rights = _rights.AsSpan(0, _count);
        var (a, b) = LensEdges;
        var left = LiquidTabBarMetrics.EdgeAt(lefts, a);
        var right = LiquidTabBarMetrics.EdgeAt(rights, b);
        var inset = LiquidTabBarMetrics.PressInset * Math.Clamp(_squeezeValue, 0, 1);
        var top = LiquidTabBarMetrics.TrackPadding + inset;
        return new Rect(left + inset, top, Math.Max(0, right - left - 2 * inset),
            Math.Max(0, LiquidTabBarMetrics.ItemHeight - 2 * inset));
    }

    /// <summary>A tab's size or openness moved: lay the row out again (only this bar's tabs) and redraw.</summary>
    internal void OnItemGeometryChanged() => ItemsPanelRoot?.InvalidateArrange();

    /// <summary>Something drawn by a tab changed (a fading label, a rolling badge): redraw the lens.</summary>
    internal void OnItemLookChanged() => _lens?.InvalidateVisual();

    /// <summary>A tab became or stopped being a text tab: its targets and size change.</summary>
    internal void OnItemsChanged()
    {
        UpdateOpenTargets(animate: IsLoaded);
        ItemsPanelRoot?.InvalidateMeasure();
    }

    private void InvalidateTabs()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            ContainerFromIndex(i)?.InvalidateVisual();
        }
        _lens?.InvalidateVisual();
    }

    // ---- Lens motion --------------------------------------------------------------------------------------------------

    private MotionValue<double> CreateValue(double initial, Action<double> write)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += v =>
        {
            write(v);
            _lens?.InvalidateVisual();
        };
        return value;
    }

    private void MoveLensTo(int index, bool animate)
    {
        if (index < 0)
        {
            return;
        }
        if (!animate)
        {
            _edgeA?.Jump(index);
            _edgeB?.Jump(index);
            _a = index;
            _b = index;
            _lens?.InvalidateVisual();
            return;
        }
        _edgeA ??= CreateValue(_a, v => _a = v);
        _edgeB ??= CreateValue(_b, v => _b = v);
        _ = _edgeA.AnimateTo(index, LiquidTabBarMetrics.Slide);
        _ = _edgeB.AnimateTo(index, LiquidTabBarMetrics.Slide);
    }

    private void SetSqueeze(double target, MotionSpec spec)
    {
        if (target > 0 && !ArcMotion.IsFull(this))
        {
            target = 0;
        }
        if (_squeeze is null && Math.Abs(_squeezeValue - target) < 0.0001)
        {
            return;
        }
        _squeeze ??= CreateValue(_squeezeValue, v => _squeezeValue = v);
        _ = _squeeze.AnimateTo(target, spec);
    }

    /// <summary>Leans the lens toward the hovered tab's side (mouse only, full motion only) or back.</summary>
    private void UpdateLean()
    {
        var selected = SelectedIndex;
        var hover = _hoverIndex;
        var target = 0.0;
        if (!_dragging && !_pressed && hover >= 0 && selected >= 0 && hover != selected && IsSelectable(hover)
            && ArcMotion.IsFull(this))
        {
            target = hover > selected ? LiquidTabBarMetrics.Lean : -LiquidTabBarMetrics.Lean;
        }
        if (_lean is null && Math.Abs(target - _leanValue) < 0.0001)
        {
            return;
        }
        _lean ??= CreateValue(_leanValue, v => _leanValue = v);
        if (Math.Abs(_lean.Target - target) > 0.0001 || Math.Abs(_lean.Value - target) > 0.0001)
        {
            _ = _lean.AnimateTo(target, LiquidTabBarMetrics.LeanMotion);
        }
    }

    // ---- Drawing ------------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (_count == 0 || _trackWidth <= 0)
        {
            return;
        }
        var rect = new Rect(_trackX, 0, _trackWidth, LiquidTabBarMetrics.TrackHeight);
        var radius = rect.Height / 2;
        if (Background is { } background)
        {
            context.DrawRectangle(background, null, new RoundedRect(rect, radius));
        }
        if (BorderBrush is { } border)
        {
            if (_trackPen is null || !ReferenceEquals(_trackPenBrush, border))
            {
                _trackPen = new Pen(border, 1);
                _trackPenBrush = border;
            }
            var ring = rect.Deflate(0.5);
            context.DrawRectangle(null, _trackPen, new RoundedRect(ring, ring.Height / 2));
        }
    }

    /// <summary>Draws the lens: its fill, and every tab it covers redrawn inverted, all clipped to its rounded shape.</summary>
    internal void RenderLens(DrawingContext context)
    {
        if (!LensVisible || LensBackground is not { } fill)
        {
            return;
        }
        var rect = ComputeLensRect();
        if (rect.Width < 0.5 || rect.Height < 0.5)
        {
            return;
        }
        var shape = new RoundedRect(rect, Math.Min(rect.Height, rect.Width) / 2);
        var panel = ItemsPanelRoot;
        using (context.PushClip(shape))
        {
            context.DrawRectangle(fill, null, shape);
            if (panel is null)
            {
                return;
            }
            var pen = LensGlyphPen();
            var children = panel.Children;
            for (var i = 0; i < _count && i < children.Count; i++)
            {
                if (_rights[i] < rect.Left || _lefts[i] > rect.Right || children[i] is not LiquidTabBarItem tab)
                {
                    continue;
                }
                using (context.PushTransform(Matrix.CreateTranslation(_lefts[i], LiquidTabBarMetrics.TrackPadding)))
                {
                    tab.RenderInverted(context, this, pen);
                }
            }
        }
    }

    private Pen? LensGlyphPen()
    {
        var brush = LensForeground;
        if (brush is null)
        {
            return null;
        }
        if (_lensGlyphPen is null || !ReferenceEquals(_lensGlyphPenBrush, brush))
        {
            _lensGlyphPen = new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _lensGlyphPenBrush = brush;
        }
        return _lensGlyphPen;
    }

    // ---- Pointer ------------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        var hover = IndexAtPoint(e.GetPosition(this));
        if (hover != _hoverIndex)
        {
            _hoverIndex = hover;
            UpdateLean();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            UpdateLean();
        }
    }

    private int IndexAtPoint(Point point)
    {
        if (point.Y < 0 || point.Y > LiquidTabBarMetrics.TrackHeight)
        {
            return -1;
        }
        for (var i = 0; i < _count; i++)
        {
            if (point.X >= _lefts[i] && point.X <= _rights[i])
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>A click or tap on a tab selects it.</summary>
    internal void OnItemClicked(LiquidTabBarItem item)
    {
        if (!item.IsEffectivelyEnabled || _dragging)
        {
            return;
        }
        var index = IndexFromContainer(item);
        if (index >= 0)
        {
            SelectedIndex = index;
            item.Focus(NavigationMethod.Pointer);
        }
    }

    private bool CanGrab(PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || SelectedIndex < 0 || e.Source is not Visual source || FindItem(source) is not { } item)
        {
            return false;
        }
        return IndexFromContainer(item) == SelectedIndex && item.IsEffectivelyEnabled;
    }

    private void OnGrabPressed(object? sender, DragGestureEventArgs e)
    {
        _pressed = true;
        SetSqueeze(1, LiquidTabBarMetrics.Press);
        UpdateLean();
    }

    private void OnGrabReleased(object? sender, DragGestureEventArgs e)
    {
        _pressed = false;
        SetSqueeze(0, LiquidTabBarMetrics.Slide);
        UpdateLean();
    }

    private void OnDragStarted(object? sender, DragGestureEventArgs e)
    {
        _dragging = true;
        PseudoClasses.Set(DraggingPseudoClass, true);
        _dragOrigin = SelectedIndex;
        _dragPanelOrigin = TopLevel.GetTopLevel(this) is { } top && this.TranslatePoint(default, top) is { } origin ? origin : default;
        _dragStartIndex = (_a + _b) / 2;
        _dragFrom = LiquidTabBarMetrics.IndexAt(_centers.AsSpan(0, _count), e.Start.X - _dragPanelOrigin.X);
        _dragTarget = _dragStartIndex;
        UpdateLean();
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        if (!_dragging || _count == 0)
        {
            return;
        }
        var centers = _centers.AsSpan(0, _count);
        var raw = _dragStartIndex + LiquidTabBarMetrics.IndexAt(centers, e.Position.X - _dragPanelOrigin.X) - _dragFrom;
        var target = LiquidTabBarMetrics.Resist(raw, _count, centers);
        _dragTarget = target;
        _edgeA ??= CreateValue(_a, v => _a = v);
        _edgeB ??= CreateValue(_b, v => _b = v);
        // The edge on the side the pointer moves toward leads; the other follows a little behind.
        var forward = target >= (_a + _b) / 2;
        _ = _edgeA.AnimateTo(target, forward ? LiquidTabBarMetrics.DragTrail : LiquidTabBarMetrics.DragLead);
        _ = _edgeB.AnimateTo(target, forward ? LiquidTabBarMetrics.DragLead : LiquidTabBarMetrics.DragTrail);
    }

    private void OnDragCompleted(object? sender, DragGestureEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        var target = LandingIndex(_dragTarget, e.Velocity.X);
        EndDrag();
        _pressed = false;
        SetSqueeze(0, LiquidTabBarMetrics.Slide);
        if (target >= 0 && target != SelectedIndex)
        {
            SelectedIndex = target;
            ContainerFromIndex(target)?.Focus(NavigationMethod.Pointer);
        }
        else
        {
            MoveLensTo(SelectedIndex, animate: true);
        }
        UpdateLean();
    }

    private void OnDragCancelled(object? sender, DragGestureEventArgs e)
    {
        EndDrag();
        _pressed = false;
        SetSqueeze(0, LiquidTabBarMetrics.Slide);
        MoveLensTo(SelectedIndex, animate: true);
        UpdateLean();
    }

    private void EndDrag()
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        PseudoClasses.Set(DraggingPseudoClass, false);
    }

    /// <summary>
    /// Where a release lands: the release position coasts for <see cref="LiquidTabBarMetrics.FlickHorizon"/> at its
    /// velocity and picks the nearest selectable tab; a quick flick that would land back on the starting tab moves on
    /// to the next one in its direction.
    /// </summary>
    internal int LandingIndex(double position, double velocity)
    {
        if (_count == 0)
        {
            return -1;
        }
        var centers = _centers.AsSpan(0, _count);
        var pitch = LiquidTabBarMetrics.PitchAt(centers, position);
        var projected = position + velocity / pitch * LiquidTabBarMetrics.FlickHorizon.TotalSeconds;
        var target = NearestSelectable((int)Math.Round(Math.Clamp(projected, 0, _count - 1)));
        if (target == _dragOrigin && Math.Abs(velocity) >= LiquidTabBarMetrics.FlickVelocity)
        {
            var next = Step(_dragOrigin, Math.Sign(velocity), wrap: false);
            if (next >= 0)
            {
                target = next;
            }
        }
        return target;
    }

    private int NearestSelectable(int index)
    {
        for (var d = 0; d < _count; d++)
        {
            if (index - d >= 0 && IsSelectable(index - d))
            {
                return index - d;
            }
            if (index + d < _count && IsSelectable(index + d))
            {
                return index + d;
            }
        }
        return -1;
    }

    // ---- Keyboard -----------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source || FindItem(source) is not { } item)
        {
            base.OnKeyDown(e);
            return;
        }
        var current = IndexFromContainer(item);
        int? target = e.Key switch
        {
            Key.Left => Step(current, -1, wrap: true),
            Key.Right => Step(current, 1, wrap: true),
            Key.Home => FirstEnabled(0, 1),
            Key.End => FirstEnabled(ItemCount - 1, -1),
            _ => null
        };
        if (target is { } index)
        {
            e.Handled = true;
            if (index >= 0 && index != current)
            {
                SelectedIndex = index;
                ContainerFromIndex(index)?.Focus(NavigationMethod.Directional);
            }
            return;
        }
        if (e.Key is Key.Enter or Key.Space && current >= 0)
        {
            e.Handled = true;
            SelectedIndex = current;
            return;
        }
        base.OnKeyDown(e);
    }

    private LiquidTabBarItem? FindItem(Visual source)
    {
        for (Visual? v = source; v is not null && !ReferenceEquals(v, this); v = v.GetVisualParent())
        {
            if (v is LiquidTabBarItem item && IndexFromContainer(item) >= 0)
            {
                return item;
            }
        }
        return null;
    }

    /// <summary>The next enabled tab from <paramref name="from"/> in <paramref name="direction"/>, optionally wrapping around the ends.</summary>
    private int Step(int from, int direction, bool wrap)
    {
        var count = ItemCount;
        for (var i = 1; i <= count; i++)
        {
            var index = from + direction * i;
            if (wrap)
            {
                index = (index % count + count) % count;
            }
            else if (index < 0 || index >= count)
            {
                return -1;
            }
            if (IsSelectable(index))
            {
                return index;
            }
        }
        return -1;
    }

    private int FirstEnabled(int from, int direction)
    {
        for (var index = from; index >= 0 && index < ItemCount; index += direction)
        {
            if (IsSelectable(index))
            {
                return index;
            }
        }
        return -1;
    }

    private bool IsSelectable(int index) => ContainerFromIndex(index) is not { } container || container.IsEnabled;

    /// <summary>Only the selected tab is a tab stop; the arrow keys move between tabs.</summary>
    private void UpdateTabStops()
    {
        var selected = SelectedIndex;
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is { } container)
            {
                container.IsTabStop = i == selected || (selected < 0 && i == 0);
            }
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LiquidTabBarAutomationPeer(this);

    private sealed class LiquidTabBarAutomationPeer : SelectingItemsControlAutomationPeer
    {
        public LiquidTabBarAutomationPeer(LiquidTabBar owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}
