using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SkeletonToken : AbstractControlDesignToken
{
    /// <summary>Block fill: 38% border-strong mixed into surface (accent independent).</summary>
    public Color BlockColor { get; set; }

    /// <summary>Block corner radius (radius-control; lines clamp it to half their height).</summary>
    public double BlockRadius { get; set; }

    /// <summary>How far the loaded content rises into place.</summary>
    public double ContentRise { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        BlockColor = Oklch.MixOklab(n.Surface, n.BorderStrong, 0.38);
        BlockRadius = 18;
        ContentRise = 4;
    }
}
