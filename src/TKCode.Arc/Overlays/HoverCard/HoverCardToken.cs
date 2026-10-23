using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class HoverCardToken : AbstractControlDesignToken
{
    /// <summary>Card width (Arc: 18.5rem).</summary>
    public double CardWidth { get; set; }

    /// <summary>Gap between the trigger and the card.</summary>
    public double SideOffset { get; set; }

    /// <summary>Space inside the card border (Arc: 16).</summary>
    public Thickness CardPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardWidth = 296;
        SideOffset = 8;
        CardPadding = new Thickness(16);
    }
}
