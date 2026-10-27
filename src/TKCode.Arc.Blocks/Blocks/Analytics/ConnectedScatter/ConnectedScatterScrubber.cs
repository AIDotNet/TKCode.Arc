using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The month scrubber under the chart: a 44px band with a 4px rail (the played part in Accent), a 2px dot per month, a
/// 20px thumb with an 8px centre, and under it the quarter labels (first month, January / April / July / October, last
/// month). Pressing moves the head to the pointer, dragging follows it, releasing snaps to a month; the keys are the
/// block's. The block drives <see cref="SetPosition"/> and answers the events; this control draws and reports input.
/// </summary>
/// <remarks>
/// The thumb's hover (1.08), press (1.12) and focus-dot (1.5) scales run on the 580ms CSS spring curve, driven by this
/// control's own loop because the thumb and the dots are drawn, not template parts.
/// </remarks>
internal sealed class ConnectedScatterScrubber : Control
{
    public static readonly StyledProperty<double> BandHeightProperty =
        AvaloniaProperty.Register<ConnectedScatterScrubber, double>(nameof(BandHeight), 44);

    private const double PadX = 10;
    private const double TrackHeight = 4;
    private const double ThumbSize = 20;
    private const double CenterSize = 8;
    private const double DotSize = 2;
    private const double LabelRow = 18;
    private const double LabelOverlap = 4;
    private const double FocusOffset = 2;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(64);
    private readonly LiveLoop _loop;
    private AnalyticsTween _thumbScale = new(1);
    private AnalyticsTween _centerScale = new(1);
    private List<ScatterTimelineLabel> _labels = [];
    private IReadOnlyList<ScatterPoint> _points = [];
    private bool _quarters = true;
    private int _count;
    private double _u;
    private int _reported;
    private string _valueText = string.Empty;
    private string _name = string.Empty;
    private bool _dragging;
    private bool _hover;
    private int _paletteVersion = -1;
    private bool _dark;

    static ConnectedScatterScrubber()
    {
        AffectsMeasure<ConnectedScatterScrubber>(BandHeightProperty);
        FocusableProperty.OverrideDefaultValue<ConnectedScatterScrubber>(true);
        IsTabStopProperty.OverrideDefaultValue<ConnectedScatterScrubber>(true);
        CursorProperty.OverrideDefaultValue<ConnectedScatterScrubber>(new Cursor(StandardCursorType.Hand));
        FocusAdornerProperty.OverrideDefaultValue<ConnectedScatterScrubber>(null);
    }

    public ConnectedScatterScrubber()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => Invalidate();
        ResourcesChanged += (_, _) => Invalidate();
    }

    /// <summary>The pointer went down at a month position (continuous).</summary>
    public event Action<double>? ScrubStarted;

    /// <summary>The pointer moved while down.</summary>
    public event Action<double>? ScrubMoved;

    /// <summary>The pointer was released (or capture was lost).</summary>
    public event Action? ScrubEnded;

    /// <summary>A key went down: return true when the block used it.</summary>
    public Func<KeyEventArgs, bool>? KeyHandler { get; set; }

    /// <summary>Called when assistive technology sets the slider's value (a month index).</summary>
    public Action<int>? ValueRequested { get; set; }

    /// <summary>The band's height without the month labels (44).</summary>
    public double BandHeight { get => GetValue(BandHeightProperty); set => SetValue(BandHeightProperty, value); }

    /// <summary>The month labels shown now (tests).</summary>
    internal IReadOnlyList<ScatterTimelineLabel> Labels => _labels;

    /// <summary>The value text the slider reports (tests).</summary>
    internal string ValueText => _valueText;

    /// <summary>The month the slider reports, from 0 (tests).</summary>
    internal int ReportedIndex => _reported;

    /// <summary>True while the pointer is down on the scrubber.</summary>
    internal bool IsDragging => _dragging;

    /// <summary>The thumb's scale now (tests).</summary>
    internal double ThumbScale => _thumbScale.Value;

    /// <summary>The focus dot's scale now (tests).</summary>
    internal double CenterScale => _centerScale.Value;

    /// <summary>The thumb's x within the control at the current position.</summary>
    internal double ThumbX => XOf(_u);

    /// <summary>The slider's name for assistive technology.</summary>
    public string SliderName
    {
        get => _name;
        set
        {
            _name = value;
            AutomationProperties.SetName(this, value);
        }
    }

    /// <summary>Sets the months (labels under the rail) and whether the quarter labels are shown (false: only January and the ends).</summary>
    public void SetMonths(IReadOnlyList<ScatterPoint> points, bool quarters)
    {
        _points = points;
        _count = points.Count;
        _quarters = quarters;
        _labels = ConnectedScatterEngine.TimelineLabels(points, quarters);
        InvalidateVisual();
    }

    /// <summary>Moves the thumb and the fill to month position <paramref name="u"/>.</summary>
    public void SetPosition(double u)
    {
        if (Math.Abs(u - _u) < 1e-9)
        {
            return;
        }
        _u = u;
        InvalidateVisual();
    }

    /// <summary>Updates what the slider reports: the settled month and its value text.</summary>
    public void Report(int index, string valueText)
    {
        var old = _reported;
        _reported = index;
        _valueText = valueText;
        AutomationProperties.SetHelpText(this, valueText);
        if (old != index && ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, old + 1.0, index + 1.0);
        }
    }

    private void Invalidate()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateVisual();
    }

    private Rect Track => new(PadX, (BandHeight - TrackHeight) / 2, Math.Max(1, Bounds.Width - PadX * 2), TrackHeight);

    private double XOf(double u)
    {
        var track = Track;
        return _count < 2 ? track.X : track.X + Math.Clamp(u / (_count - 1), 0, 1) * track.Width;
    }

    private double PositionAt(double x) => ConnectedScatterEngine.PositionAt(x, Track.X, Track.Width, _count);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        return new Size(width, BandHeight + (_labels.Count > 0 ? LabelRow - LabelOverlap : 0));
    }

    #region Input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || _count == 0 || point.Position.Y > BandHeight)
        {
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
        SetScale(pressed: true);
        e.Handled = true;
        ScrubStarted?.Invoke(PositionAt(point.Position.X));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            ScrubMoved?.Invoke(PositionAt(e.GetPosition(this).X));
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hover = true;
            SetScale(pressed: _dragging);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        SetScale(pressed: _dragging);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag(null);
    }

    private void EndDrag(IPointer? pointer)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        if (pointer is not null && ReferenceEquals(pointer.Captured, this))
        {
            pointer.Capture(null);
        }
        SetScale(pressed: false);
        ScrubEnded?.Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && KeyHandler is { } handler)
        {
            e.Handled = handler(e);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        SetCenter(AnalyticsFocus.IsVisible(this));
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetCenter(false);
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InputElement.IsKeyboardFocusWithinProperty)
        {
            SetCenter(AnalyticsFocus.IsVisible(this));
        }
    }

    // Hover and press scale the thumb only at Full motion (Reduced removes scale effects).
    private void SetScale(bool pressed)
    {
        var target = pressed ? 1.12 : _hover ? 1.08 : 1;
        Animate(ref _thumbScale, ArcMotion.IsFull(this) ? target : 1);
    }

    // The focus dot's growth shows keyboard focus, so it stays at every level (it only stops gliding).
    private void SetCenter(bool focused) => Animate(ref _centerScale, focused ? 1.5 : 1);

    // The 580ms CSS spring curve: a drawn thumb cannot use a ControlTheme transition.
    private void Animate(ref AnalyticsTween tween, double target)
    {
        if (ArcMotion.IsFull(this))
        {
            tween.Start(target, TimeSpan.FromMilliseconds(580), ArcEasings.CssSpring);
            _loop.Start();
        }
        else
        {
            tween.Jump(target);
        }
        InvalidateVisual();
    }

    private bool Advance(double dt)
    {
        var moving = _thumbScale.Advance(dt) | _centerScale.Advance(dt);
        InvalidateVisual();
        return moving;
    }

    #endregion

    #region Render

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        // The whole 44px band answers the pointer.
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, size.Width, BandHeight));
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        if (_paletteVersion < 0 || !_palette.IsReady)
        {
            _palette.Refresh(this);
            _paletteVersion = _palette.Version;
            _dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        }
        var p = _palette;
        var track = Track;
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, p, new Rect(0, 0, size.Width, BandHeight), 0, FocusOffset);
        }
        context.DrawRectangle(p.Brush(p.SurfaceMuted), null, new RoundedRect(track, TrackHeight / 2));
        var x = XOf(_u);
        if (x > track.X)
        {
            context.DrawRectangle(p.Brush(p.Accent), null, new RoundedRect(new Rect(track.X, track.Y, x - track.X, track.Height), TrackHeight / 2));
        }
        var dot = p.Brush(_dark ? p.Background : p.Surface, 0.8);
        for (var i = 0; i < _count; i++)
        {
            var cx = XOf(i);
            context.DrawEllipse(dot, null, new Point(cx, BandHeight / 2), DotSize / 2, DotSize / 2);
        }
        DrawThumb(context, x);
        DrawLabels(context);
    }

    private void DrawThumb(DrawingContext context, double x)
    {
        var p = _palette;
        var center = new Point(x, BandHeight / 2);
        var scale = _thumbScale.Value;
        var half = ThumbSize / 2 * scale;
        var shadow = _dark
            ? new BoxShadows(new BoxShadow { Spread = 0.5, Color = Color.FromArgb(77, 0, 0, 0) },
                [new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb(89, 0, 0, 0) },
                 new BoxShadow { OffsetY = 2, Blur = 6, Color = Color.FromArgb(51, 0, 0, 0) }])
            : new BoxShadows(new BoxShadow { Spread = 0.5, Color = Color.FromArgb(18, 0, 0, 0) },
                [new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb(36, 0, 0, 0) },
                 new BoxShadow { OffsetY = 2, Blur = 6, Color = Color.FromArgb(15, 0, 0, 0) }]);
        context.DrawRectangle(p.Brush(p.Background), null, new RoundedRect(new Rect(center.X - half, center.Y - half, half * 2, half * 2), half), shadow);
        var inner = CenterSize / 2 * _centerScale.Value * scale;
        context.DrawEllipse(p.Brush(p.Accent), null, center, inner, inner);
    }

    private void DrawLabels(DrawingContext context)
    {
        if (_labels.Count == 0)
        {
            return;
        }
        var p = _palette;
        var face = new Typeface(p.BodyFont);
        var brush = p.Brush(p.TextMuted);
        var top = BandHeight - LabelOverlap;
        for (var k = 0; k < _labels.Count; k++)
        {
            var label = _labels[k];
            var text = _text.Get(label.Text, face, 12, true);
            var x = XOf(label.Index);
            double left;
            if (k == 0 && label.Index == 0)
            {
                // The first label starts at the thumb's outer edge, the last one ends at it.
                left = x - PadX;
            }
            else if (label.Index == _count - 1)
            {
                left = x + PadX - text.Width;
            }
            else
            {
                left = x - text.Width / 2;
            }
            text.Draw(context, brush, new Point(left, top + (LabelRow - text.Height) / 2));
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ScrubberPeer(this);

    private sealed class ScrubberPeer(ConnectedScatterScrubber owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        private new ConnectedScatterScrubber Owner => (ConnectedScatterScrubber)base.Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string? GetNameCore() => Owner._name;

        protected override string? GetHelpTextCore() => Owner._valueText;

        public bool IsReadOnly => false;

        public double Minimum => 1;

        public double Maximum => Math.Max(1, Owner._count);

        public double Value => Owner._reported + 1;

        public double SmallChange => 1;

        public double LargeChange => 3;

        public void SetValue(double value) => Owner.ValueRequested?.Invoke((int)Math.Round(value) - 1);
    }
}

/// <summary>
/// The months as a table for screen readers (Arc's visually hidden <c>&lt;table&gt;</c>): a zero-size control whose
/// automation peer is a table named by its caption, one row per month holding the month, the two values and the ratio.
/// Nothing is drawn and nothing takes input. Private copy of the Cohort retention pattern.
/// </summary>
internal sealed class ConnectedScatterDataTable : Control
{
    private string _caption = string.Empty;
    private string[] _rowHeaders = [];
    private string[][] _cells = [];

    static ConnectedScatterDataTable()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<ConnectedScatterDataTable>(false);
        FocusableProperty.OverrideDefaultValue<ConnectedScatterDataTable>(false);
    }

    /// <summary>The table's caption.</summary>
    public string Caption => _caption;

    /// <summary>The month names.</summary>
    public IReadOnlyList<string> RowHeaders => _rowHeaders;

    /// <summary>The cell texts, row by row (X, Y, ratio).</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows => _cells;

    /// <summary>Replaces the table (the peer tree is rebuilt).</summary>
    public void Update(string caption, string[] rowHeaders, string[][] cells)
    {
        _caption = caption;
        _rowHeaders = rowHeaders;
        _cells = cells;
        if (ControlAutomationPeer.FromElement(this) is ConnectedScatterDataTablePeer peer)
        {
            peer.Rebuild();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new ConnectedScatterDataTablePeer(this);
}

/// <summary>The table's peer: a table with a row peer per month.</summary>
internal sealed class ConnectedScatterDataTablePeer : ControlAutomationPeer
{
    private List<AutomationPeer>? _rows;

    public ConnectedScatterDataTablePeer(ConnectedScatterDataTable owner) : base(owner)
    {
    }

    private new ConnectedScatterDataTable Owner => (ConnectedScatterDataTable)base.Owner;

    internal void Rebuild()
    {
        _rows = null;
        InvalidateChildren();
        RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, null, Owner.Caption);
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

    protected override string? GetNameCore() => Owner.Caption;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override IReadOnlyList<AutomationPeer> GetChildrenCore()
    {
        if (_rows is null)
        {
            _rows = new List<AutomationPeer>(Owner.RowHeaders.Count);
            for (var r = 0; r < Owner.RowHeaders.Count; r++)
            {
                var row = new ConnectedScatterTextPeer(this, Owner, Owner.RowHeaders[r], AutomationControlType.DataItem);
                var cells = r < Owner.Rows.Count ? Owner.Rows[r] : [];
                var children = new List<AutomationPeer>(cells.Count + 1)
                {
                    new ConnectedScatterTextPeer(row, Owner, Owner.RowHeaders[r], AutomationControlType.HeaderItem)
                };
                foreach (var cell in cells)
                {
                    children.Add(new ConnectedScatterTextPeer(row, Owner, cell, AutomationControlType.Text));
                }
                row.SetChildren(children);
                _rows.Add(row);
            }
        }
        return _rows;
    }
}

/// <summary>A peer with a fixed name and type that is not backed by a control (a table row or cell).</summary>
internal sealed class ConnectedScatterTextPeer : AutomationPeer
{
    private readonly AutomationPeer _parent;
    private readonly Control _host;
    private readonly string _name;
    private readonly AutomationControlType _type;
    private IReadOnlyList<AutomationPeer> _children = [];

    public ConnectedScatterTextPeer(AutomationPeer parent, Control host, string name, AutomationControlType type)
    {
        _parent = parent;
        _host = host;
        _name = name;
        _type = type;
    }

    internal void SetChildren(IReadOnlyList<AutomationPeer> children) => _children = children;

    protected override void BringIntoViewCore()
    {
    }

    protected override string? GetAcceleratorKeyCore() => null;

    protected override string? GetAccessKeyCore() => null;

    protected override AutomationControlType GetAutomationControlTypeCore() => _type;

    protected override string? GetAutomationIdCore() => null;

    protected override Rect GetBoundingRectangleCore() => default;

    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => _children;

    protected override string GetClassNameCore() => _type == AutomationControlType.DataItem ? "row" : "cell";

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override string? GetNameCore() => _name;

    protected override AutomationPeer? GetParentCore() => _parent;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsEnabledCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override void SetFocusCore()
    {
    }

    protected override bool ShowContextMenuCore() => false;

    protected override bool TrySetParent(AutomationPeer? parent) => false;
}
