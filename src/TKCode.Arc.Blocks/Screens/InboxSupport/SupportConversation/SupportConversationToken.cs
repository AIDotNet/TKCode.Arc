namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SupportConversation"/> (an original TKCode design). Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class SupportConversationToken : AbstractControlDesignToken
{
    /// <summary>Below this width the details hide behind a toggle (820).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Width of the customer panel (300).</summary>
    public double SidePanelWidth { get; set; }

    /// <summary>The height the view takes when its container does not give one (620).</summary>
    public double DefaultHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        NarrowWidth = 820;
        SidePanelWidth = 300;
        DefaultHeight = 620;
    }
}
