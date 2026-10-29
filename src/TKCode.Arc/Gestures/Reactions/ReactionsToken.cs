using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry and motion constants of <see cref="Reactions"/> (values from Arc's reactions.tsx / reactions.module.css).</summary>
[ControlDesignToken]
internal sealed class ReactionsToken : AbstractControlDesignToken
{
    /// <summary>Pill and add button height.</summary>
    public double PillHeight { get; set; }

    /// <summary>Pill padding: 9px before the icon, 11px after the count.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>Gap between the pill icon and its count.</summary>
    public double PillContentGap { get; set; }

    /// <summary>Gap between pills in a row (6px).</summary>
    public double ColumnGap { get; set; }

    /// <summary>Gap between wrapped rows (space-2).</summary>
    public double RowGap { get; set; }

    public double PillGlyphSize { get; set; }

    public double AddButtonWidth { get; set; }

    /// <summary>Picker tray padding (5px) and the gap between its rows and options (2px).</summary>
    public Thickness PickerPadding { get; set; }

    public double PickerGap { get; set; }

    /// <summary>Picker options are 40px circles with 20px glyphs.</summary>
    public double OptionExtent { get; set; }

    public double OptionGlyphSize { get; set; }

    public double CaptionHeight { get; set; }

    /// <summary>Gap to the add button (Radix sideOffset 8).</summary>
    public double PickerOffset { get; set; }

    /// <summary>The label tip appears after a short hover (0.45s).</summary>
    public TimeSpan TipDelay { get; set; }

    /// <summary>The viewer's own reaction: border color-mix(accent 45%, border); hover 65%.</summary>
    public double ReactedRingOpacity { get; set; }

    public double ReactedHoverRingOpacity { get; set; }

    /// <summary>Hovered / pressed own reaction: color-mix(accent 17% / 18%, surface).</summary>
    public double ReactedHoverFillOpacity { get; set; }

    public double ReactedPressFillOpacity { get; set; }

    /// <summary>Filled glyph of a chosen reaction: color-mix(currentColor 22%, transparent).</summary>
    public double GlyphFillOpacity { get; set; }

    /// <summary>The tap impulse (P27): a spring of stiffness 420 and damping 17, not one of the standard presets.</summary>
    public double KickStiffness { get; set; }

    public double KickDamping { get; set; }

    /// <summary>Velocity added per tap: scale +11/s and rotation −420°/s; quick taps stack.</summary>
    public double KickScaleVelocity { get; set; }

    public double KickRotateVelocity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PillHeight = 32;
        PillPadding = new Thickness(9, 0, 11, 0);
        PillContentGap = 6;
        ColumnGap = 6;
        RowGap = 8;
        PillGlyphSize = 16;
        AddButtonWidth = 38;
        PickerPadding = new Thickness(5);
        PickerGap = 2;
        OptionExtent = 40;
        OptionGlyphSize = 20;
        CaptionHeight = 22;
        PickerOffset = 8;
        TipDelay = TimeSpan.FromMilliseconds(450);
        ReactedRingOpacity = 0.45;
        ReactedHoverRingOpacity = 0.65;
        ReactedHoverFillOpacity = 0.17;
        ReactedPressFillOpacity = 0.18;
        GlyphFillOpacity = 0.22;
        KickStiffness = 420;
        KickDamping = 17;
        KickScaleVelocity = 11;
        KickRotateVelocity = -420;
    }
}
