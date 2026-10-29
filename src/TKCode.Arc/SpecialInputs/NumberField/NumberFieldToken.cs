using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and accent-independent colors of <see cref="NumberField"/> (Arc <c>number-field.module.css</c>).</summary>
[ControlDesignToken]
internal sealed class NumberFieldToken : AbstractControlDesignToken
{
    /// <summary>Default shell (and head row) width per size: <c>--nf-width</c> 164 / 196 / 228.</summary>
    public double ShellWidthSM { get; set; }
    public double ShellWidthMD { get; set; }
    public double ShellWidthLG { get; set; }

    /// <summary>Shell inset: <c>--nf-inset</c> 3px (4px at lg), on every side.</summary>
    public Thickness ShellPadding { get; set; }
    public Thickness ShellPaddingLG { get; set; }

    /// <summary>Gap between the steppers and the value: <c>gap: 2px</c>.</summary>
    public double ShellGap { get; set; }

    /// <summary>Stepper square per size: <c>--nf-step</c> 28 / 34 / 40.</summary>
    public double StepSizeSM { get; set; }
    public double StepSizeMD { get; set; }
    public double StepSizeLG { get; set; }

    /// <summary>Concentric stepper corners: <c>calc(var(--radius-control) - var(--nf-inset) - 1px)</c> (18 − 3 − 1, lg 18 − 4 − 1).</summary>
    public CornerRadius StepRadius { get; set; }
    public CornerRadius StepRadiusLG { get; set; }
    public CornerRadius StepRingRadius { get; set; }
    public CornerRadius StepRingRadiusLG { get; set; }

    /// <summary>Lucide Minus / Plus: 16px, stroke 1.75 (viewBox units).</summary>
    public double StepGlyphSize { get; set; }
    public double StepGlyphStroke { get; set; }

    /// <summary>Stepper opacities: at a limit (<c>aria-disabled</c>) .32, refused during a strain .6, field disabled .35.</summary>
    public double StepLimitOpacity { get; set; }
    public double StepStrainOpacity { get; set; }
    public double StepDisabledOpacity { get; set; }

    /// <summary>Head row (label + limit note): <c>margin-bottom: var(--space-2)</c>, <c>gap: var(--space-3)</c>.</summary>
    public Thickness HeadMargin { get; set; }
    public Thickness NoteMargin { get; set; }

    /// <summary>The limit note: 12px on a 1.4 line.</summary>
    public double NoteLineHeight { get; set; }

    /// <summary><c>--nf-limit</c>: <c>color-mix(in oklab, var(--warning) 72%, var(--foreground))</c> (88% in dark).</summary>
    public Color LimitColor { get; set; }

    /// <summary>Typed value past a limit: border <c>mix(warning 55%, border)</c>, background <c>mix(warning 5%, surface)</c>.</summary>
    public Color WarnBorder { get; set; }
    public Color WarnSurface { get; set; }

    /// <summary>Focused warn border: <c>mix(warning 80%, border)</c>.</summary>
    public Color WarnBorderFocus { get; set; }

    /// <summary>Pressed / held stepper fill: <c>color-mix(in oklab, var(--foreground) 10%, var(--surface))</c>.</summary>
    public Color StepPressedFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ShellWidthSM = 164;
        ShellWidthMD = 196;
        ShellWidthLG = 228;
        ShellPadding = new Thickness(3);
        ShellPaddingLG = new Thickness(4);
        ShellGap = 2;
        StepSizeSM = 28;
        StepSizeMD = 34;
        StepSizeLG = 40;
        StepRadius = new CornerRadius(14);
        StepRadiusLG = new CornerRadius(13);
        // The steppers' focus rings sit 4px outside them: radius + 4 keeps them concentric.
        StepRingRadius = new CornerRadius(18);
        StepRingRadiusLG = new CornerRadius(17);
        StepGlyphSize = 16;
        StepGlyphStroke = 1.75;
        StepLimitOpacity = 0.32;
        StepStrainOpacity = 0.6;
        StepDisabledOpacity = 0.35;
        HeadMargin = new Thickness(0, 0, 0, 8);
        NoteMargin = new Thickness(12, 0, 0, 0);
        NoteLineHeight = 12 * 1.4;
        LimitColor = Oklch.MixOklab(n.Foreground, n.Warning, isDarkMode ? 0.88 : 0.72);
        WarnBorder = Oklch.MixOklab(n.Border, n.Warning, 0.55);
        WarnSurface = Oklch.MixOklab(n.Surface, n.Warning, 0.05);
        WarnBorderFocus = Oklch.MixOklab(n.Border, n.Warning, 0.80);
        StepPressedFill = Oklch.MixOklab(n.Surface, n.Foreground, 0.10);
    }
}
