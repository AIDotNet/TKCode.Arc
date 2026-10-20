using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Which side of a divider is hidden.</summary>
internal enum ResizablePanelsMode
{
    Open,
    Before,
    After
}

/// <summary>
/// A divider of <see cref="ResizablePanels"/>: a one pixel line inside a wider grab area, a grip that forms while it
/// is hovered, focused or dragged, and a restore tab once a neighbouring pane is hidden. Drawn in
/// <see cref="Render"/>; its states animate with <see cref="MotionValue{T}"/> because the grip, line and tab are
/// one drawn shape whose parts use different curves (fast fades, the CSS spring scale, the snappy tab pop).
/// </summary>
internal sealed class ResizablePanelsHandle : Control
{
    /// <summary>Grab area around the line (Arc: inset 0 −6px; 30px into the visible pane when collapsed).</summary>
    public const double Reach = 6;

    public const double CollapsedReach = 30;

    private static readonly Geometry RightChevron = ArcIcons.ChevronRight;
    private static readonly Geometry LeftChevron = ArcIcons.ChevronLeft;

    private readonly ResizablePanels _owner;
    private MotionValue<double>? _hot;
    private MotionValue<double>? _gripScale;
    private MotionValue<double>? _active;
    private MotionValue<double>? _tab;
    private MotionValue<double>? _tabScale;
    private bool _pointerOver;
    private bool _dragging;
    private SolidColorBrush? _lineBrush;
    private SolidColorBrush? _gripBrush;
    private SolidColorBrush? _tabStroke;
    private SolidColorBrush? _tabGlyph;
    private Pen? _tabPen;
    private Pen? _glyphPen;
    private ResizablePanelsMode _tabSide = ResizablePanelsMode.Before;
    private Cursor? _resizeCursor;
    private Cursor? _handCursor;

    public ResizablePanelsHandle(ResizablePanels owner, int index)
    {
        _owner = owner;
        Index = index;
        Focusable = true;
        IsTabStop = true;
        ClipToBounds = false;
    }

    /// <summary>Divider index: between pane <c>Index</c> and <c>Index + 1</c>.</summary>
    public int Index { get; }

    public ResizablePanelsMode Mode { get; private set; }

    /// <summary>The x of the one pixel line inside this control.</summary>
    public double LineOffset => Mode switch
    {
        ResizablePanelsMode.Before => 0,
        ResizablePanelsMode.After => CollapsedReach,
        _ => Reach
    };

    /// <summary>Width of the control: the line plus the grab area.</summary>
    public double ReachWidth => Mode == ResizablePanelsMode.Open ? 2 * Reach + 1 : CollapsedReach + 1;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _resizeCursor ??= new Cursor(StandardCursorType.SizeWestEast);
        _handCursor ??= new Cursor(StandardCursorType.Hand);
        Cursor = Mode == ResizablePanelsMode.Open ? _resizeCursor : _handCursor;
    }

    public void SetMode(ResizablePanelsMode mode)
    {
        if (Mode == mode)
        {
            return;
        }
        var wasOpen = Mode == ResizablePanelsMode.Open;
        Mode = mode;
        if (mode != ResizablePanelsMode.Open)
        {
            _tabSide = mode;
        }
        Cursor = mode == ResizablePanelsMode.Open ? _resizeCursor : _handCursor;
        InvalidateMeasure();
        _tab ??= Create(0);
        _tabScale ??= Create(1);
        var shown = mode != ResizablePanelsMode.Open;
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (level == ArcMotionLevel.Off)
        {
            _tab.Jump(shown ? 1 : 0);
            _tabScale.Jump(1);
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            _tabScale.Jump(1);
            _ = _tab.AnimateTo(shown ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else if (shown)
        {
            // Pops in: scale .6 → 1 on the snappy spring, opacity and blur on fast + enter.
            if (wasOpen)
            {
                _tab.Jump(0);
                _tabScale.Jump(0.6);
            }
            _ = _tab.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
            _ = _tabScale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            var exit = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
            _ = _tab.AnimateTo(0, exit);
            _ = _tabScale.AnimateTo(0.6, exit);
        }
        UpdateHot();
    }

    public void SetDragging(bool dragging)
    {
        _dragging = dragging;
        _active ??= Create(0);
        Animate(_active, dragging ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        UpdateHot();
    }

    private void UpdateHot()
    {
        _hot ??= Create(0);
        _gripScale ??= Create(0.6);
        var open = Mode == ResizablePanelsMode.Open;
        var hot = open && (_pointerOver || _dragging || (IsFocused && Classes.Contains(":focus-visible")));
        Animate(_hot, hot ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        // The grip forms on Arc's CSS spring (580ms, slight overshoot).
        Animate(_gripScale, hot ? 1 : 0.6, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        InvalidateVisual();
    }

    private void Animate(MotionValue<double> value, double target, MotionSpec spec)
    {
        if (Math.Abs(value.Target - target) < 0.0001)
        {
            return;
        }
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            value.Jump(target);
            return;
        }
        _ = value.AnimateTo(target, spec);
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override Size MeasureOverride(Size availableSize) => new(ReachWidth, 0);

    #region Input (forwarded to the group)

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _pointerOver = true;
            UpdateHot();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerOver = false;
        UpdateHot();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(_owner);
        if (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            _owner.Reset();
            return;
        }
        Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(this);
        _owner.BeginDrag(this, e.Pointer, point.Position.X, e.Timestamp);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _owner.MoveDrag(e.Pointer, e.GetPosition(_owner).X, e.Timestamp);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _owner.EndDrag(e.Pointer, e.GetPosition(_owner).X, e.Timestamp, cancelled: false);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _owner.CancelDrag(e.Pointer);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && _owner.HandleKey(Index, e.Key, (e.KeyModifiers & KeyModifiers.Shift) != 0))
        {
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateHot();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateHot();
    }

    #endregion

    #region Rendering

    public override void Render(DrawingContext context)
    {
        var height = Bounds.Height;
        var x = LineOffset;
        var hot = Math.Clamp(_hot?.Value ?? 0, 0, 1);
        var active = Math.Clamp(_active?.Value ?? 0, 0, 1);
        var tab = Math.Clamp(_tab?.Value ?? 0, 0, 1);

        // The line: border, border-strong while hot or dragged, transparent once a neighbour is hidden.
        var lineColor = Mix(ColorOf(_owner.HandleLineBrush), ColorOf(_owner.HandleLineHotBrush), Math.Max(hot, active));
        if (Mode != ResizablePanelsMode.Open)
        {
            lineColor = Color.FromArgb(0, lineColor.R, lineColor.G, lineColor.B);
        }
        if (lineColor.A > 0)
        {
            _lineBrush ??= new SolidColorBrush();
            _lineBrush.Color = lineColor;
            context.FillRectangle(_lineBrush, new Rect(x, 0, 1, height));
        }

        // The grip: a 4 × 32 pill that forms (opacity, scaleY .6 → 1) and turns foreground while dragged.
        var gripOpacity = Math.Max(hot, active);
        if (gripOpacity > 0.001 && Mode == ResizablePanelsMode.Open)
        {
            var scale = _gripScale?.Value ?? 1;
            var gripHeight = 32 * scale;
            var rect = new Rect(x + 0.5 - 2, height / 2 - gripHeight / 2, 4, gripHeight);
            _gripBrush ??= new SolidColorBrush();
            _gripBrush.Color = Mix(ColorOf(_owner.GripBrush), ColorOf(_owner.GripActiveBrush), active);
            using (context.PushOpacity(gripOpacity))
            {
                context.DrawRectangle(_gripBrush, null, new RoundedRect(rect, 2));
            }
        }

        // The restore tab: an 18 × 40 pill beside the line that points the way the pane comes back.
        if (tab > 0.001)
        {
            var before = _tabSide == ResizablePanelsMode.Before;
            var tabRect = before ? new Rect(x + 5, height / 2 - 20, 18, 40) : new Rect(x - 5 - 18 + 1, height / 2 - 20, 18, 40);
            var hotTab = Mode != ResizablePanelsMode.Open && (_pointerOver || (IsFocused && Classes.Contains(":focus-visible")));
            var scale = _tabScale?.Value ?? 1;
            var origin = before ? new Point(tabRect.Left, tabRect.Center.Y) : new Point(tabRect.Right, tabRect.Center.Y);
            var matrix = Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y);
            _tabStroke ??= new SolidColorBrush();
            _tabStroke.Color = hotTab ? ColorOf(_owner.RestoreStrokeHot) : ColorOf(_owner.RestoreStroke);
            _tabGlyph ??= new SolidColorBrush();
            _tabGlyph.Color = hotTab ? ColorOf(_owner.RestoreGlyphHot) : ColorOf(_owner.RestoreGlyph);
            _tabPen ??= new Pen(_tabStroke, 1);
            _glyphPen ??= new Pen(_tabGlyph, 1.75 * 14 / 24, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            using (context.PushOpacity(tab))
            using (context.PushTransform(matrix))
            {
                var rounded = new RoundedRect(tabRect.Deflate(0.5), 9);
                context.DrawRectangle(_owner.RestoreFill, _tabPen, rounded, _owner.RestoreShadow);
                var glyph = before ? RightChevron : LeftChevron;
                var glyphMatrix = Matrix.CreateScale(14 / 24.0, 14 / 24.0) * Matrix.CreateTranslation(tabRect.Center.X - 7, tabRect.Center.Y - 7);
                using (context.PushTransform(glyphMatrix))
                {
                    context.DrawGeometry(null, _glyphPen, glyph);
                }
            }
        }
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new HandleAutomationPeer(this);

    private sealed class HandleAutomationPeer : ControlAutomationPeer, IRangeValueProvider
    {
        public HandleAutomationPeer(ResizablePanelsHandle owner) : base(owner)
        {
        }

        private ResizablePanelsHandle Handle => (ResizablePanelsHandle)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Thumb;

        protected override string? GetNameCore() => Handle._owner.PaneLabel(Handle.Index);

        protected override string? GetHelpTextCore() => Handle._owner.ValueText(Handle.Index);

        public bool IsReadOnly => false;

        public double Minimum => Handle._owner.ShareLimits(Handle.Index).Min;

        public double Maximum => Handle._owner.ShareLimits(Handle.Index).Max;

        public double Value => Math.Round(Handle._owner.Share(Handle.Index));

        public double SmallChange => 1;

        public double LargeChange => 5;

        public void SetValue(double value) => Handle._owner.SetShare(Handle.Index, value);
    }
}
