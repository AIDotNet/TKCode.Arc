using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TabsToken : AbstractControlDesignToken
{
    /// <summary>Padding of the trigger list inside its frame (Arc: space-1).</summary>
    public Thickness ListPadding { get; set; }

    /// <summary>Space between the list and the panel (Arc: space-4).</summary>
    public Thickness ContentMargin { get; set; }

    /// <summary>Line height of panel text (text-sm × leading-body).</summary>
    public double ContentLineHeight { get; set; }

    /// <summary>Width of the edge scroll buttons (Arc: 33px).</summary>
    public double ScrollButtonWidth { get; set; }

    public double ScrollGlyphSize { get; set; }

    public CornerRadius ScrollStartRadius { get; set; }

    public CornerRadius ScrollEndRadius { get; set; }

    /// <summary>Selection pill radius: radius-control − 3px.</summary>
    public CornerRadius PillRadius { get; set; }

    /// <summary>Focus ring around the panel (offset 3px around radius-control).</summary>
    public CornerRadius ContentFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ListPadding = new Thickness(4);
        ContentMargin = new Thickness(0, 16, 0, 0);
        ContentLineHeight = 14 * 1.4;
        ScrollButtonWidth = 33;
        ScrollGlyphSize = 17;
        ScrollStartRadius = new CornerRadius(17, 0, 0, 17);
        ScrollEndRadius = new CornerRadius(0, 17, 17, 0);
        PillRadius = new CornerRadius(15);
        ContentFocusRingRadius = new CornerRadius(22);
    }
}
