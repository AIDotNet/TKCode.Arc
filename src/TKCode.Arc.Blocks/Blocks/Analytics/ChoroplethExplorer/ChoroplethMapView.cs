using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>How a region's fill moves to a new colour.</summary>
internal enum ChoroplethFillMotion
{
    /// <summary>At once (first data, reduced motion).</summary>
    Jump,

    /// <summary>480ms (.22, 1, .36, 1) after the region's sweep delay (its centre's x / width × 260ms): metric changes.</summary>
    Sweep,

    /// <summary>480ms without delay (re-binning on zoom, colour overrides).</summary>
    Plain,
}

/// <summary>The hover card's content.</summary>
internal sealed record ChoroplethTip(string Title, string Rank, IReadOnlyList<ChoroplethTipRow> Rows, string? Hint);

/// <summary>One metric line of the hover card; the current metric is drawn in the foreground colour.</summary>
internal readonly record struct ChoroplethTipRow(string Label, string Value, bool IsCurrent);

/// <summary>
/// The map of a <see cref="ChoroplethExplorer"/>: one self-drawn control that paints every region of the shown area, the
/// opened region's subregions, the hover outline and the hover card, and moves a camera over them. The outlines are parsed
/// once (by <see cref="ChoroplethRegion"/>) and drawn under one transform; strokes are divided by the camera's scale so they
/// stay 1px (0.75px for subregions, 1.5px for the outline) like SVG's non-scaling stroke.
/// </summary>
/// <remarks>
/// Why code-driven: every region's fill, fill opacity and opacity is a tween with its own delay (the west-to-east sweep is
/// <c>delay = centre x / 975 × 260ms</c>) inside one drawing, and the camera is three values (scale, x, y) on one critically
/// damped spring (k 72, c 17: measured ω ≈ 8.5) that keep their velocity when the target changes; a ControlTheme animates
/// properties of controls, not entries of a drawing.
/// </remarks>
internal sealed class ChoroplethMapView : Control
{
    /// <summary>The camera spring (critically damped, ω ≈ 8.5 rad/s, 99% in about 800ms; notes 0 and 4.3).</summary>
    internal static readonly SpringSpec CameraSpring = SpringSpec.FromPhysical(72, 17);

    private static Cursor? s_hand;

    private static readonly TimeSpan FillDuration = ArcMotionPresets.Considered;
    private static readonly TimeSpan StateDuration = ArcMotionPresets.Standard;
    private static readonly TimeSpan LeaveDuration = ArcMotionPresets.Exit;
    private static readonly TimeSpan SubShowDelay = TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan SubHideDuration = ArcMotionPresets.Fast;
    private static readonly TimeSpan TipDuration = ArcMotionPresets.Fast;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(256);
    private readonly Dictionary<(uint, double), ImmutablePen> _pens = new();
    private readonly LiveLoop _loop;

    private readonly List<Slot> _slots = new();
    private readonly List<Slot> _leaving = new();
    private AnalyticsTween _leavingFade = new(0);
    private SubLayer? _subs;
    private readonly List<SubLayer> _subsLeaving = new();

    private AnalyticsSpringChannel _camScale = new(1, 0.0005, 0.001);
    private AnalyticsSpringChannel _camX = new(0, 0.01, 0.01);
    private AnalyticsSpringChannel _camY = new(0, 0.01, 0.01);

    private Size _viewBox = new(975, 610);
    private Slot? _hover;
    private Point _pointer;
    private ChoroplethTip? _tip;
    private AnalyticsTween _tipOpacity = new(0);
    private ChoroplethRegion? _outline;

    public ChoroplethMapView()
    {
        ClipToBounds = true;
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => RefreshTheme();
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    /// <summary>The pointer moved onto another region (null: off every region).</summary>
    public event Action<ChoroplethRegion?, bool>? HoverChanged;

    /// <summary>A region was clicked or tapped (true: a subregion).</summary>
    public event Action<ChoroplethRegion, bool>? Invoked;

    /// <summary>Builds the hover card for a region (true: a subregion); null shows none.</summary>
    public Func<ChoroplethRegion, bool, ChoroplethTip?>? TipProvider { get; set; }

    /// <summary>The region under the pointer (tests).</summary>
    internal ChoroplethRegion? HoveredRegion => _hover?.Region;

    /// <summary>The camera's current scale, x and y in view-box units (tests).</summary>
    internal (double Scale, double X, double Y) Camera => (_camScale.Value, _camX.Value, _camY.Value);

    /// <summary>The camera's target (tests).</summary>
    internal (double Scale, double X, double Y) CameraTarget => (_camScale.Target, _camX.Target, _camY.Target);

    /// <summary>The hover card's opacity (tests).</summary>
    internal double TipOpacity => _tipOpacity.Value;

    /// <summary>The hover card being shown (tests).</summary>
    internal ChoroplethTip? Tip => _tip;

    /// <summary>True while anything moves (tests).</summary>
    internal bool IsMoving => _loop.IsActive;

    /// <summary>Opacity, fill and colour of a top-level region (tests).</summary>
    internal (Color Color, double Fill, double Opacity, double Enter) StateOf(ChoroplethRegion region)
    {
        var slot = Find(region);
        return slot is null ? default : (slot.CurrentColor, slot.Fill.Value, slot.Opacity.Value, slot.Enter.Value);
    }

    /// <summary>The fill delay a region got for its last sweep (tests).</summary>
    internal TimeSpan SweepDelayOf(ChoroplethRegion region) => Find(region) is { } slot ? ChoroplethMath.SweepDelay(slot.Wave) : TimeSpan.Zero;

    /// <summary>The opacity of the shown subregion layer (tests).</summary>
    internal double SubLayerOpacity => _subs?.Opacity.Value ?? 0;

    /// <summary>The subregion layer's parent (tests).</summary>
    internal ChoroplethRegion? SubParent => _subs?.Parent;

    private bool Animates => IsLoaded && ArcMotion.IsFull(this);

    // Area and regions -------------------------------------------------------------------------------------------------

    /// <summary>Shows <paramref name="regions"/>. With <paramref name="animate"/> the old map fades out (180ms) and the new one fades in from west to east.</summary>
    public void SetArea(Size viewBox, IReadOnlyList<ChoroplethRegion> regions, bool animate)
    {
        animate &= Animates && _slots.Count > 0;
        _leaving.Clear();
        if (animate)
        {
            _leaving.AddRange(_slots);
            _leavingFade.Jump(1);
            _leavingFade.Start(0, LeaveDuration, AnalyticsEasings.EaseOut);
        }
        _slots.Clear();
        _hover = null;
        _tip = null;
        _tipOpacity.Jump(0);
        _viewBox = viewBox.Width > 0 && viewBox.Height > 0 ? viewBox : new Size(975, 610);
        foreach (var region in regions)
        {
            var slot = new Slot(region, _viewBox.Width);
            if (animate)
            {
                slot.Enter.Jump(0);
                slot.Enter.Start(1, FillDuration, AnalyticsEasings.Enter, ChoroplethMath.SweepDelay(slot.Wave));
            }
            _slots.Add(slot);
        }
        SetSubregions(null, null, false);
        _subsLeaving.Clear();
        _camScale.Jump(1);
        _camX.Jump(0);
        _camY.Jump(0);
        Kick();
        InvalidateMeasure();
    }

    /// <summary>True when the map draws exactly <paramref name="regions"/>.</summary>
    public bool Shows(IReadOnlyList<ChoroplethRegion> regions) =>
        regions.Count == _slots.Count && _slots.Select(x => x.Region).SequenceEqual(regions);

    /// <summary>Sets the colour and fill opacity of top-level region <paramref name="index"/>.</summary>
    public void SetFill(int index, Color color, double fill, ChoroplethFillMotion motion)
    {
        if ((uint)index < (uint)_slots.Count)
        {
            _slots[index].SetFill(color, fill, Animates ? motion : ChoroplethFillMotion.Jump);
            Kick();
        }
    }

    /// <summary>Sets the opacity of top-level region <paramref name="index"/> (context .26, dimmed .14, hidden 0) over 240ms.</summary>
    public void SetOpacity(int index, double opacity, bool animate)
    {
        if ((uint)index < (uint)_slots.Count)
        {
            _slots[index].SetOpacity(opacity, animate && Animates ? StateDuration : TimeSpan.Zero);
            Kick();
        }
    }

    /// <summary>
    /// Shows the subregions of <paramref name="parent"/> (null hides them): the layer fades in over 480ms after 140ms, the
    /// previous layer fades out over 160ms.
    /// </summary>
    public void SetSubregions(ChoroplethRegion? parent, IReadOnlyList<ChoroplethRegion>? subregions, bool animate)
    {
        if (parent is not null && _subs is { } same && ReferenceEquals(same.Parent, parent) && subregions is not null &&
            same.Slots.Count == subregions.Count && same.Slots.Select(x => x.Region).SequenceEqual(subregions))
        {
            return;
        }
        animate &= Animates;
        if (_subs is { } old)
        {
            if (animate)
            {
                old.Opacity.Start(0, SubHideDuration, AnalyticsEasings.Standard);
                _subsLeaving.Add(old);
            }
            if (_hover is { IsSub: true })
            {
                _hover = null;
                _tipOpacity.Jump(0);
            }
        }
        _subs = null;
        if (parent is not null && subregions is { Count: > 0 })
        {
            var layer = new SubLayer(parent);
            foreach (var region in subregions)
            {
                layer.Slots.Add(new Slot(region, _viewBox.Width, true));
            }
            if (animate)
            {
                layer.Opacity.Jump(0);
                layer.Opacity.Start(1, FillDuration, AnalyticsEasings.Standard, SubShowDelay);
            }
            else
            {
                layer.Opacity.Jump(1);
            }
            _subs = layer;
        }
        Kick();
    }

    /// <summary>Sets the colour and fill opacity of subregion <paramref name="index"/> of the shown layer.</summary>
    public void SetSubFill(int index, Color color, double fill, ChoroplethFillMotion motion)
    {
        if (_subs is { } layer && (uint)index < (uint)layer.Slots.Count)
        {
            layer.Slots[index].SetFill(color, fill, Animates ? motion : ChoroplethFillMotion.Jump);
            Kick();
        }
    }

    /// <summary>Sets the opacity of subregion <paramref name="index"/> (dimmed .14) over 240ms.</summary>
    public void SetSubOpacity(int index, double opacity, bool animate)
    {
        if (_subs is { } layer && (uint)index < (uint)layer.Slots.Count)
        {
            layer.Slots[index].SetOpacity(opacity, animate && Animates ? StateDuration : TimeSpan.Zero);
            Kick();
        }
    }

    /// <summary>Points the camera at <paramref name="box"/> (null: the whole area) on the camera spring, keeping its velocity.</summary>
    public void SetCamera(Rect? box, bool animate)
    {
        var (s, x, y) = box is { } b ? ChoroplethMath.CameraFor(b, _viewBox) : (1.0, 0.0, 0.0);
        if (animate && Animates)
        {
            _camScale.SpringTo(s, CameraSpring);
            _camX.SpringTo(x, CameraSpring);
            _camY.SpringTo(y, CameraSpring);
        }
        else
        {
            _camScale.Jump(s);
            _camX.Jump(x);
            _camY.Jump(y);
        }
        Kick();
    }

    /// <summary>Outlines <paramref name="region"/> (a row under the pointer or the keyboard, a pinned subregion) without a card.</summary>
    public void SetOutline(ChoroplethRegion? region)
    {
        if (!ReferenceEquals(_outline, region))
        {
            _outline = region;
            InvalidateVisual();
        }
    }

    /// <summary>Rebuilds the hover card of the region under the pointer (the metric changed).</summary>
    public void RefreshTip()
    {
        if (_hover is { } hover && _tip is not null)
        {
            _tip = TipProvider?.Invoke(hover.Region, hover.IsSub);
            InvalidateVisual();
        }
    }

    /// <summary>Hides the hover card and forgets the region under the pointer.</summary>
    public void ClearHover()
    {
        if (_hover is null && _tipOpacity.Value <= 0)
        {
            return;
        }
        _hover = null;
        if (Animates)
        {
            _tipOpacity.Start(0, TipDuration, AnalyticsEasings.Standard);
        }
        else
        {
            _tipOpacity.Jump(0);
        }
        Kick();
        HoverChanged?.Invoke(null, false);
    }

    private Slot? Find(ChoroplethRegion region)
    {
        foreach (var slot in _slots)
        {
            if (ReferenceEquals(slot.Region, region))
            {
                return slot;
            }
        }
        return null;
    }

    private void Kick()
    {
        _loop.Start();
        InvalidateVisual();
    }

    private bool Advance(double dt)
    {
        var moving = false;
        foreach (var slot in _slots)
        {
            moving |= slot.Advance(dt);
        }
        if (_leaving.Count > 0)
        {
            if (!_leavingFade.Advance(dt))
            {
                _leaving.Clear();
            }
            else
            {
                moving = true;
            }
        }
        if (_subs is { } layer)
        {
            moving |= layer.Opacity.Advance(dt);
            foreach (var slot in layer.Slots)
            {
                moving |= slot.Advance(dt);
            }
        }
        for (var i = _subsLeaving.Count - 1; i >= 0; i--)
        {
            if (!_subsLeaving[i].Opacity.Advance(dt))
            {
                _subsLeaving.RemoveAt(i);
            }
            else
            {
                moving = true;
            }
        }
        moving |= _camScale.Step(dt);
        moving |= _camX.Step(dt);
        moving |= _camY.Step(dt);
        moving |= _tipOpacity.Advance(dt);
        if (_tipOpacity.Value <= 0 && _hover is null)
        {
            _tip = null;
        }
        InvalidateVisual();
        return moving;
    }

    // Layout ---------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : _viewBox.Width;
        return new Size(width, Math.Round(width * _viewBox.Height / _viewBox.Width, 2));
    }

    private double ViewScale => Bounds.Width > 0 ? Bounds.Width / _viewBox.Width : 1;

    /// <summary>The view-box point under a point of this control, through the camera.</summary>
    private Point ToWorld(Point p)
    {
        var k = ViewScale;
        var s = Math.Max(1e-6, _camScale.Value);
        return new Point((p.X / k - _camX.Value) / s, (p.Y / k - _camY.Value) / s);
    }

    /// <summary>The region at a point of this control: the shown subregions first, then every visible top-level region.</summary>
    internal (ChoroplethRegion Region, bool IsSub)? RegionAt(Point point)
    {
        var slot = HitTest(point);
        return slot is null ? null : (slot.Region, slot.IsSub);
    }

    private Slot? HitTest(Point point)
    {
        if (!new Rect(Bounds.Size).Contains(point))
        {
            return null;
        }
        var world = ToWorld(point);
        if (_subs is { } layer && layer.Opacity.Target > 0)
        {
            var unit = Math.Max(1e-6, layer.Parent.SubregionScale);
            var local = new Point((world.X - layer.Parent.SubregionOrigin.X) / unit, (world.Y - layer.Parent.SubregionOrigin.Y) / unit);
            for (var i = layer.Slots.Count - 1; i >= 0; i--)
            {
                var slot = layer.Slots[i];
                if (slot.Contains(local))
                {
                    return slot;
                }
            }
        }
        for (var i = _slots.Count - 1; i >= 0; i--)
        {
            var slot = _slots[i];
            if (slot.Opacity.Target <= 0.001 || slot.Enter.Target <= 0)
            {
                continue;
            }
            if (slot.Contains(world))
            {
                return slot;
            }
        }
        return null;
    }

    // Input -------------------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _pointer = e.GetPosition(this);
        var slot = HitTest(_pointer);
        if (!ReferenceEquals(slot, _hover))
        {
            _hover = slot;
            if (slot is not null)
            {
                _tip = TipProvider?.Invoke(slot.Region, slot.IsSub);
                if (Animates && _tipOpacity.Value < 1)
                {
                    _tipOpacity.Start(1, TipDuration, AnalyticsEasings.Standard);
                }
                else if (!Animates)
                {
                    _tipOpacity.Jump(1);
                }
            }
            else if (Animates)
            {
                _tipOpacity.Start(0, TipDuration, AnalyticsEasings.Standard);
            }
            else
            {
                _tipOpacity.Jump(0);
            }
            Cursor = slot is null ? null : s_hand ??= new Cursor(StandardCursorType.Hand);
            HoverChanged?.Invoke(slot?.Region, slot?.IsSub ?? false);
            Kick();
        }
        else if (slot is not null)
        {
            // The card follows the pointer every frame (no smoothing; notes 6).
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHover();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        var slot = HitTest(e.GetPosition(this));
        if (slot is not null)
        {
            e.Handled = true;
            if (e.Pointer.Type == PointerType.Touch)
            {
                ClearHover();
            }
            Invoked?.Invoke(slot.Region, slot.IsSub);
        }
    }

    // Theme -------------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ResourcesChanged += OnResourcesChanged;
        RefreshTheme();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnResourcesChanged(object? sender, Avalonia.Controls.ResourcesChangedEventArgs e) => RefreshTheme();

    private void RefreshTheme()
    {
        if (this.IsAttachedToVisualTree())
        {
            _palette.Refresh(this);
            _pens.Clear();
            _text.Clear();
            InvalidateVisual();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MapPeer(this);

    // Drawing -------------------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            if (!this.IsAttachedToVisualTree())
            {
                return;
            }
            _palette.Refresh(this);
        }
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var k = ViewScale;
        var s = Math.Max(1e-6, _camScale.Value);
        var camera = Matrix.CreateScale(s, s) * Matrix.CreateTranslation(_camX.Value, _camY.Value) * Matrix.CreateScale(k, k);
        var surface = _palette.Surface;
        using (context.PushTransform(camera))
        {
            if (_leaving.Count > 0 && _leavingFade.Value > 0.002)
            {
                using (context.PushOpacity(_leavingFade.Value))
                {
                    var pen = Pen(surface, 1 / (k * s));
                    foreach (var slot in _leaving)
                    {
                        slot.Draw(context, _palette, pen, 1);
                    }
                }
            }
            var statePen = Pen(surface, 1 / (k * s));
            foreach (var slot in _slots)
            {
                slot.Draw(context, _palette, statePen, 1);
            }
            foreach (var layer in _subsLeaving)
            {
                DrawLayer(context, layer, k, s, surface);
            }
            if (_subs is { } shown)
            {
                DrawLayer(context, shown, k, s, surface);
            }
            DrawOutline(context, k, s);
        }
        DrawTip(context, size);
    }

    private void DrawLayer(DrawingContext context, SubLayer layer, double k, double s, Color surface)
    {
        var opacity = layer.Opacity.Value;
        if (opacity <= 0.002)
        {
            return;
        }
        var unit = Math.Max(1e-6, layer.Parent.SubregionScale);
        var origin = layer.Parent.SubregionOrigin;
        using (context.PushTransform(Matrix.CreateScale(unit, unit) * Matrix.CreateTranslation(origin.X, origin.Y)))
        using (context.PushOpacity(opacity))
        {
            var pen = Pen(surface, 0.75 / (k * s * unit));
            foreach (var slot in layer.Slots)
            {
                slot.Draw(context, _palette, pen, 1);
            }
        }
    }

    private void DrawOutline(DrawingContext context, double k, double s)
    {
        var target = _hover?.Region ?? _outline;
        if (target is null)
        {
            return;
        }
        var geometry = target.ResolveGeometry();
        if (geometry is null)
        {
            return;
        }
        var isSub = _hover is { } hover ? hover.IsSub : _subs is { } layer && layer.Contains(target);
        if (isSub && _subs is { } subs)
        {
            var unit = Math.Max(1e-6, subs.Parent.SubregionScale);
            var origin = subs.Parent.SubregionOrigin;
            using (context.PushTransform(Matrix.CreateScale(unit, unit) * Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                context.DrawGeometry(null, Pen(_palette.Foreground, 1.5 / (k * s * unit)), geometry);
            }
            return;
        }
        if (Find(target) is null)
        {
            return;
        }
        context.DrawGeometry(null, Pen(_palette.Foreground, 1.5 / (k * s)), geometry);
    }

    private ImmutablePen Pen(Color color, double thickness)
    {
        // Strokes are divided by the camera scale (non-scaling strokes); quantized so a zoom reuses a few dozen pens.
        var q = Math.Round(thickness, 4);
        var key = ((uint)color.A << 24 | (uint)color.R << 16 | (uint)color.G << 8 | color.B, q);
        if (!_pens.TryGetValue(key, out var pen))
        {
            if (_pens.Count > 512)
            {
                _pens.Clear();
            }
            pen = new ImmutablePen(new ImmutableSolidColorBrush(color), q, null, PenLineCap.Round, PenLineJoin.Round);
            _pens[key] = pen;
        }
        return pen;
    }

    // Hover card -------------------------------------------------------------------------------------------------------

    private const double TipPaddingX = 12;
    private const double TipPaddingY = 10;
    private const double TipMinWidth = 200;
    private const double TipMaxWidth = 260;
    private const double TipGap = 16;
    private const double TipTitleLine = 19.6;
    private const double TipRowLine = 16.8;
    private const double TipRowGap = 2;
    private const double TipRowsTop = 6;
    private const double TipHintGap = 8;

    private Typeface BodyFace => new(_palette.BodyFont);

    private Typeface MediumFace => new(_palette.BodyFont, FontStyle.Normal, _palette.Medium);

    private Size MeasureTip(ChoroplethTip tip, double maxWidth)
    {
        var title = _text.Get(tip.Title, MediumFace, 14);
        var rank = _text.Get(tip.Rank, BodyFace, 12, true);
        var width = title.Width + 12 + rank.Width;
        foreach (var row in tip.Rows)
        {
            width = Math.Max(width, _text.Get(row.Label, BodyFace, 12).Width + 16 + _text.Get(row.Value, BodyFace, 12, true).Width);
        }
        if (tip.Hint is { } hint)
        {
            width = Math.Max(width, _text.Get(hint, BodyFace, 12).Width);
        }
        width = Math.Clamp(Math.Ceiling(width) + 2 * TipPaddingX, TipMinWidth, Math.Max(TipMinWidth, maxWidth));
        var height = 2 * TipPaddingY + TipTitleLine;
        if (tip.Rows.Count > 0)
        {
            height += TipRowsTop + tip.Rows.Count * TipRowLine + (tip.Rows.Count - 1) * TipRowGap;
        }
        if (tip.Hint is not null)
        {
            height += TipHintGap + 1 + TipHintGap + TipRowLine;
        }
        return new Size(width, height);
    }

    private void DrawTip(DrawingContext context, Size bounds)
    {
        var opacity = _tipOpacity.Value;
        if (_tip is not { } tip || opacity <= 0.002)
        {
            return;
        }
        var maxWidth = Math.Min(TipMaxWidth, bounds.Width * 0.8);
        var size = MeasureTip(tip, maxWidth);
        var x = _pointer.X + TipGap;
        if (x + size.Width > bounds.Width)
        {
            x = _pointer.X - TipGap - size.Width;
        }
        var y = Math.Clamp(_pointer.Y - size.Height / 2, 0, Math.Max(0, bounds.Height - size.Height));
        var rect = new Rect(x, y, size.Width, size.Height);
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(_palette.Brush(_palette.SurfaceRaised), _palette.Pen(_palette.Border, 1),
                new RoundedRect(rect.Deflate(0.5), 12), _palette.ShadowFloating);
            var inner = rect.Deflate(new Thickness(TipPaddingX, TipPaddingY));
            var title = _text.GetTrimmed(tip.Title, MediumFace, 14, Math.Max(0, inner.Width - 12 - _text.Get(tip.Rank, BodyFace, 12, true).Width));
            title.Draw(context, _palette.Brush(_palette.Foreground), new Point(inner.X, inner.Y + (TipTitleLine - title.Height) / 2));
            var rank = _text.Get(tip.Rank, BodyFace, 12, true);
            rank.Draw(context, _palette.Brush(_palette.TextMuted), new Point(inner.Right - rank.Width, inner.Y + (TipTitleLine - rank.Height) / 2));
            var rowY = inner.Y + TipTitleLine + TipRowsTop;
            foreach (var row in tip.Rows)
            {
                var brush = _palette.Brush(row.IsCurrent ? _palette.Foreground : _palette.TextMuted);
                var label = _text.Get(row.Label, BodyFace, 12);
                var value = _text.Get(row.Value, BodyFace, 12, true);
                label.Draw(context, brush, new Point(inner.X, rowY + (TipRowLine - label.Height) / 2));
                value.Draw(context, brush, new Point(inner.Right - value.Width, rowY + (TipRowLine - value.Height) / 2));
                rowY += TipRowLine + TipRowGap;
            }
            if (tip.Hint is { } hint)
            {
                var ruleY = rowY - TipRowGap + TipHintGap;
                context.DrawRectangle(_palette.Brush(_palette.BorderSubtle), null, new Rect(inner.X, ruleY, inner.Width, 1));
                var text = _text.GetTrimmed(hint, BodyFace, 12, inner.Width);
                text.Draw(context, _palette.Brush(_palette.TextSecondary), new Point(inner.X, ruleY + 1 + TipHintGap + (TipRowLine - text.Height) / 2));
            }
        }
    }

    // Parts ---------------------------------------------------------------------------------------------------------------

    /// <summary>One drawn region with its tweens.</summary>
    private sealed class Slot
    {
        public Slot(ChoroplethRegion region, double viewWidth, bool isSub = false)
        {
            Region = region;
            IsSub = isSub;
            Box = region.ResolveBounds();
            Wave = ChoroplethMath.Wave(Box, viewWidth);
        }

        public readonly ChoroplethRegion Region;
        public readonly bool IsSub;
        public readonly Rect Box;
        public readonly double Wave;
        public Color From;
        public Color To;
        public AnalyticsTween ColorProgress = new(1);
        public AnalyticsTween Fill = new(0);
        public AnalyticsTween Opacity = new(1);
        public AnalyticsTween Enter = new(1);
        private bool _hasFill;

        public Color CurrentColor => ColorProgress.Value >= 1 ? To : Lerp(From, To, ColorProgress.Value);

        public void SetFill(Color color, double fill, ChoroplethFillMotion motion)
        {
            if (!_hasFill || motion == ChoroplethFillMotion.Jump)
            {
                _hasFill = true;
                From = color;
                To = color;
                ColorProgress.Jump(1);
                Fill.Jump(fill);
                return;
            }
            if (color == To && Math.Abs(fill - Fill.Target) < 1e-6)
            {
                return;
            }
            var delay = motion == ChoroplethFillMotion.Sweep ? ChoroplethMath.SweepDelay(Wave) : TimeSpan.Zero;
            if (color != To)
            {
                From = CurrentColor;
                To = color;
                ColorProgress.Jump(0);
                ColorProgress.Start(1, FillDuration, AnalyticsEasings.Standard, delay);
            }
            Fill.Start(fill, FillDuration, AnalyticsEasings.Standard, delay);
        }

        public void SetOpacity(double opacity, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                Opacity.Jump(opacity);
            }
            else if (Math.Abs(opacity - Opacity.Target) > 1e-6 || Opacity.IsRunning)
            {
                Opacity.Start(opacity, duration, AnalyticsEasings.Standard);
            }
        }

        public bool Advance(double dt)
        {
            var moving = ColorProgress.Advance(dt);
            moving |= Fill.Advance(dt);
            moving |= Opacity.Advance(dt);
            moving |= Enter.Advance(dt);
            return moving;
        }

        public bool Contains(Point point)
        {
            if (!Box.Inflate(0.5).Contains(point))
            {
                return false;
            }
            return Region.ResolveGeometry()?.FillContains(point) ?? false;
        }

        public void Draw(DrawingContext context, AnalyticsPalette palette, IPen pen, double layerOpacity)
        {
            var opacity = Opacity.Value * Enter.Value * layerOpacity;
            if (opacity <= 0.002)
            {
                return;
            }
            var geometry = Region.ResolveGeometry();
            if (geometry is null)
            {
                return;
            }
            var brush = palette.Brush(CurrentColor, Math.Clamp(Fill.Value, 0, 1));
            if (opacity >= 0.999)
            {
                context.DrawGeometry(brush, pen, geometry);
                return;
            }
            using (context.PushOpacity(opacity))
            {
                context.DrawGeometry(brush, pen, geometry);
            }
        }

        private static Color Lerp(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * t),
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }
    }

    /// <summary>The subregions of one opened region.</summary>
    private sealed class SubLayer(ChoroplethRegion parent)
    {
        public readonly ChoroplethRegion Parent = parent;
        public readonly List<Slot> Slots = new();
        public AnalyticsTween Opacity = new(1);

        public bool Contains(ChoroplethRegion region)
        {
            foreach (var slot in Slots)
            {
                if (ReferenceEquals(slot.Region, region))
                {
                    return true;
                }
            }
            return false;
        }
    }

    private sealed class MapPeer(ChoroplethMapView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
