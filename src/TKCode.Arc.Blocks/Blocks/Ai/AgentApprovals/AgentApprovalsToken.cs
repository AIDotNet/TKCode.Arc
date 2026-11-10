using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="AgentApprovals"/>. Colors, radii and fonts come from ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class AgentApprovalsToken : AbstractControlDesignToken
{
    /// <summary>The widest the queue grows (920).</summary>
    public double MaxWidth { get; set; }

    /// <summary>The width of the list beside the detail (300).</summary>
    public double ListWidth { get; set; }

    /// <summary>At or below this width the list sits above the detail (680).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Padding of the list and of the detail (20).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>Padding of a request row (10).</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>The square that holds a request's glyph (32).</summary>
    public double GlyphBoxSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxWidth = 920;
        ListWidth = 300;
        CompactWidth = 680;
        SectionPadding = new Thickness(20);
        RowPadding = new Thickness(10);
        GlyphBoxSize = 32;
    }
}
