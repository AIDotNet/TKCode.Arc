using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The status colours (notes 3.2): an operational bar is Success mixed 62% into the surface, degraded is Warning, an outage
/// Danger; the header icon's disc is the status colour 14% into the surface; a badge's edge is 27% of it into the border
/// and its fill 11% into the surface.
/// </summary>
internal static class UptimeColors
{
    /// <summary>The bar and legend colour of a status.</summary>
    public static Color Bar(AnalyticsPalette p, ServiceStatus status) => status switch
    {
        ServiceStatus.Operational => AnalyticsColors.Mix(p.Success, 0.62, p.Surface),
        ServiceStatus.Degraded => p.Warning,
        _ => p.Danger
    };

    /// <summary>The text and icon colour of a status (Success, Warning, Danger).</summary>
    public static Color Ink(AnalyticsPalette p, ServiceStatus status) => status switch
    {
        ServiceStatus.Operational => p.Success,
        ServiceStatus.Degraded => p.Warning,
        _ => p.Danger
    };

    /// <summary>The icon disc: the ink 14% into the surface.</summary>
    public static Color Disc(AnalyticsPalette p, ServiceStatus status) => AnalyticsColors.Mix(Ink(p, status), 0.14, p.Surface);

    /// <summary>A badge's fill: the ink 11% into the surface.</summary>
    public static Color BadgeFill(AnalyticsPalette p, ServiceStatus status) => AnalyticsColors.Mix(Ink(p, status), 0.11, p.Surface);

    /// <summary>A badge's edge: the ink 27% into the border.</summary>
    public static Color BadgeEdge(AnalyticsPalette p, ServiceStatus status) => AnalyticsColors.Mix(Ink(p, status), 0.27, p.Border);

    /// <summary>The timeline dot of a tone.</summary>
    public static Color Tone(AnalyticsPalette p, IncidentTone tone) => tone switch
    {
        IncidentTone.Success => p.Success,
        IncidentTone.Warning => p.Warning,
        IncidentTone.Danger => p.Danger,
        _ => p.Foreground
    };
}

/// <summary>Base for the small self-drawn parts: a palette that refreshes with the theme.</summary>
internal abstract class UptimeVisual : Control
{
    protected readonly AnalyticsPalette Palette = new();
    protected readonly AnalyticsTextCache Text = new(128);

    protected UptimeVisual()
    {
        IsHitTestVisible = false;
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>Reads the palette; call at the start of <c>Render</c>.</summary>
    protected void EnsurePalette()
    {
        if (!Palette.IsReady)
        {
            Palette.Refresh(this);
        }
    }

    private void RefreshPalette()
    {
        Text.Clear();
        if (Palette.IsReady)
        {
            Palette.Refresh(this);
        }
        InvalidateVisual();
    }
}

/// <summary>The legend mark: three bottom-aligned bars of one status shape, 8 wide and 14 high (14 / 9 / 5 tall).</summary>
internal sealed class UptimeStatusMark : UptimeVisual
{
    /// <summary>The status the mark shows.</summary>
    public static readonly StyledProperty<ServiceStatus> StatusProperty =
        AvaloniaProperty.Register<UptimeStatusMark, ServiceStatus>(nameof(Status));

    static UptimeStatusMark()
    {
        AffectsRender<UptimeStatusMark>(StatusProperty);
        AffectsMeasure<UptimeStatusMark>(StatusProperty);
    }

    public ServiceStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(8, 14);

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        var h = Status switch { ServiceStatus.Operational => 14.0, ServiceStatus.Degraded => 9.0, _ => 5.0 };
        context.DrawRectangle(Palette.Brush(UptimeColors.Bar(Palette, Status)), null, new RoundedRect(new Rect(0, 14 - h, 8, h), 1.5));
    }
}

/// <summary>
/// A status badge: a 22 high pill with a 1px edge and a tinted fill, a 12px icon and 11px 500 text in the status colour
/// (notes 3.2). Its colours animate over 240ms (Standard) when the status changes.
/// </summary>
internal sealed class UptimeBadge : UptimeVisual
{
    /// <summary>The status that tints it.</summary>
    public static readonly StyledProperty<ServiceStatus> StatusProperty =
        AvaloniaProperty.Register<UptimeBadge, ServiceStatus>(nameof(Status), ServiceStatus.Degraded);

    /// <summary>The label.</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<UptimeBadge, string?>(nameof(Label));

    /// <summary>The icon (Lucide geometry; a triangle alert when null).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<UptimeBadge, Geometry?>(nameof(Icon));

    private readonly LiveLoop _loop;
    private AnalyticsTween _mix = new(1);
    private ServiceStatus _from;

    static UptimeBadge()
    {
        AffectsRender<UptimeBadge>(StatusProperty, LabelProperty, IconProperty);
        AffectsMeasure<UptimeBadge>(LabelProperty);
    }

    public UptimeBadge()
    {
        _loop = new LiveLoop(this, dt =>
        {
            var running = _mix.Advance(dt);
            InvalidateVisual();
            return running;
        });
        _from = Status;
    }

    public ServiceStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            _from = change.GetOldValue<ServiceStatus>();
            if (this.IsAttachedToVisualTree() && ArcMotion.IsFull(this))
            {
                _mix.Jump(0);
                _mix.Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
                _loop.Start();
            }
            else
            {
                _mix.Jump(1);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsurePalette();
        var typeface = new Typeface(Palette.BodyFont, FontStyle.Normal, Palette.Medium);
        var text = Text.Get(Label ?? string.Empty, typeface, 11);
        return new Size(8 + 12 + 4 + Math.Ceiling(text.Width) + 8, 22);
    }

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        var p = Palette;
        var t = Math.Clamp(_mix.Value, 0, 1);
        var fill = AnalyticsColors.Lerp(UptimeColors.BadgeFill(p, _from), UptimeColors.BadgeFill(p, Status), t);
        var edge = AnalyticsColors.Lerp(UptimeColors.BadgeEdge(p, _from), UptimeColors.BadgeEdge(p, Status), t);
        var ink = AnalyticsColors.Lerp(UptimeColors.Ink(p, _from), UptimeColors.Ink(p, Status), t);
        var rect = new Rect(Bounds.Size);
        context.DrawRectangle(p.Brush(fill), p.Pen(edge, 1), new RoundedRect(rect.Deflate(0.5), rect.Height / 2));
        var pen = p.Pen(ink, 2);
        var icon = Icon ?? ArcIcons.TriangleAlert;
        using (context.PushTransform(Matrix.CreateScale(0.5, 0.5) * Matrix.CreateTranslation(8, (rect.Height - 12) / 2)))
        {
            context.DrawGeometry(null, pen, icon);
        }
        var typeface = new Typeface(p.BodyFont, FontStyle.Normal, p.Medium);
        var text = Text.Get(Label ?? string.Empty, typeface, 11);
        text.Draw(context, p.Brush(ink), new Point(8 + 12 + 4, (rect.Height - text.Height) / 2));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TextPeer(this);

    private sealed class TextPeer(UptimeBadge owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => owner.Label;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The header's status icon (28 square, 20-unit view box): a disc in the status colour 14% into the surface and two round
/// 1.3 lines in the status colour, a tick when operational and an exclamation mark otherwise. The line ends spring
/// (k 160, c 26) between the shapes (the exclamation's dot is a zero-length round-capped line) and the colours fade over
/// 240ms (Standard) (notes 3.1 and 6.6).
/// </summary>
/// <remarks>Why code-driven: eight coordinates inside one drawing move on one spring; a ControlTheme cannot animate path data.</remarks>
internal sealed class UptimeStatusIcon : UptimeVisual
{
    /// <summary>The status.</summary>
    public static readonly StyledProperty<ServiceStatus> StatusProperty =
        AvaloniaProperty.Register<UptimeStatusIcon, ServiceStatus>(nameof(Status));

    private static readonly double[] s_tick = [6.1, 10.3, 8.8, 13, 8.8, 13, 14, 7.4];
    private static readonly double[] s_mark = [10, 14.1, 10, 14.1, 10, 5.8, 10, 10.9];
    private static readonly SpringSpec s_spring = SpringSpec.FromPhysical(160, 26);

    private readonly AnalyticsSpringArray _lines;
    private AnalyticsTween _mix = new(1);
    private ServiceStatus _from;
    private readonly LiveLoop _loop;

    static UptimeStatusIcon()
    {
        AffectsRender<UptimeStatusIcon>(StatusProperty);
    }

    public UptimeStatusIcon()
    {
        _lines = new AnalyticsSpringArray(this, s_spring);
        _lines.Jump(s_tick);
        _lines.Changed += InvalidateVisual;
        _loop = new LiveLoop(this, dt =>
        {
            var running = _mix.Advance(dt);
            InvalidateVisual();
            return running;
        });
    }

    public ServiceStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>The eight line coordinates now (tests).</summary>
    internal AnalyticsSpringArray Lines => _lines;

    protected override Size MeasureOverride(Size availableSize) => new(28, 28);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != StatusProperty)
        {
            return;
        }
        var old = change.GetOldValue<ServiceStatus>();
        var now = change.GetNewValue<ServiceStatus>();
        _from = old;
        var shape = now == ServiceStatus.Operational ? s_tick : s_mark;
        var animate = this.IsAttachedToVisualTree() && ArcMotion.IsFull(this);
        if (animate)
        {
            _lines.SetTargets(shape);
            _mix.Jump(0);
            _mix.Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
            _loop.Start();
        }
        else
        {
            _lines.Jump(shape);
            _mix.Jump(1);
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        var p = Palette;
        var t = Math.Clamp(_mix.Value, 0, 1);
        var disc = AnalyticsColors.Lerp(UptimeColors.Disc(p, _from), UptimeColors.Disc(p, Status), t);
        var ink = AnalyticsColors.Lerp(UptimeColors.Ink(p, _from), UptimeColors.Ink(p, Status), t);
        const double scale = 1.4;
        context.DrawEllipse(p.Brush(disc), null, new Point(14, 14), 10 * scale, 10 * scale);
        var pen = p.Pen(ink, 1.3 * scale, cap: PenLineCap.Round);
        var v = _lines.Values;
        if (v.Length < 8)
        {
            return;
        }
        // A zero-length line has no direction: nudge it so the round caps still make a dot.
        context.DrawLine(pen, new Point(v[0] * scale, v[1] * scale), new Point(v[2] * scale, v[3] * scale + 0.001));
        context.DrawLine(pen, new Point(v[4] * scale, v[5] * scale), new Point(v[6] * scale, v[7] * scale + 0.001));
    }
}

/// <summary>The sample of a series in the KPI row: a 14 wide line (p95: 2px Accent, p50: 2px TextMuted, target: 1px dashed).</summary>
internal sealed class UptimeKeyMark : UptimeVisual
{
    /// <summary>Which series the mark stands for.</summary>
    public static readonly StyledProperty<UptimeKeyKind> KindProperty =
        AvaloniaProperty.Register<UptimeKeyMark, UptimeKeyKind>(nameof(Kind));

    static UptimeKeyMark()
    {
        AffectsRender<UptimeKeyMark>(KindProperty);
    }

    public UptimeKeyKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(14, 8);

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        var p = Palette;
        var y = 4.0;
        switch (Kind)
        {
            case UptimeKeyKind.P95:
                context.DrawLine(p.Pen(p.Accent, 2, cap: PenLineCap.Round), new Point(1, y), new Point(13, y));
                break;
            case UptimeKeyKind.P50:
                context.DrawLine(p.Pen(p.TextMuted, 2, cap: PenLineCap.Round), new Point(1, y), new Point(13, y));
                break;
            default:
                context.DrawLine(p.Pen(p.TextMuted, 1, dash: 3, gap: 2, cap: PenLineCap.Flat), new Point(0, y + 0.5), new Point(14, y + 0.5));
                break;
        }
    }
}

/// <summary>The series a <see cref="UptimeKeyMark"/> stands for.</summary>
internal enum UptimeKeyKind
{
    P95,
    P50,
    Target
}

/// <summary>A 1px horizontal rule in BorderSubtle (the line above every incident but the first).</summary>
internal sealed class UptimeRule : UptimeVisual
{
    protected override Size MeasureOverride(Size availableSize) => new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, 1);

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        context.FillRectangle(Palette.Brush(Palette.BorderSubtle), new Rect(0, 0, Bounds.Width, 1));
    }
}
