using Avalonia;
using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class PaginationToken : AbstractControlDesignToken
{
    /// <summary>Arrow glyph size (Arc: 16px).</summary>
    public double StepGlyphSize { get; set; }

    /// <summary>Arc: disabled arrows at opacity .4.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Width reserved for the boundary ellipsis.</summary>
    public double EllipsisWidth { get; set; }

    public CornerRadius ButtonFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ButtonFocusRingRadius = new CornerRadius(22);
        StepGlyphSize = 16;
        DisabledOpacity = 0.4;
        EllipsisWidth = 24;
    }
}
