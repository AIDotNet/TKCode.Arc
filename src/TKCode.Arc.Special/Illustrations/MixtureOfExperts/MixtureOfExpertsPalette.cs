using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The colors, brushes, pens and fonts every part of a <see cref="MixtureOfExperts"/> draws with, captured from the
/// control's ink properties (ArcDesign tokens set in its theme) and the first / second choice colors, and rebuilt only
/// when one of them changes. Derived colors follow the measured mixes: a lit tile is 9% of its choice color over the
/// surface with a 55% border, lines and capacity bars use the choice color at 70%, and so on.
/// </summary>
internal sealed class MixtureOfExpertsPalette
{
    private readonly Dictionary<(Color Color, double Thickness, bool Dashed), ImmutablePen> _pens = new();
    private readonly Dictionary<Color, ImmutablePen> _glyphPens = new();

    /// <summary>Bumped on every capture, so parts can tell their cached drawing is stale.</summary>
    public int Version { get; private set; }

    public bool IsReady { get; private set; }

    public Color Foreground { get; private set; } = Colors.Black;
    public Color TextSecondary { get; private set; }
    public Color TextMuted { get; private set; }
    public Color Border { get; private set; }
    public Color BorderStrong { get; private set; }
    public Color Surface { get; private set; } = Colors.White;
    public Color SurfaceMuted { get; private set; }
    public Color Warning { get; private set; }
    public Color Success { get; private set; }
    public Color FocusRing { get; private set; }

    /// <summary>The first and second choice colors.</summary>
    public Color[] Rank { get; } = new Color[2];

    /// <summary>A lit tile's fill and border per choice.</summary>
    public Color[] TileFill { get; } = new Color[2];
    public Color[] TileBorder { get; } = new Color[2];

    /// <summary>Lit lines and capacity bars per choice (70%).</summary>
    public Color[] RankLine { get; } = new Color[2];

    /// <summary>A sequence token routed to an inspected expert: fill and border per choice.</summary>
    public Color[] TokenTint { get; } = new Color[2];
    public Color[] TokenTintBorder { get; } = new Color[2];

    public Color WarningBorder { get; private set; }
    public Color WarningLine { get; private set; }
    public Color CurrentRing { get; private set; }
    public Color RouterLiveBorder { get; private set; }
    public Color Track { get; private set; }
    public Color FillIdle { get; private set; }
    public Color FillLive { get; private set; }
    public Color TextIdle { get; private set; }
    public Color FlowActive { get; private set; }
    public Color CapacityIdle { get; private set; }
    public Color CapacityFull { get; private set; }
    public Color LoadTrack { get; private set; }
    public Color LoadSegment { get; private set; }

    public Typeface Regular { get; private set; }
    public Typeface Medium { get; private set; }

    public ExplainerTextCache Texts { get; } = new(1024);

    public void Capture(MixtureOfExperts owner)
    {
        Foreground = ColorOf(owner.Foreground, Colors.Black);
        TextSecondary = ColorOf(owner.TextSecondaryInk, Color.FromRgb(0x52, 0x52, 0x52));
        TextMuted = ColorOf(owner.TextMutedInk, Color.FromRgb(0x73, 0x73, 0x73));
        Border = ColorOf(owner.BorderInk, Color.FromRgb(0xE5, 0xE5, 0xE5));
        BorderStrong = ColorOf(owner.BorderStrongInk, Color.FromRgb(0xD4, 0xD4, 0xD4));
        Surface = ColorOf(owner.SurfaceInk, Colors.White);
        SurfaceMuted = ColorOf(owner.SurfaceMutedInk, Color.FromRgb(0xF5, 0xF5, 0xF5));
        Warning = ColorOf(owner.WarningInk, Color.FromRgb(0xC2, 0x7A, 0x00));
        Success = ColorOf(owner.SuccessInk, Color.FromRgb(0x16, 0x8A, 0x3E));
        FocusRing = ColorOf(owner.FocusRingInk, Color.FromArgb(184, 0x1F, 0x1F, 0x1F));
        Rank[0] = ColorOf(owner.FirstChoiceBrush ?? owner.Series1Ink, Color.FromRgb(0x1F, 0x7D, 0xCF));
        Rank[1] = ColorOf(owner.SecondChoiceBrush ?? owner.Series2Ink, Color.FromRgb(0x71, 0x28, 0xA5));
        for (var r = 0; r < 2; r++)
        {
            TileFill[r] = Oklch.MixOklab(Surface, Rank[r], 0.09);
            TileBorder[r] = Oklch.MixOklab(Border, Rank[r], 0.55);
            RankLine[r] = Oklch.WithAlpha(Rank[r], 0.7);
            TokenTint[r] = Oklch.MixOklab(Surface, Rank[r], 0.12);
            TokenTintBorder[r] = Oklch.WithAlpha(Rank[r], 0.55);
        }
        WarningBorder = Oklch.MixOklab(Border, Warning, 0.55);
        WarningLine = Oklch.WithAlpha(Warning, 0.7);
        CurrentRing = Oklch.MixOklab(BorderStrong, Foreground, 0.4);
        RouterLiveBorder = Oklch.MixOklab(Border, BorderStrong, 0.6);
        Track = Oklch.WithAlpha(Foreground, 0.07);
        FillIdle = Oklch.WithAlpha(TextMuted, 0.3);
        FillLive = Oklch.WithAlpha(TextMuted, 0.55);
        TextIdle = Oklch.WithAlpha(TextMuted, 0.6);
        FlowActive = Oklch.MixOklab(BorderStrong, TextMuted, 0.5);
        CapacityIdle = Oklch.WithAlpha(TextMuted, 0.28);
        CapacityFull = Oklch.WithAlpha(TextSecondary, 0.55);
        LoadTrack = Oklch.WithAlpha(Foreground, 0.06);
        LoadSegment = Oklch.WithAlpha(TextMuted, 0.45);
        var family = owner.FontFamily;
        Regular = new Typeface(family, FontStyle.Normal, FontWeight.Normal);
        Medium = new Typeface(family, FontStyle.Normal, FontWeight.Medium);
        _pens.Clear();
        _glyphPens.Clear();
        Texts.Clear();
        Version++;
        IsReady = true;
    }

    /// <summary>A cached brush of <paramref name="color"/> (alpha kept exactly; the cache holds every palette color).</summary>
    public IImmutableSolidColorBrush Brush(Color color) => Texts.Brush(color);

    /// <summary>A cached pen.</summary>
    public ImmutablePen Pen(Color color, double thickness, bool dashed = false)
    {
        if (!_pens.TryGetValue((color, thickness, dashed), out var pen))
        {
            pen = new ImmutablePen(new ImmutableSolidColorBrush(color), thickness,
                dashed ? new ImmutableDashStyle([3 / thickness, 3 / thickness], 0) : null, PenLineCap.Flat, PenLineJoin.Round);
            _pens[(color, thickness, dashed)] = pen;
        }
        return pen;
    }

    /// <summary>Draws a 24×24 stroked glyph (Lucide, 1.75 stroke in its own units) scaled into <paramref name="box"/>.</summary>
    public void DrawGlyph(DrawingContext context, Geometry glyph, Rect box, Color color)
    {
        if (!_glyphPens.TryGetValue(color, out var pen))
        {
            pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1.75, null, PenLineCap.Round, PenLineJoin.Round);
            _glyphPens[color] = pen;
        }
        var scale = Math.Min(box.Width, box.Height) / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(box.X, box.Y)))
        {
            context.DrawGeometry(null, pen, glyph);
        }
    }

    /// <summary>A one-line text at 14 px (or <paramref name="size"/>), cached.</summary>
    public FormattedText Text(string text, Color color, bool medium = false, double size = 14)
        => Texts.Get(text, medium ? Medium : Regular, size, color);

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid
        ? Color.FromArgb((byte)Math.Round(solid.Color.A * Math.Clamp(solid.Opacity, 0, 1)), solid.Color.R, solid.Color.G, solid.Color.B)
        : fallback;
}

/// <summary>
/// A crossfade between two looks of a drawn part (looks are small integers each part maps to colors), on wall time like
/// a CSS transition: <see cref="Set"/> starts it, <see cref="Mix"/> says how far the new look has come in. A change in
/// the middle of a fade starts from whichever look was showing more.
/// </summary>
internal struct MixtureOfExpertsFade
{
    private ExplainerValue _mix;

    public MixtureOfExpertsFade(int look)
    {
        From = To = look;
        _mix = new ExplainerValue(1);
    }

    public int From { get; private set; }

    public int To { get; private set; }

    /// <summary>The time the fade ends (wall seconds).</summary>
    public double Until { get; private set; }

    /// <summary>Moves to <paramref name="look"/>, fading over <paramref name="duration"/> seconds (0 jumps). True when it changed.</summary>
    public bool Set(int look, double now, double duration)
    {
        if (look == To)
        {
            return false;
        }
        if (duration <= 0)
        {
            Jump(look);
            return true;
        }
        From = _mix.Value(now) >= 0.5 ? To : From;
        To = look;
        _mix.Jump(0);
        _mix.Tween(1, now, duration, ArcEasings.Standard);
        Until = now + duration;
        return true;
    }

    public void Jump(int look)
    {
        From = To = look;
        _mix.Jump(1);
        Until = 0;
    }

    /// <summary>How far the new look has come in (1 = only the new look shows).</summary>
    public double Mix(double now) => From == To ? 1 : Math.Clamp(_mix.Value(now), 0, 1);
}

/// <summary>A number tweened on wall time like a CSS transition (bar lengths, segment widths, opacity).</summary>
internal struct MixtureOfExpertsTween
{
    private ExplainerValue _value;

    public MixtureOfExpertsTween(double value)
    {
        _value = new ExplainerValue(value);
        Until = 0;
    }

    public double Target => _value.Target;

    public double Until { get; private set; }

    public bool Set(double value, double now, double duration, Easing? easing = null)
    {
        if (Math.Abs(value - _value.Target) < 1e-9)
        {
            return false;
        }
        if (duration <= 0)
        {
            _value.Jump(value);
            Until = 0;
            return true;
        }
        _value.Tween(value, now, duration, easing ?? ArcEasings.Standard);
        Until = now + duration;
        return true;
    }

    public void Jump(double value)
    {
        _value.Jump(value);
        Until = 0;
    }

    public double Value(double now) => _value.Value(now);
}

/// <summary>Packet trails: a reusable pen whose dash shows one part of a path (see <see cref="ExplainerPath.ShowPart"/>).</summary>
internal static class MixtureOfExpertsPens
{
    public static Pen Trail(Color color) => new(new ImmutableSolidColorBrush(color), 2, new DashStyle(new AvaloniaList<double>(), 0), PenLineCap.Round);
}
