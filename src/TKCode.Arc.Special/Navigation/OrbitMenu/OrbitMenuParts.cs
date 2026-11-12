using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Carries the action an <see cref="OrbitMenu"/> fired.</summary>
public sealed class OrbitMenuActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="action"/>.</summary>
    public OrbitMenuActionEventArgs(RoutedEvent routedEvent, OrbitMenuAction action, object? item = null) : base(routedEvent)
    {
        Action = action;
        Item = item ?? action;
    }

    /// <summary>The action that fired.</summary>
    public OrbitMenuAction Action { get; }

    /// <summary>The item behind the action: the bound data item, or the action itself when it was written in XAML.</summary>
    public object Item { get; }
}

/// <summary>
/// The numbers of the orbit menu's motion and targeting that are not theme values: the swirl, the magnet and the lens.
/// </summary>
internal static class OrbitMenuGeometry
{
    /// <summary>How far behind its place (radians, counterclockwise) an action starts as it swirls out.</summary>
    public const double Swirl = 0.75;

    /// <summary>The scale an action leaves the trigger at.</summary>
    public const double LaunchScale = 0.4;

    /// <summary>The blur (px) an action leaves the trigger with; it clears by 60% of the way out.</summary>
    public const double LaunchBlur = 4;

    /// <summary>The magnet reaches this far (px) from an action's center.</summary>
    public const double MagnetRadius = 88;

    /// <summary>The share of the way to the pointer a nearby action leans.</summary>
    public const double MagnetPull = 0.2;

    /// <summary>How much an action under the pointer grows.</summary>
    public const double MagnetGrowth = 0.29;

    /// <summary>How much more the targeted action grows.</summary>
    public const double TargetGain = 0.1;

    /// <summary>The lens's scale away from the pointer (while it glides, or under the keyboard).</summary>
    public const double LensScale = 1.1;

    /// <summary>The lens is fully opaque from this scale up and fades below it.</summary>
    public const double LensOpaqueScale = 0.3;

    /// <summary>Delay between actions swirling out, in reading order.</summary>
    public static readonly TimeSpan OpenStagger = TimeSpan.FromMilliseconds(35);

    /// <summary>Delay between actions folding back, in reverse order.</summary>
    public static readonly TimeSpan CloseStagger = TimeSpan.FromMilliseconds(18);

    /// <summary>How long a folding action stays opaque before it fades.</summary>
    public static readonly TimeSpan CloseFadeDelay = TimeSpan.FromMilliseconds(60);

    public static readonly SpringSpec OpenSpring = SpringSpec.FromVisual(0.5, 0);
    public static readonly SpringSpec CloseSpring = ArcMotionPresets.Settle;
    public static readonly SpringSpec MorphSpring = SpringSpec.FromVisual(0.42, 0);
    public static readonly SpringSpec PillSpring = SpringSpec.FromVisual(0.28, 0);
    public static readonly SpringSpec LensHideSpring = SpringSpec.FromVisual(0.2, 0);

    /// <summary>The angle in degrees of the action at <paramref name="index"/> of <paramref name="count"/>.</summary>
    public static double AngleOf(int index, int count, double direction, double spread)
    {
        if (count <= 1)
        {
            return direction;
        }
        if (Math.Abs(spread) >= 360)
        {
            return direction + Math.Sign(spread) * 360.0 * index / count;
        }
        return direction - spread / 2 + spread * index / (count - 1);
    }

    /// <summary>Where the action at <paramref name="index"/> rests, relative to the trigger's center.</summary>
    public static Point PlaceOnArc(int index, int count, double radius, double direction, double spread)
    {
        var radians = AngleOf(index, count, direction, spread) * Math.PI / 180;
        return new Point(Math.Cos(radians) * radius, Math.Sin(radians) * radius);
    }

    /// <summary>The magnet's weight at <paramref name="distance"/> px from an action: 1 on it, 0 from <see cref="MagnetRadius"/> on.</summary>
    public static double Weight(double distance)
    {
        if (distance >= MagnetRadius)
        {
            return 0;
        }
        var t = distance / MagnetRadius;
        var w = 1 - t * t;
        return w * w;
    }

    /// <summary>An angle in degrees folded into (-180, 180].</summary>
    public static double Wrap(double degrees)
    {
        var folded = (degrees % 360 + 360) % 360;
        return folded > 180 ? folded - 360 : folded;
    }

    /// <summary>
    /// The action the pointer is aimed at, chosen by angle around the trigger: nothing inside the dead zone or beyond the
    /// reach, and nothing more than <paramref name="tolerance"/> degrees past either end of the arc; otherwise the action
    /// with the nearest angle. -1 when there is none.
    /// </summary>
    public static int TargetAt(Vector offset, int count, double radius, double direction, double spread, double deadZone,
                               double reach, double tolerance, Func<int, bool> targetable)
    {
        var distance = offset.Length;
        if (count == 0 || distance < deadZone || distance > radius + reach)
        {
            return -1;
        }
        var angle = Math.Atan2(offset.Y, offset.X) * 180 / Math.PI;
        if (Math.Abs(spread) < 360 && Math.Abs(Wrap(angle - direction)) > Math.Abs(spread) / 2 + tolerance)
        {
            return -1;
        }
        var best = -1;
        var bestGap = double.MaxValue;
        for (var i = 0; i < count; i++)
        {
            if (!targetable(i))
            {
                continue;
            }
            var gap = Math.Abs(Wrap(angle - AngleOf(i, count, direction, spread)));
            if (gap < bestGap - 1e-9)
            {
                bestGap = gap;
                best = i;
            }
        }
        return best;
    }
}

/// <summary>
/// A panel of no size: every child sits on the trigger's center at its own size and the menu moves it with a render
/// transform, so the actions, the lens and the pill never take room in the layout and opening never lays anything out.
/// </summary>
internal sealed class OrbitMenuOrbit : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(infinite);
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var center = new Point(finalSize.Width / 2, finalSize.Height / 2);
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            child.Arrange(new Rect(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The round trigger of an <see cref="OrbitMenu"/>. A press goes to the menu (it opens at once and follows the drag), so
/// the button only clicks from the keyboard and assistive technology. <c>:open</c> turns it into glass, <c>:holding</c>
/// presses it in, <c>:hover</c> is set for a mouse or a pen only.
/// </summary>
[PseudoClasses(OpenPseudoClass, HoldingPseudoClass, HoverPseudoClass)]
internal sealed class OrbitMenuTrigger : AvaloniaButton
{
    internal const string OpenPseudoClass = ":open";
    internal const string HoldingPseudoClass = ":holding";
    internal const string HoverPseudoClass = ":hover";

    private LazyFocusRing? _focusRing;
    private OrbitMenuGlyph? _glyph;

    internal OrbitMenu? Menu { get; set; }

    internal OrbitMenuGlyph? Glyph => _glyph;

    internal void SetOpen(bool open) => PseudoClasses.Set(OpenPseudoClass, open);

    internal void SetHolding(bool holding) => PseudoClasses.Set(HoldingPseudoClass, holding);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<OrbitMenuGlyph>("PART_Glyph");
        _focusRing ??= new LazyFocusRing(this, OrbitMenuTokens.Key(OrbitMenuTokenKind.TriggerFocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"), below: true);
        Menu?.OnTriggerTemplateApplied();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (Menu is { } menu)
        {
            // The menu takes the press: it opens now and follows the drag (the button never sees a click from a pointer).
            menu.BeginPress(e);
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TriggerPeer(this);

    private sealed class TriggerPeer(OrbitMenuTrigger owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private OrbitMenu? Menu => ((OrbitMenuTrigger)Owner).Menu;

        public ExpandCollapseState ExpandCollapseState =>
            Menu?.IsOpen == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand() => Menu?.Open();

        public void Collapse() => Menu?.Close();
    }
}

/// <summary>Which mark the trigger shows.</summary>
internal enum OrbitMenuGlyphState
{
    Dots,
    Close,
    Check
}

/// <summary>
/// The trigger's mark, three round-capped lines on a 24 × 24 grid that morph into each other: an ellipsis (three dots),
/// a close mark (turned a quarter turn as it forms) and a check. Code-driven because the shape interpolates point by point
/// from wherever an interrupted morph left it, which a transition on a geometry cannot do.
/// </summary>
internal sealed class OrbitMenuGlyph : Control
{
    // x1, y1, x2, y2 for three lines, then the stroke width and the rotation in degrees.
    private const int Values = 14;
    private static readonly double[] s_dots = [6, 12, 6.01, 12, 18, 12, 18.01, 12, 12, 12, 12.01, 12, 3.2, 0];
    private static readonly double[] s_close = [7, 7, 17, 17, 17, 7, 7, 17, 12, 12, 12.01, 12, 2, 90];
    private static readonly double[] s_check = [5.25, 12.5, 9.75, 17, 18.75, 7, 9.75, 17, 9.75, 17, 9.76, 17, 2.2, 0];

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<OrbitMenuGlyph, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<OrbitMenuGlyph, double>(nameof(Size), 24);

    private readonly double[] _from = (double[])s_dots.Clone();
    private readonly double[] _to = (double[])s_dots.Clone();
    private readonly double[] _now = (double[])s_dots.Clone();
    private MotionValue<double>? _progress;
    private Pen? _pen;

    static OrbitMenuGlyph()
    {
        AffectsMeasure<OrbitMenuGlyph>(SizeProperty);
        AffectsRender<OrbitMenuGlyph>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<OrbitMenuGlyph>(false);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    internal OrbitMenuGlyphState State { get; private set; }

    /// <summary>The current stroke width (tests read the morph's progress from it).</summary>
    internal double StrokeWidth => _now[12];

    /// <summary>The current rotation in degrees.</summary>
    internal double Rotation => _now[13];

    internal void SetState(OrbitMenuGlyphState state, bool animate)
    {
        if (state == State)
        {
            return;
        }
        State = state;
        Array.Copy(_now, _from, Values);
        Array.Copy(state switch
        {
            OrbitMenuGlyphState.Close => s_close,
            OrbitMenuGlyphState.Check => s_check,
            _ => s_dots
        }, _to, Values);
        if (!animate || !IsLoaded)
        {
            _progress?.Stop();
            Blend(1);
            return;
        }
        if (_progress is null)
        {
            _progress = new MotionValue<double>(this, 0, MotionVectors.Double);
            _progress.Changed += Blend;
        }
        _progress.Jump(0);
        _ = _progress.AnimateTo(1, MotionSpec.Spring(OrbitMenuGeometry.MorphSpring));
    }

    private void Blend(double t)
    {
        for (var i = 0; i < Values; i++)
        {
            _now[i] = _from[i] + (_to[i] - _from[i]) * t;
        }
        if (_pen is not null)
        {
            _pen.Thickness = _now[12];
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } ink)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_pen.Brush, ink))
        {
            _pen = new Pen(ink, _now[12], lineCap: PenLineCap.Round);
        }
        var scale = Size / 24;
        var transform = Matrix.CreateTranslation(-12, -12) * Matrix.CreateRotation(_now[13] * Math.PI / 180) *
                        Matrix.CreateTranslation(12, 12) * Matrix.CreateScale(scale, scale);
        using (context.PushTransform(transform))
        {
            for (var line = 0; line < 3; line++)
            {
                var o = line * 4;
                context.DrawLine(_pen, new Point(_now[o], _now[o + 1]), new Point(_now[o + 2], _now[o + 3]));
            }
        }
    }
}

/// <summary>The disc that glides under the targeted action; the menu places, scales and fades it.</summary>
[PseudoClasses(DangerPseudoClass)]
internal sealed class OrbitMenuLens : Control
{
    internal const string DangerPseudoClass = ":danger";

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<OrbitMenuLens, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<OrbitMenuLens, BoxShadows>(nameof(Shadow));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<OrbitMenuLens, double>(nameof(Size), 46);

    private readonly ScaleTransform _scale = new() { ScaleX = 0, ScaleY = 0 };
    private readonly TranslateTransform _translate = new();

    static OrbitMenuLens()
    {
        AffectsMeasure<OrbitMenuLens>(SizeProperty);
        AffectsRender<OrbitMenuLens>(FillProperty, ShadowProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<OrbitMenuLens>(false);
    }

    public OrbitMenuLens()
    {
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = new TransformGroup { Children = { _scale, _translate } };
        Opacity = 0;
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public BoxShadows Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    internal double VisualScale => _scale.ScaleX;

    internal Point VisualOffset => new(_translate.X, _translate.Y);

    internal void SetDanger(bool danger) => PseudoClasses.Set(DangerPseudoClass, danger);

    internal void Place(Point offset, double scale, double opacity)
    {
        _translate.X = offset.X;
        _translate.Y = offset.Y;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        Opacity = Math.Clamp(opacity, 0, 1);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(Fill, null, new RoundedRect(bounds, bounds.Width / 2), Shadow);
    }
}

/// <summary>
/// The thin arc behind the actions: two halves drawn out from the direction to the first and to the last action as the
/// menu opens, and drawn back as it folds.
/// </summary>
internal sealed class OrbitMenuTrack : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<OrbitMenuTrack, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<OrbitMenuTrack, double>(nameof(Thickness), 1.5);

    private double _radius;
    private double _direction;
    private double _halfSweep;
    private double _progress;
    private StreamGeometry? _geometry;
    private Pen? _pen;

    static OrbitMenuTrack()
    {
        AffectsRender<OrbitMenuTrack>(StrokeProperty, ThicknessProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<OrbitMenuTrack>(false);
    }

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    internal double Progress => _progress;

    /// <summary>The arc: its radius, the direction it opens toward and half its sweep (degrees, signed).</summary>
    internal void SetArc(double radius, double direction, double halfSweep)
    {
        if (radius == _radius && direction == _direction && halfSweep == _halfSweep)
        {
            return;
        }
        _radius = radius;
        _direction = direction;
        _halfSweep = halfSweep;
        _geometry = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    internal void SetProgress(double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        if (Math.Abs(progress - _progress) < 1e-5)
        {
            return;
        }
        _progress = progress;
        _geometry = null;
        Opacity = Math.Min(1, 1.6 * progress);
        IsVisible = progress > 0;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var extent = 2 * (_radius + Thickness);
        return new Size(extent, extent);
    }

    public override void Render(DrawingContext context)
    {
        if (Stroke is not { } stroke || _progress <= 0 || _radius <= 0 || Math.Abs(_halfSweep) < 1e-3)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_pen.Brush, stroke) || Math.Abs(_pen.Thickness - Thickness) > 1e-6)
        {
            _pen = new Pen(stroke, Thickness, lineCap: PenLineCap.Round);
        }
        _geometry ??= Build(Bounds.Size);
        context.DrawGeometry(null, _pen, _geometry);
    }

    private StreamGeometry Build(Size size)
    {
        var center = new Point(size.Width / 2, size.Height / 2);
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        for (var side = -1; side <= 1; side += 2)
        {
            var sweep = _halfSweep * side * _progress;
            var start = At(center, _direction);
            g.BeginFigure(start, false);
            // ArcTo cannot draw a half circle or more in one go reliably; split long sweeps.
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / 90));
            for (var s = 1; s <= steps; s++)
            {
                var end = At(center, _direction + sweep * s / steps);
                g.ArcTo(end, new Size(_radius, _radius), 0, false, sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
            }
            g.EndFigure(false);
        }
        return geometry;
    }

    private Point At(Point center, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + Math.Cos(radians) * _radius, center.Y + Math.Sin(radians) * _radius);
    }
}

/// <summary>
/// The round area around an open menu that follows a hovering pointer, so the arc can be aimed from between and beyond
/// the actions; a press on it (not on an action) folds the menu.
/// </summary>
internal sealed class OrbitMenuField : Control, ICustomHitTest
{
    internal OrbitMenu? Menu { get; set; }

    public bool HitTest(Point point)
    {
        var radius = Bounds.Width / 2;
        return new Vector(point.X - radius, point.Y - radius).Length <= radius;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.DrawEllipse(Brushes.Transparent, null, bounds);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && Menu is { } menu)
        {
            e.Handled = true;
            menu.Close();
        }
    }
}

/// <summary>
/// The glass label pill: names the target, says how to cancel, or confirms what fired. Its width springs to fit the text
/// and the text swaps with a soft blur; it rises and grows in as it appears.
/// </summary>
internal sealed class OrbitMenuPill : Border
{
    private readonly ArcContentSwap _swap = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Mode = ContentSwapMode.None
    };

    private readonly ScaleTransform _scale = new() { ScaleX = 0.82, ScaleY = 0.82 };
    private readonly TranslateTransform _translate = new();
    private MotionValue<double>? _presence;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _width;
    private double _restWidth = double.NaN;
    private double _heading = -Math.PI / 2;
    private Size _arranged;
    private double _near = 36;
    private double _arcInner = 73;
    private double _arcOuter = 135;
    private string? _text;
    private bool _done;
    private bool _shown;
    private double _blur;

    public OrbitMenuPill()
    {
        Child = _swap;
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = new TransformGroup { Children = { _scale, _translate } };
        Opacity = 0;
        IsHitTestVisible = false;
    }

    internal string? Text => _text;

    internal bool IsDone => _done;

    internal bool IsShown => _shown;

    internal double Spacing { get; set; } = 6;

    internal double GlyphSize { get; set; } = 15;

    /// <summary>
    /// Where the pill goes: along <paramref name="heading"/> (radians), just clear of the trigger
    /// (<paramref name="near"/> from its center) when it fits before the arc's inner edge (<paramref name="arcInner"/>),
    /// as it does for an arc that opens up or down; otherwise just beyond the arc (<paramref name="arcOuter"/>).
    /// </summary>
    internal void SetPlacement(double heading, double near, double arcInner, double arcOuter)
    {
        _heading = heading;
        _near = near;
        _arcInner = arcInner;
        _arcOuter = arcOuter;
        Apply();
    }

    /// <summary>The pill's center relative to the trigger's center.</summary>
    internal Point Anchor
    {
        get
        {
            var cos = Math.Cos(_heading);
            var sin = Math.Sin(_heading);
            var size = _arranged;
            var height = size.Height > 0 ? size.Height : 32;
            // The choice uses a nominal width so a longer label never moves the pill to the other place.
            var fits = _near + 2 * (60 * Math.Abs(cos) + height / 2 * Math.Abs(sin)) <= _arcInner;
            var extent = size.Width / 2 * Math.Abs(cos) + height / 2 * Math.Abs(sin);
            var distance = (fits ? _near : _arcOuter) + extent;
            return new Point(cos * distance, sin * distance);
        }
    }

    /// <summary>Shows <paramref name="text"/> (with a check when <paramref name="done"/>), appearing first if hidden.</summary>
    internal void Show(string text, bool done)
    {
        var changed = text != _text || done != _done;
        _text = text;
        _done = done;
        EnsureMotion();
        var visible = _fade!.Value > 0.01;
        if (!visible)
        {
            // Out of sight: the new text and its width are in place before the pill rises in.
            _swap.Mode = ContentSwapMode.None;
            _swap.Content = Row(text, done);
            _shown = true;
            JumpWidth();
            _presence!.Jump(0);
        }
        else if (changed)
        {
            _shown = true;
            _swap.Mode = ArcMotion.GetEffectiveLevel(this) switch
            {
                ArcMotionLevel.Full => ContentSwapMode.BlurSwap,
                ArcMotionLevel.Reduced => ContentSwapMode.Crossfade,
                _ => ContentSwapMode.None
            };
            _swap.Content = Row(text, done);
        }
        _shown = true;
        if (_fade.Target < 1)
        {
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        if (_presence!.Target < 1)
        {
            _ = _presence.AnimateTo(1, MotionSpec.Spring(OrbitMenuGeometry.PillSpring));
        }
    }

    internal void Hide()
    {
        if (!_shown)
        {
            return;
        }
        _shown = false;
        EnsureMotion();
        var exit = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        _ = _fade!.AnimateTo(0, exit);
        if (ArcMotion.IsFull(this))
        {
            _ = _presence!.AnimateTo(0, exit);
        }
        else
        {
            _presence!.Jump(0);
        }
    }

    private void EnsureMotion()
    {
        if (_presence is not null)
        {
            return;
        }
        _presence = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fade = new MotionValue<double>(this, 0, MotionVectors.Double);
        _presence.Changed += _ => Apply();
        _fade.Changed += _ => Apply();
    }

    private void Apply()
    {
        var presence = _presence?.Value ?? 0;
        var fade = _fade?.Value ?? 0;
        var full = ArcMotion.IsFull(this);
        var scale = full ? 0.82 + 0.18 * presence : 1;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        var anchor = Anchor;
        _translate.X = anchor.X;
        _translate.Y = anchor.Y + (full ? 12 * (1 - presence) : 0);
        Opacity = Math.Clamp(fade, 0, 1);
        var blur = full ? OrbitMenuGeometry.LaunchBlur * Math.Clamp(1 - fade, 0, 1) : 0;
        if (Math.Abs(blur - _blur) > 0.05 || (blur == 0 && _blur != 0))
        {
            _blur = blur < 0.05 ? 0 : blur;
            Effect = ArcBlur.Get(_blur);
        }
    }

    private Control Row(string text, bool done)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Medium };
        ArcTokens.Bind(label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(label, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        if (!done)
        {
            return label;
        }
        var check = new ArcGlyph { Data = ArcIcons.Check, Size = GlyphSize, StrokeThickness = 2.25, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(check, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = Spacing, Children = { check, label } };
    }

    private void JumpWidth()
    {
        _restWidth = double.NaN;
        _width?.Stop();
        _width = null;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = base.MeasureOverride(availableSize.WithWidth(double.PositiveInfinity));
        var target = desired.Width;
        if (double.IsNaN(_restWidth) || !_shown || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _restWidth = target;
            if (_width is not null)
            {
                _width.Jump(target);
            }
        }
        else if (Math.Abs(target - (_width?.Target ?? _restWidth)) > 0.25)
        {
            if (_width is null)
            {
                _width = new MotionValue<double>(this, _restWidth, MotionVectors.Double);
                _width.Changed += _ => InvalidateMeasure();
            }
            _ = _width.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        var width = _width?.Value ?? _restWidth;
        return new Size(width, desired.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // While the width springs the text keeps its own width, centered: it overflows evenly instead of sliding.
        if (Child is { } child)
        {
            var padding = Padding;
            var edge = BorderThickness;
            var innerWidth = Math.Max(0, finalSize.Width - padding.Left - padding.Right - edge.Left - edge.Right);
            var innerHeight = Math.Max(0, finalSize.Height - padding.Top - padding.Bottom - edge.Top - edge.Bottom);
            var width = Math.Max(child.DesiredSize.Width, innerWidth);
            child.Arrange(new Rect((finalSize.Width - width) / 2, padding.Top + edge.Top, width, innerHeight));
        }
        // The anchor depends on the width (beyond the arc); keep it in step as the width springs.
        _arranged = finalSize;
        Apply();
        return finalSize;
    }
}
