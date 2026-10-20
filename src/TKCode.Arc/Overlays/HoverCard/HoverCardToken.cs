using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class HoverCardToken : AbstractControlDesignToken
{
    /// <summary>Card width (Arc: 18.5rem).</summary>
    public double CardWidth { get; set; }

    /// <summary>Gap between the trigger and the card.</summary>
    public double SideOffset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardWidth = 296;
        SideOffset = 8;
    }
}
