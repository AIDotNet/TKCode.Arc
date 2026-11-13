using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The neutral colors of the screen shell in the current theme, read from ArcDesign tokens (falling back to
/// <see cref="ArcPalette.Neutrals"/>) and turned into immutable brushes once per theme. The shell's page background is the
/// theme's Background in dark and SurfaceMuted in light (a near-white page under white cards).
/// </summary>
internal sealed class ScreenPaint
{
    private static ScreenPaint? s_last;

    private ScreenPaint(Control owner, bool dark)
    {
        IsDark = dark;
        var n = ArcPalette.Neutrals(dark);
        Color Get(ArcDesignTokenKind kind, Color fallback)
        {
            var c = ArcTokens.GetColor(owner, kind);
            return c.A == 0 ? fallback : c;
        }
        Background = Get(ArcDesignTokenKind.Background, n.Background);
        Surface = Get(ArcDesignTokenKind.Surface, n.Surface);
        SurfaceRaised = Get(ArcDesignTokenKind.SurfaceRaised, n.SurfaceRaised);
        SurfaceMuted = Get(ArcDesignTokenKind.SurfaceMuted, n.SurfaceMuted);
        Foreground = Get(ArcDesignTokenKind.Foreground, n.Foreground);
        TextSecondary = Get(ArcDesignTokenKind.TextSecondary, n.TextSecondary);
        TextMuted = Get(ArcDesignTokenKind.TextMuted, n.TextMuted);
        Border = Get(ArcDesignTokenKind.Border, n.Border);
        BorderSubtle = Get(ArcDesignTokenKind.BorderSubtle, n.BorderSubtle);
        BorderStrong = Get(ArcDesignTokenKind.BorderStrong, n.BorderStrong);
        FocusRing = Get(ArcDesignTokenKind.FocusRing, Oklch.WithAlpha(Foreground, 0.72));
        Danger = Get(ArcDesignTokenKind.Danger, n.Danger);
        Page = dark ? Background : SurfaceMuted;

        PageBrush = B(Page);
        SurfaceBrush = B(Surface);
        RaisedBrush = B(SurfaceRaised);
        MutedSurfaceBrush = B(SurfaceMuted);
        ForegroundBrush = B(Foreground);
        SecondaryBrush = B(TextSecondary);
        MutedBrush = B(TextMuted);
        BorderBrush = B(Border);
        SubtleBrush = B(BorderSubtle);
        StrongBrush = B(BorderStrong);
        DangerBrush = B(Danger);
        // Arc's screen shell: hover fills are the foreground at 5% (4.5% light), active/pressed toggles 8% (7% light).
        HoverFill = B(Oklch.WithAlpha(Foreground, dark ? 0.05 : 0.045));
        ActiveFill = B(Oklch.WithAlpha(Foreground, dark ? 0.08 : 0.07));
        OpenFill = B(Oklch.WithAlpha(Foreground, 0.09));
        ChipHoverFill = B(Oklch.WithAlpha(Foreground, dark ? 0.06 : 0.045));
        ButtonHover = B(SurfaceMuted);
        Scrim = B(Color.FromArgb((byte)(dark ? 133 : 71), 0, 0, 0));
        DialogScrim = B(Color.FromArgb(61, 10, 10, 10));
        InverseBrush = B(dark ? Foreground : Foreground);
        InverseText = B(dark ? Background : Surface);
        KbdBorder = B(Oklch.MixOklab(Border, BorderStrong, 0.45));
        BorderPen = new ImmutablePen(BorderBrush, 1);
        SubtlePen = new ImmutablePen(SubtleBrush, 1);
        StrongPen = new ImmutablePen(StrongBrush, 1);
        FocusPen = new ImmutablePen(B(FocusRing), 2);
        DashedBorderPen = new ImmutablePen(BorderBrush, 1, new ImmutableDashStyle(new double[] { 3, 3 }, 0));
    }

    private static ImmutableSolidColorBrush B(Color c) => new(c);

    /// <summary>
    /// The colors for <paramref name="owner"/>'s theme. The last set is reused while the theme's key colors are the same, so
    /// the many parts of a screen share one set; call it again when <c>ActualThemeVariant</c> changes.
    /// </summary>
    public static ScreenPaint For(Control owner)
    {
        var dark = IsDarkTheme(owner);
        var last = s_last;
        if (last is not null && last.IsDark == dark
            && last.Foreground == ArcTokens.GetColor(owner, ArcDesignTokenKind.Foreground)
            && last.Surface == ArcTokens.GetColor(owner, ArcDesignTokenKind.Surface)
            && last.Border == ArcTokens.GetColor(owner, ArcDesignTokenKind.Border))
        {
            return last;
        }
        return s_last = new ScreenPaint(owner, dark);
    }

    public static bool IsDarkTheme(Control owner)
    {
        var fg = ArcTokens.GetColor(owner, ArcDesignTokenKind.Foreground);
        if (fg.A == 0)
        {
            return owner.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        }
        return Oklch.FromColor(fg).L > 0.5;
    }

    public bool IsDark { get; }

    public Color Background { get; }
    public Color Surface { get; }
    public Color SurfaceRaised { get; }
    public Color SurfaceMuted { get; }
    public Color Foreground { get; }
    public Color TextSecondary { get; }
    public Color TextMuted { get; }
    public Color Border { get; }
    public Color BorderSubtle { get; }
    public Color BorderStrong { get; }
    public Color FocusRing { get; }
    public Color Danger { get; }
    public Color Page { get; }

    public IImmutableSolidColorBrush PageBrush { get; }
    public IImmutableSolidColorBrush SurfaceBrush { get; }
    public IImmutableSolidColorBrush RaisedBrush { get; }
    public IImmutableSolidColorBrush MutedSurfaceBrush { get; }
    public IImmutableSolidColorBrush ForegroundBrush { get; }
    public IImmutableSolidColorBrush SecondaryBrush { get; }
    public IImmutableSolidColorBrush MutedBrush { get; }
    public IImmutableSolidColorBrush BorderBrush { get; }
    public IImmutableSolidColorBrush SubtleBrush { get; }
    public IImmutableSolidColorBrush StrongBrush { get; }
    public IImmutableSolidColorBrush DangerBrush { get; }
    public IImmutableSolidColorBrush HoverFill { get; }
    public IImmutableSolidColorBrush ActiveFill { get; }
    public IImmutableSolidColorBrush OpenFill { get; }
    public IImmutableSolidColorBrush ChipHoverFill { get; }
    public IImmutableSolidColorBrush ButtonHover { get; }
    public IImmutableSolidColorBrush Scrim { get; }
    public IImmutableSolidColorBrush DialogScrim { get; }
    public IImmutableSolidColorBrush InverseBrush { get; }
    public IImmutableSolidColorBrush InverseText { get; }
    public IImmutableSolidColorBrush KbdBorder { get; }
    public IPen BorderPen { get; }
    public IPen SubtlePen { get; }
    public IPen StrongPen { get; }
    public IPen FocusPen { get; }
    public IPen DashedBorderPen { get; }
}
