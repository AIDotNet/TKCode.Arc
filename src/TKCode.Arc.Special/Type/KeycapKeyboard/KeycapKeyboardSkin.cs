using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Everything a keycap needs to draw at one key unit and in one theme: geometry from the unit, the brushes, shadows, pens
/// and typeface. Built by <see cref="KeycapKeyboard"/> when the unit or a theme value changes, shared by every key, so a
/// frame allocates nothing.
/// </summary>
internal sealed class KeycapKeyboardSkin
{
    // Keycap proportions, all of the key unit (Arc keycap keyboard, design reference).
    internal const double GapRatio = 0.14;
    internal const double TravelRatio = 0.085;
    internal const double RadiusRatio = 0.2;
    internal const double FaceRadiusRatio = 0.156;
    internal const double FaceSideRatio = 0.09;
    internal const double FaceTopRatio = 0.05;
    internal const double FaceBottomRatio = 0.15;
    internal const double LipRatio = 0.07;
    internal const double GlowInsetRatio = 0.22;
    internal const double GlowRadiusRatio = 0.4;
    internal const double PressDepth = 0.92;
    internal const double LatchDepth = 0.5;

    internal static readonly Geometry BackspaceIcon =
        ArcIcons.Parse("M10 5a2 2 0 0 0-1.344.519l-6.328 5.74a1 1 0 0 0 0 1.481l6.328 5.741A2 2 0 0 0 10 19h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2z M12 9l6 6 M18 9l-6 6"); // Lucide "delete"

    internal static readonly Geometry ShiftIcon =
        ArcIcons.Parse("M9 19a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1v-6a1 1 0 0 1 1-1h3.293a.707.707 0 0 0 .5-1.207l-7.086-7.086a1 1 0 0 0-1.414 0l-7.086 7.086a.707.707 0 0 0 .5 1.207H8a1 1 0 0 1 1 1z"); // Lucide "arrow-big-up"

    internal static Geometry EnterIcon => ArcIcons.CornerDownLeft;

    // Press motion (Arc keycap keyboard, design reference).
    internal static readonly Easing DownEasing = new SplineEasing(0.2, 0.8, 0.2, 1);
    internal static readonly Easing UpEasing = new SplineEasing(0.2, 0.9, 0.3, 1);
    internal static readonly Easing FadeEasing = new SplineEasing(0.22, 1, 0.36, 1);
    internal static readonly Easing Linear = new LinearEasing();

    public KeycapKeyboardSkin(double unit, in KeycapKeyboardColors colors, FontFamily legendFont)
    {
        Unit = unit;
        Travel = unit * TravelRatio;
        Gap = unit * GapRatio;
        Radius = unit * RadiusRatio;
        FaceRadius = unit * FaceRadiusRatio;
        FaceInset = new Thickness(unit * FaceSideRatio, unit * FaceTopRatio, unit * FaceSideRatio, unit * FaceBottomRatio);
        LegendSize = Math.Clamp(unit * 0.3, 9, 16);
        CornerSize = Math.Max(unit * 0.21, 7);
        IconSize = LegendSize;
        CornerPadding = new Thickness(unit * 0.13, unit * 0.1);
        GlowInset = unit * GlowInsetRatio;
        GlowRadius = unit * GlowRadiusRatio;
        Typeface = new Typeface(legendFont);

        Housing = new ImmutableSolidColorBrush(colors.Housing);
        HousingShadows = new BoxShadows(
            new BoxShadow { OffsetY = 1, Blur = 2, Color = colors.HousingShadow },
            [new BoxShadow { OffsetY = unit * 0.068, Blur = unit * 0.17, Color = colors.HousingShadow }]);
        CapShadows = new BoxShadows(
            new BoxShadow { Spread = 1, Color = colors.CapRing, IsInset = true },
            [new BoxShadow { OffsetY = -unit * LipRatio, Color = colors.CapLip, IsInset = true }]);
        FaceShadows = new BoxShadows(
            new BoxShadow { OffsetY = 1, Blur = 1.5, Color = colors.FaceShade, IsInset = true },
            [new BoxShadow { OffsetY = -1, Color = colors.FaceLight, IsInset = true }]);

        CapNeutral = Vertical(colors.CapTop, colors.CapBottom);
        FaceNeutral = Vertical(colors.FaceTop, colors.FaceBottom);
        var accent = colors.Accent;
        CapAccent = Vertical(Oklch.MixOklab(accent, Colors.White, 0.14), accent);
        FaceAccent = Vertical(Oklch.MixOklab(accent, Colors.Black, 0.08), Oklch.MixOklab(accent, Colors.White, 0.12));
        TintNeutral = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, colors.PressTintOpacity));
        TintAccent = new ImmutableSolidColorBrush(Oklch.WithAlpha(Colors.White, colors.AccentPressTintOpacity));
        Glow = new ImmutableRadialGradientBrush(new RadialGradientBrush
        {
            RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Oklch.WithAlpha(accent, colors.GlowOpacity), 0),
                new GradientStop(Oklch.WithAlpha(accent, 0), 1)
            }
        });

        Legend = new ImmutableSolidColorBrush(colors.Legend);
        CornerLegend = new ImmutableSolidColorBrush(colors.CornerLegend);
        ActiveLegend = new ImmutableSolidColorBrush(colors.ActiveLegend);
        AccentLegend = new ImmutableSolidColorBrush(colors.AccentLegend);
        BindingText = new ImmutableSolidColorBrush(colors.BindingText);
        BindingFace = new ImmutableSolidColorBrush(colors.BindingFace);
        CornerPen = IconPen(CornerLegend);
        ActivePen = IconPen(ActiveLegend);
        AccentPen = IconPen(AccentLegend);
        FocusPen = new ImmutablePen(new ImmutableSolidColorBrush(colors.Focus), 2);
    }

    public double Unit { get; }
    public double Travel { get; }
    public double Gap { get; }
    public double Radius { get; }
    public double FaceRadius { get; }
    public Thickness FaceInset { get; }
    public double LegendSize { get; }
    public double CornerSize { get; }
    public double IconSize { get; }
    public Thickness CornerPadding { get; }
    public double GlowInset { get; }
    public double GlowRadius { get; }
    public Typeface Typeface { get; }

    public IBrush Housing { get; }
    public BoxShadows HousingShadows { get; }
    public IBrush CapNeutral { get; }
    public IBrush CapAccent { get; }
    public BoxShadows CapShadows { get; }
    public IBrush FaceNeutral { get; }
    public IBrush FaceAccent { get; }
    public BoxShadows FaceShadows { get; }
    public IBrush TintNeutral { get; }
    public IBrush TintAccent { get; }
    public IBrush Glow { get; }
    public IBrush Legend { get; }
    public IBrush CornerLegend { get; }
    public IBrush ActiveLegend { get; }
    public IBrush AccentLegend { get; }
    public IBrush BindingText { get; }
    public IBrush BindingFace { get; }
    public IPen CornerPen { get; }
    public IPen ActivePen { get; }
    public IPen AccentPen { get; }
    public IPen FocusPen { get; }

    /// <summary>Width of a key of <paramref name="units"/> key units: the units and the gaps they swallow.</summary>
    public double KeyWidth(double units) => units * (Unit + Gap) - Gap;

    /// <summary>Height of a key's box: the cap and the side below it.</summary>
    public double KeyHeight => Unit + Travel;

    private static IBrush Vertical(Color top, Color bottom) => new ImmutableLinearGradientBrush(
        [new ImmutableGradientStop(0, top), new ImmutableGradientStop(1, bottom)],
        startPoint: new RelativePoint(0.5, 0, RelativeUnit.Relative),
        endPoint: new RelativePoint(0.5, 1, RelativeUnit.Relative));

    // Lucide strokes are 1.75 wide on the 24 px grid; the icon is drawn scaled, so the pen is too.
    private static IPen IconPen(IBrush brush) => new ImmutablePen((IImmutableBrush)brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
}

/// <summary>The theme colors a <see cref="KeycapKeyboardSkin"/> is built from.</summary>
internal readonly record struct KeycapKeyboardColors(
    Color Housing,
    Color HousingShadow,
    Color CapTop,
    Color CapBottom,
    Color CapRing,
    Color CapLip,
    Color FaceTop,
    Color FaceBottom,
    Color FaceShade,
    Color FaceLight,
    Color Legend,
    Color CornerLegend,
    Color ActiveLegend,
    Color Accent,
    Color AccentLegend,
    Color BindingText,
    Color BindingFace,
    Color Focus,
    double GlowOpacity,
    double PressTintOpacity,
    double AccentPressTintOpacity);
