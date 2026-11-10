using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="InboxTriage"/> (an original TKCode design). Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class InboxTriageToken : AbstractControlDesignToken
{
    /// <summary>The widest the block grows (560).</summary>
    public double BlockMaxWidth { get; set; }

    /// <summary>Padding inside the block (20).</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>The tallest the list grows before it scrolls (520).</summary>
    public double ListMaxHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockMaxWidth = 560;
        BlockPadding = new Thickness(20);
        ListMaxHeight = 520;
    }
}
