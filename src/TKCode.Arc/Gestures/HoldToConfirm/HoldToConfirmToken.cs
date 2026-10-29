using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class HoldToConfirmToken : AbstractControlDesignToken
{
    /// <summary>Inline padding (Arc <c>0 var(--space-5)</c>).</summary>
    public Thickness InlinePadding { get; set; }

    /// <summary>Danger tone border: 32% danger mixed into the border (accent independent).</summary>
    public Color DangerBorder { get; set; }

    /// <summary>Opacity of the hover tint of the fill color over the surface (Arc mixes 6%).</summary>
    public double HoverTintOpacity { get; set; }

    public double DisabledOpacity { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        InlinePadding = new Thickness(20, 0);
        DangerBorder = Oklch.MixOklab(neutrals.Border, neutrals.Danger, 0.32);
        HoverTintOpacity = 0.06;
        DisabledOpacity = 0.5;
        FocusRingRadius = new CornerRadius(22);
    }
}
