using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Resolved colors and font for drawing stepper markers and connectors (shared by all items).</summary>
internal sealed class StepperPalette
{
    public int Version { get; set; }
    public IBrush Surface { get; set; } = Brushes.White;
    public IBrush BorderStrong { get; set; } = Brushes.Gray;
    public IBrush Track { get; set; } = Brushes.LightGray;
    public IBrush TextMuted { get; set; } = Brushes.Gray;
    public IBrush Foreground { get; set; } = Brushes.Black;
    public IBrush Accent { get; set; } = Brushes.Black;
    public IBrush AccentStrong { get; set; } = Brushes.Black;
    public IBrush AccentForeground { get; set; } = Brushes.White;
    public IBrush AccentSubtle { get; set; } = Brushes.LightGray;
    public IBrush Danger { get; set; } = Brushes.Red;
    public IBrush ErrorDisc { get; set; } = Brushes.MistyRose;
    public IBrush ErrorDiscHover { get; set; } = Brushes.MistyRose;
    public IBrush ErrorRing { get; set; } = Brushes.MistyRose;
    public IBrush FocusRing { get; set; } = Brushes.Blue;
    public FontFamily FontFamily { get; set; } = FontFamily.Default;
    public FontWeight NumberWeight { get; set; } = FontWeight.Medium;
    public double NumberFontSize { get; set; } = 12;
}

internal enum StepperGlyphKind
{
    Number,
    Check,
    Error
}

/// <summary>
/// The 28px marker of a stepper step, drawn directly: a halo ring that grows out from behind the disc for the current
/// step (snappy, P26), the disc with its inset rim in the state color (colors cross over 240ms), and the glyph. A new
/// glyph pops in from 0.5 through a subtle blur while its stroke draws (P15, P16); the old one shrinks away.
/// </summary>
internal sealed class StepperMarker : Control
{
    private sealed class Look
    {
        public IBrush? Disc { get; init; }
        public IBrush? Rim { get; init; }
        public double RimWidth { get; init; }
        public IBrush? Glyph { get; init; }
        public IBrush? Ring { get; init; }
    }

    private sealed class Glyph
    {
        public StepperGlyphKind Kind { get; init; }
        public int Number { get; init; }
        public FormattedText? Text { get; set; }
        public IBrush? LastBrush { get; set; }
        public IBrush? LeavingBrush { get; set; }
        public MotionValue<double>? Fade { get; set; }
        public MotionValue<double>? Scale { get; set; }
        public MotionValue<double>? Stroke { get; set; }
        public MotionValue<double>? Dot { get; set; }
        public MotionValue<double>? Exit { get; set; }
        public bool Full { get; set; }
    }

    internal static readonly Geometry CheckPath = ArcIcons.Parse("M5.5 12.5l4.25 4.25L18.5 8");
    internal static readonly Geometry AlertPath = ArcIcons.Parse("M12 6.75v6.5");
    private const double DiscSize = 28;
    private const double Halo = 4;

    private StepperPalette? _palette;
    private Look? _from;
    private Look? _to;
    private MotionValue<double>? _lookT;
    private Glyph? _glyph;
    private readonly List<Glyph> _leaving = new();
    private MotionValue<double>? _ringScale;
    private MotionValue<double>? _ringFade;
    private bool _ringFull = true;
    private MotionValue<double>? _press;
    private bool _current;
    private IPen? _glyphPen;
    private IBrush? _glyphPenBrush;

    public StepperMarker()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    internal StepperGlyphKind Kind => _glyph?.Kind ?? StepperGlyphKind.Number;

    internal bool IsRingShown => _current;

    protected override Size MeasureOverride(Size availableSize) => new(DiscSize, DiscSize);

    /// <summary>Applies a state. <paramref name="glyphDelay"/> and <paramref name="ringDelay"/> are in seconds.</summary>
    public void Apply(StepperPalette palette, int number, StepperGlyphKind kind, StepperStepStatus status, bool current,
                      bool hover, double glyphDelay, double ringDelay, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        animate = animate && IsLoaded && level != ArcMotionLevel.Off;
        var full = animate && level == ArcMotionLevel.Full;
        var paletteChanged = !ReferenceEquals(_palette, palette) || palette.Version != _paletteVersion;
        _palette = palette;
        _paletteVersion = palette.Version;
        if (paletteChanged && _glyph is not null)
        {
            _glyph.Text = null;
            _glyphPen = null;
        }

        var look = LookFor(palette, status, current, hover);
        if (_to is null || paletteChanged || !animate || level != ArcMotionLevel.Full)
        {
            _from = look;
            _to = look;
            _lookT?.Jump(1);
        }
        else if (!SameLook(_to, look))
        {
            _from = _to;
            _to = look;
            _lookT ??= CreateValue(1);
            _lookT.Jump(0);
            _ = _lookT.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }

        if (_glyph is null || _glyph.Kind != kind || _glyph.Number != number)
        {
            SwapGlyph(kind, number, glyphDelay, animate, full);
        }

        if (current != _current || _ringFade is null)
        {
            var first = _ringFade is null;
            _current = current;
            _ringScale ??= CreateValue(current ? 1 : 0.6);
            _ringFade ??= CreateValue(current ? 1 : 0);
            if (first || !animate)
            {
                _ringScale.Jump(current ? 1 : 0.6);
                _ringFade.Jump(current ? 1 : 0);
            }
            else if (current)
            {
                _ringFull = full;
                var delay = TimeSpan.FromSeconds(full ? ringDelay : 0);
                _ringScale.Jump(full ? 0.6 : 1);
                _ = _ringScale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy, delay));
                _ = _ringFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, delay));
            }
            else
            {
                var exit = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
                if (full)
                {
                    _ = _ringScale.AnimateTo(0.6, exit);
                }
                _ = _ringFade.AnimateTo(0, exit);
            }
        }
        InvalidateVisual();
    }

    private int _paletteVersion = -1;

    /// <summary>Press feedback on clickable steps: the disc scales to 0.92 on Arc's CSS spring curve.</summary>
    public void SetPressed(bool pressed)
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _press ??= CreateValue(1);
        _ = _press.AnimateTo(pressed ? 0.92 : 1, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
    }

    private static Look LookFor(StepperPalette p, StepperStepStatus status, bool current, bool hover) => status switch
    {
        StepperStepStatus.Complete => new Look
        {
            Disc = hover ? p.AccentStrong : p.Accent, Rim = hover ? p.AccentStrong : p.Accent, RimWidth = 1,
            Glyph = p.AccentForeground, Ring = p.AccentSubtle
        },
        StepperStepStatus.Current => new Look { Disc = p.Surface, Rim = p.Accent, RimWidth = 1.5, Glyph = p.Foreground, Ring = p.AccentSubtle },
        StepperStepStatus.Error => new Look
        {
            Disc = hover ? p.ErrorDiscHover : p.ErrorDisc, Rim = p.Danger, RimWidth = 1.5, Glyph = p.Danger, Ring = p.ErrorRing
        },
        _ => new Look { Disc = p.Surface, Rim = p.BorderStrong, RimWidth = 1, Glyph = p.TextMuted, Ring = p.AccentSubtle }
    };

    private static bool SameLook(Look a, Look b) => ReferenceEquals(a.Disc, b.Disc) && ReferenceEquals(a.Rim, b.Rim) &&
                                                    Math.Abs(a.RimWidth - b.RimWidth) < 0.01 && ReferenceEquals(a.Glyph, b.Glyph) &&
                                                    ReferenceEquals(a.Ring, b.Ring);

    private void SwapGlyph(StepperGlyphKind kind, int number, double delay, bool animate, bool full)
    {
        if (_glyph is not null)
        {
            if (animate)
            {
                var outgoing = _glyph;
                outgoing.LeavingBrush = _to?.Glyph;
                outgoing.Full = full;
                outgoing.Exit = CreateValue(0);
                _leaving.Add(outgoing);
                outgoing.Exit.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)).ContinueWith(_ =>
                    Dispatcher.Post(() =>
                    {
                        _leaving.Remove(outgoing);
                        InvalidateVisual();
                    }), TaskScheduler.Default);
            }
        }
        var glyph = new Glyph { Kind = kind, Number = number, Full = full };
        glyph.Fade = CreateValue(animate ? 0 : 1);
        glyph.Scale = CreateValue(animate && full ? 0.5 : 1);
        glyph.Stroke = CreateValue(animate && full ? 0 : 1);
        glyph.Dot = CreateValue(animate && full ? 0 : 1);
        if (animate)
        {
            var wait = TimeSpan.FromSeconds(full ? delay : 0);
            _ = glyph.Fade.AnimateTo(1, MotionSpec.Tween(full ? ArcMotionPresets.Fast : ArcMotionPresets.Instant, ArcEasings.Standard, wait));
            if (full)
            {
                _ = glyph.Scale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy, wait));
                _ = glyph.Stroke.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, wait + TimeSpan.FromSeconds(0.04)));
                _ = glyph.Dot.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy, wait + TimeSpan.FromSeconds(0.16)));
            }
        }
        _glyph = glyph;
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public override void Render(DrawingContext context)
    {
        var palette = _palette;
        if (palette is null || _to is null)
        {
            return;
        }
        var center = new Point(DiscSize / 2, DiscSize / 2);
        var t = Math.Clamp(_lookT?.Value ?? 1, 0, 1);
        var from = _from ?? _to;

        // Halo ring behind the disc.
        var ringFade = Math.Clamp(_ringFade?.Value ?? 0, 0, 1);
        if (ringFade > 0.001)
        {
            var scale = _ringFull ? _ringScale?.Value ?? 1 : 1;
            var r = (DiscSize / 2 + Halo) * scale;
            using (context.PushOpacity(ringFade))
            {
                DrawDiscCross(context, from.Ring, _to.Ring, t, center, r);
            }
        }

        var press = _press?.Value ?? 1;
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(press, press) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            var radius = DiscSize / 2;
            DrawDiscCross(context, from.Disc, _to.Disc, t, center, radius);
            DrawRim(context, from, center, 1 - t);
            DrawRim(context, _to, center, t);
            foreach (var leaving in _leaving)
            {
                var q = Math.Clamp(leaving.Exit?.Value ?? 1, 0, 1);
                DrawGlyph(context, leaving, leaving.LeavingBrush, 1 - q, leaving.Full ? 1 - 0.5 * q : 1, leaving.Full ? 2 * q : 0, 1, 1);
            }
            if (_glyph is { } glyph)
            {
                var fade = Math.Clamp(glyph.Fade?.Value ?? 1, 0, 1);
                var scale = glyph.Scale?.Value ?? 1;
                var blur = glyph.Full ? 2 * (1 - fade) : 0;
                var stroke = Math.Clamp(glyph.Stroke?.Value ?? 1, 0, 1);
                var dot = Math.Max(0, glyph.Dot?.Value ?? 1);
                if (t < 1 && !ReferenceEquals(from.Glyph, _to.Glyph))
                {
                    DrawGlyph(context, glyph, from.Glyph, fade * (1 - t), scale, blur, stroke, dot);
                    DrawGlyph(context, glyph, _to.Glyph, fade * t, scale, blur, stroke, dot);
                }
                else
                {
                    DrawGlyph(context, glyph, _to.Glyph, fade, scale, blur, stroke, dot);
                }
            }
        }
    }

    /// <summary>Draws a filled circle crossing from one color to the next (old under, new over at <paramref name="t"/>).</summary>
    private static void DrawDiscCross(DrawingContext context, IBrush? from, IBrush? to, double t, Point center, double radius)
    {
        if (from is not null && (t < 1 || ReferenceEquals(from, to)))
        {
            context.DrawEllipse(from, null, center, radius, radius);
        }
        if (to is not null && !ReferenceEquals(from, to) && t > 0)
        {
            using (context.PushOpacity(t))
            {
                context.DrawEllipse(to, null, center, radius, radius);
            }
        }
    }

    private readonly Dictionary<(IBrush, double), IPen> _rimPens = new();

    private void DrawRim(DrawingContext context, Look look, Point center, double opacity)
    {
        if (look.Rim is null || opacity <= 0.001)
        {
            return;
        }
        if (!_rimPens.TryGetValue((look.Rim, look.RimWidth), out var pen))
        {
            pen = new Pen(look.Rim, look.RimWidth);
            _rimPens[(look.Rim, look.RimWidth)] = pen;
        }
        var r = DiscSize / 2 - look.RimWidth / 2;
        using (context.PushOpacity(opacity))
        {
            context.DrawEllipse(null, pen, center, r, r);
        }
    }

    private void DrawGlyph(DrawingContext context, Glyph glyph, IBrush? brush, double opacity, double scale, double blur,
                           double stroke, double dot)
    {
        if (brush is null || opacity <= 0.001 || _palette is null)
        {
            return;
        }
        var center = new Point(DiscSize / 2, DiscSize / 2);
        var box = new Rect(center.X - 7, center.Y - 7, 14, 14);
        using (context.PushOpacity(opacity))
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            var effect = ArcBlur.Get(blur);
            using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, box.Inflate(4)))
            {
                if (glyph.Kind == StepperGlyphKind.Number)
                {
                    glyph.Text ??= new FormattedText(glyph.Number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, new Typeface(_palette.FontFamily, FontStyle.Normal, _palette.NumberWeight),
                        _palette.NumberFontSize, brush);
                    if (!ReferenceEquals(glyph.LastBrush, brush))
                    {
                        glyph.Text.SetForegroundBrush(brush);
                        glyph.LastBrush = brush;
                    }
                    context.DrawText(glyph.Text, new Point(center.X - glyph.Text.Width / 2, center.Y - glyph.Text.Height / 2));
                    return;
                }
                if (!ReferenceEquals(_glyphPenBrush, brush) || _glyphPen is null)
                {
                    _glyphPenBrush = brush;
                    _glyphPen = new Pen(brush, 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                }
                using (context.PushTransform(Matrix.CreateScale(14 / 24.0, 14 / 24.0) * Matrix.CreateTranslation(box.X, box.Y)))
                {
                    var path = glyph.Kind == StepperGlyphKind.Check ? CheckPath : AlertPath;
                    DrawStroke(context, path, _glyphPen, stroke);
                    if (glyph.Kind == StepperGlyphKind.Error && dot > 0.001)
                    {
                        var r = 1.4 * dot;
                        context.DrawEllipse(brush, null, new Point(12, 17.4), r, r);
                    }
                }
            }
        }
    }

    private static void DrawStroke(DrawingContext context, Geometry path, IPen pen, double progress)
    {
        if (progress >= 0.999)
        {
            context.DrawGeometry(null, pen, path);
            return;
        }
        if (progress <= 0.001)
        {
            return;
        }
        var length = path.ContourLength;
        if (length > 0 && path.TryGetSegment(0, length * progress, true, out var segment) && segment is not null)
        {
            context.DrawGeometry(null, pen, segment);
        }
    }
}
