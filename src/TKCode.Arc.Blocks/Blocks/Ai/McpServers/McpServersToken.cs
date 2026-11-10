using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="McpServers"/> and the accent-independent tints it derives from the neutrals. Colors
/// that follow the accent or the status, radii and fonts come from ArcDesign tokens. Paddings are Avalonia thicknesses
/// (left, top, right, bottom); the compact ones apply at or below <see cref="CompactWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class McpServersToken : AbstractControlDesignToken
{
    /// <summary>The card is at most this wide (720) and centered.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>At or below this width of the block the paddings tighten and the header stacks (520).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Header padding (24 22 24 16).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Header padding in a narrow card (16 18 16 14).</summary>
    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>Space around the list of servers (12 at the sides, 12 below).</summary>
    public Thickness ListPadding { get; set; }

    /// <summary>Space around the list in a narrow card (6 at the sides, 8 below).</summary>
    public Thickness CompactListPadding { get; set; }

    /// <summary>Padding of the Add server form (24 at the sides, 20 above and below).</summary>
    public Thickness FormPadding { get; set; }

    /// <summary>Padding of the form in a narrow card (16 at the sides).</summary>
    public Thickness CompactFormPadding { get; set; }

    /// <summary>Padding inside a server's header button (12 10).</summary>
    public Thickness RowHeaderPadding { get; set; }

    /// <summary>Side length of a server's mark (36).</summary>
    public double MarkSize { get; set; }

    /// <summary>Padding of an open server's body: lined up with the name (60 at the start), 16 at the end, 16 below.</summary>
    public Thickness BodyPadding { get; set; }

    /// <summary>Padding of an open server's body in a narrow card (12 at the sides).</summary>
    public Thickness CompactBodyPadding { get; set; }

    /// <summary>Diameter of the status dot (8).</summary>
    public double StatusDotSize { get; set; }

    /// <summary>How far the connected dot's halo grows past the dot (5).</summary>
    public double StatusHaloSpread { get; set; }

    /// <summary>One breath of the connected dot's halo (2.4 s).</summary>
    public TimeSpan ConnectedPulse { get; set; }

    /// <summary>One breath of the connecting dot (0.9 s).</summary>
    public TimeSpan ConnectingPulse { get; set; }

    /// <summary>The tint behind a hovered server header: the muted surface at 70%.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The tint behind an open server: the muted surface at 40%.</summary>
    public Color RowOpenBackground { get; set; }

    /// <summary>The longest the error log grows before it scrolls (160).</summary>
    public double LogMaxHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardMaxWidth = 720;
        CompactWidth = 520;
        HeaderPadding = new Thickness(24, 22, 24, 16);
        CompactHeaderPadding = new Thickness(16, 18, 16, 14);
        ListPadding = new Thickness(12, 0, 12, 12);
        CompactListPadding = new Thickness(6, 0, 6, 8);
        FormPadding = new Thickness(24, 20, 24, 20);
        CompactFormPadding = new Thickness(16, 16, 16, 16);
        RowHeaderPadding = new Thickness(12, 10, 12, 10);
        MarkSize = 36;
        BodyPadding = new Thickness(60, 2, 16, 16);
        CompactBodyPadding = new Thickness(12, 2, 12, 14);
        StatusDotSize = 8;
        StatusHaloSpread = 5;
        ConnectedPulse = TimeSpan.FromMilliseconds(2400);
        ConnectingPulse = TimeSpan.FromMilliseconds(900);
        RowHoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.7);
        RowOpenBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.4);
        LogMaxHeight = 160;
    }
}
