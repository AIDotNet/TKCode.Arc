using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="AgentBuilder"/> and the accent-independent tints it derives from the neutrals.
/// Colors that follow the accent, radii and fonts come from ArcDesign tokens in the theme. Breakpoints compare with the
/// builder's own width.
/// </summary>
[ControlDesignToken]
internal sealed class AgentBuilderToken : AbstractControlDesignToken
{
    /// <summary>Below this width the test chat moves under the settings (760).</summary>
    public double StackWidth { get; set; }

    /// <summary>Below this width the card paddings tighten (520).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Space between the settings card and the test chat card (16).</summary>
    public double ColumnGap { get; set; }

    /// <summary>The test chat column's share of the width beside the settings (0.46).</summary>
    public double ChatShare { get; set; }

    /// <summary>The narrowest the test chat column gets beside the settings (340).</summary>
    public double ChatMinWidth { get; set; }

    /// <summary>The lowest the test chat gets beside the settings (560).</summary>
    public double ChatMinHeight { get; set; }

    /// <summary>The test chat's height under the settings (540).</summary>
    public double StackedChatHeight { get; set; }

    /// <summary>Padding of a settings section (24 at the sides, 20 above and below).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>Padding of a settings section in a narrow builder (16, 16).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>Space between a section's heading and its content (12).</summary>
    public double SectionGap { get; set; }

    /// <summary>Height of the instructions field (176).</summary>
    public double InstructionsHeight { get; set; }

    /// <summary>The agent's avatar: a 40px circle.</summary>
    public double AvatarSize { get; set; }

    /// <summary>The square behind a tool or source glyph (32).</summary>
    public double IconBoxSize { get; set; }

    /// <summary>Padding of a tool or source row (10 at the sides, 8 above and below).</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>The tint behind a hovered row: the muted surface at 70%.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The fill of the test chat's header strip: the muted surface at 45%.</summary>
    public Color ChatHeaderBackground { get; set; }

    /// <summary>How long a save takes when no save handler is set (600ms).</summary>
    public TimeSpan SimulatedSave { get; set; }

    /// <summary>How long indexing a source takes when no index handler is set (2.4s).</summary>
    public TimeSpan SimulatedIndex { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        StackWidth = 760;
        CompactWidth = 520;
        ColumnGap = 16;
        ChatShare = 0.46;
        ChatMinWidth = 340;
        ChatMinHeight = 560;
        StackedChatHeight = 540;
        SectionPadding = new Thickness(24, 20);
        CompactSectionPadding = new Thickness(16, 16);
        SectionGap = 12;
        InstructionsHeight = 176;
        AvatarSize = 40;
        IconBoxSize = 32;
        RowPadding = new Thickness(10, 8);
        RowHoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.7);
        ChatHeaderBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.45);
        SimulatedSave = TimeSpan.FromMilliseconds(600);
        SimulatedIndex = TimeSpan.FromMilliseconds(2400);
    }
}
