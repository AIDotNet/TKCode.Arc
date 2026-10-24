// Ported from Arc's free otp-input block (MIT, Copyright (c) 2026 Elia Kuratli): otp-input.module.css.
using AtomUI.Theme.DesignTokens;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="OtpInput"/> and the two state fills it derives from the neutrals. Everything that follows the
/// accent (the filled slot, the focus ring), radii and fonts come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class OtpInputToken : AbstractControlDesignToken
{
    /// <summary>A slot's width before the row has to shrink (<c>flex: 0 1 42px</c>).</summary>
    public double SlotWidth { get; set; }

    /// <summary>A slot's height (48).</summary>
    public double SlotHeight { get; set; }

    /// <summary>A slot's height in a narrow field (44; Arc: viewport at most 360px).</summary>
    public double SlotHeightNarrow { get; set; }

    /// <summary>Space between slots (8).</summary>
    public double SlotGap { get; set; }

    /// <summary>Space between slots in a narrow field (6).</summary>
    public double SlotGapNarrow { get; set; }

    /// <summary>Opacity of disabled slots and their characters (.48).</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>An invalid slot's fill: <c>color-mix(in srgb, var(--danger) 7%, var(--surface))</c>.</summary>
    public Color InvalidFill { get; set; }

    /// <summary>A slot's fill in the success state (ours; Arc has none): success mixed 7% into the surface.</summary>
    public Color SuccessFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        SlotWidth = 42;
        SlotHeight = 48;
        SlotHeightNarrow = 44;
        SlotGap = 8;
        SlotGapNarrow = 6;
        DisabledOpacity = 0.48;
        InvalidFill = MixSrgb(n.Danger, n.Surface, 0.07);
        SuccessFill = MixSrgb(n.Success, n.Surface, 0.07);
    }

    /// <summary>CSS <c>color-mix(in srgb, a p, b)</c> for opaque colors.</summary>
    internal static Color MixSrgb(Color a, Color b, double amountOfA)
    {
        static byte Channel(byte x, byte y, double t) => (byte)Math.Clamp(Math.Round(x * t + y * (1 - t)), 0, 255);
        return Color.FromRgb(Channel(a.R, b.R, amountOfA), Channel(a.G, b.G, amountOfA), Channel(a.B, b.B, amountOfA));
    }
}
