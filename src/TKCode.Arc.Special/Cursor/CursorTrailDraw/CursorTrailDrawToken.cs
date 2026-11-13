using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CursorTrailDraw"/> (Arc Cursor trail draw, design reference) and the surface colors derived
/// from the neutrals: the card, the stage, the floating toolbar and chip, the grid, ink and cursor sizes. Accent, ink and
/// text colors come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CursorTrailDrawToken : AbstractControlDesignToken
{
    /// <summary>Card padding round the header and the stage (8).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Gap between the header and the stage (8).</summary>
    public double CardSpacing { get; set; }

    /// <summary>Header padding (16 sides, 8 top, 4 bottom; 12 sides on narrow boards).</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness HeaderPaddingCompact { get; set; }

    /// <summary>Title size (15) and tracking (−0.45 px).</summary>
    public double TitleFontSize { get; set; }

    public double TitleLetterSpacing { get; set; }

    /// <summary>Item count, hint and chip text size (13).</summary>
    public double SmallFontSize { get; set; }

    /// <summary>Stage shape: 16:10 wide, 4:5 on boards narrower than <see cref="CompactWidth"/>.</summary>
    public double StageAspect { get; set; }

    public double StageAspectCompact { get; set; }

    /// <summary>Below this content width the stage turns 4:5 and the toolbar buttons shrink to 38.</summary>
    public double CompactWidth { get; set; }

    /// <summary>Below this content width the toolbar buttons shrink to 30.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Toolbar button size: 40, 38 compact, 30 narrow (color buttons 28 wide when narrow).</summary>
    public double ToolSize { get; set; }

    public double ToolSizeCompact { get; set; }

    public double ToolSizeNarrow { get; set; }

    public double SwatchWidthNarrow { get; set; }

    /// <summary>Toolbar padding (4, 3 narrow) and button spacing (2, 0 narrow).</summary>
    public Thickness ToolbarPadding { get; set; }

    public Thickness ToolbarPaddingNarrow { get; set; }

    public double ToolbarSpacing { get; set; }

    /// <summary>Toolbar and chip distance from the stage edge (12).</summary>
    public Thickness ToolbarMargin { get; set; }

    public Thickness ChipMargin { get; set; }

    /// <summary>Divider between toolbar groups: 1 × 20 with 4 px either side (2 narrow).</summary>
    public double DividerHeight { get; set; }

    public Thickness DividerMargin { get; set; }

    public Thickness DividerMarginNarrow { get; set; }

    /// <summary>Icon size (18) and stroke (1.75).</summary>
    public double IconSize { get; set; }

    public double IconStroke { get; set; }

    /// <summary>Color dot (16), its selection ring (23 across, 1.5 thick, 2 px gap) and the selected scale (1.12).</summary>
    public double SwatchSize { get; set; }

    public double SwatchRingSize { get; set; }

    public double SwatchRingThickness { get; set; }

    public double SwatchSelectedScale { get; set; }

    /// <summary>Chip: 40 high with 12 / 4 padding, a 16 px check, a 32 px button with 12 px sides.</summary>
    public Thickness ChipPadding { get; set; }

    public double ChipSpacing { get; set; }

    public double ChipCheckSize { get; set; }

    public double ChipButtonHeight { get; set; }

    public Thickness ChipButtonPadding { get; set; }

    /// <summary>How long the chip stays after a snap (4.2 s).</summary>
    public TimeSpan ChipLifetime { get; set; }

    /// <summary>Hint inset: 20 at the sides, clear of the toolbar at the bottom.</summary>
    public Thickness HintMargin { get; set; }

    /// <summary>Grid dot spacing (22) and radius (0.9).</summary>
    public double GridSpacing { get; set; }

    public double GridDotRadius { get; set; }

    /// <summary>Width of snapped clean shapes in px (3.37, the ink at about half pressure).</summary>
    public double CleanWidth { get; set; }

    /// <summary>Cursor radii in px: pen ring (8.4), pen nib (2.4), laser ring (11), laser dot (4.5).</summary>
    public Thickness CursorRadii { get; set; }

    public double CursorThickness { get; set; }

    /// <summary>Laser trail width at its head in px (7).</summary>
    public double LaserWidth { get; set; }

    /// <summary>Disabled toolbar buttons (Undo and Clear on an empty board) at 40%.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>The color group while the laser is picked (55%).</summary>
    public double DimmedGroupOpacity { get; set; }

    /// <summary>Selected tool highlight: the accent at 14%.</summary>
    public double HighlightOpacity { get; set; }

    public Color StageBackground { get; set; }

    public BoxShadows StageHighlight { get; set; }

    public Color ToolbarBackground { get; set; }

    public BoxShadows ToolbarShadow { get; set; }

    public Color ToolHoverBackground { get; set; }

    public Color SwatchEdge { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardPadding = new Thickness(8);
        CardSpacing = 8;
        HeaderPadding = new Thickness(16, 8, 16, 4);
        HeaderPaddingCompact = new Thickness(12, 8, 12, 4);
        TitleFontSize = 15;
        TitleLetterSpacing = -0.45;
        SmallFontSize = 13;
        StageAspect = 1.6;
        StageAspectCompact = 0.8;
        CompactWidth = 560;
        NarrowWidth = 360;
        ToolSize = 40;
        ToolSizeCompact = 38;
        ToolSizeNarrow = 30;
        SwatchWidthNarrow = 28;
        ToolbarPadding = new Thickness(4);
        ToolbarPaddingNarrow = new Thickness(3);
        ToolbarSpacing = 2;
        ToolbarMargin = new Thickness(0, 0, 0, 12);
        ChipMargin = new Thickness(10, 12, 10, 0);
        DividerHeight = 20;
        DividerMargin = new Thickness(4, 0);
        DividerMarginNarrow = new Thickness(2, 0);
        IconSize = 18;
        IconStroke = 1.75;
        SwatchSize = 16;
        SwatchRingSize = 23;
        SwatchRingThickness = 1.5;
        SwatchSelectedScale = 1.12;
        ChipPadding = new Thickness(12, 4, 4, 4);
        ChipSpacing = 8;
        ChipCheckSize = 16;
        ChipButtonHeight = 32;
        ChipButtonPadding = new Thickness(12, 0);
        ChipLifetime = TimeSpan.FromMilliseconds(4200);
        HintMargin = new Thickness(20, 0, 20, 64);
        GridSpacing = 22;
        GridDotRadius = 0.9;
        CleanWidth = 3.37;
        CursorRadii = new Thickness(8.4, 2.4, 11, 4.5);
        CursorThickness = 1.24;
        LaserWidth = 7;
        DisabledOpacity = 0.4;
        DimmedGroupOpacity = 0.55;
        HighlightOpacity = 0.14;
        StageBackground = isDarkMode ? Oklch.MixOklab(n.Surface, n.Background, 0.2) : Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.5);
        StageHighlight = new BoxShadows(new BoxShadow
        {
            IsInset = true,
            OffsetY = 1,
            Color = isDarkMode ? Oklch.WithAlpha(n.Foreground, 0.04) : Oklch.WithAlpha(Colors.White, 0.7),
        });
        ToolbarBackground = isDarkMode ? Oklch.WithAlpha(n.SurfaceRaised, 0.72) : Oklch.WithAlpha(n.Surface, 0.8);
        var ring = new BoxShadow { Spread = 1, Color = n.Border };
        ToolbarShadow = isDarkMode
            ? new BoxShadows(ring, [new BoxShadow { OffsetY = 22, Blur = 55, Color = Color.FromArgb(87, 0, 0, 0) }])
            : new BoxShadows(ring,
            [
                new BoxShadow { OffsetY = 20, Blur = 48, Color = Color.FromArgb(27, 0, 0, 0) },
                new BoxShadow { OffsetY = 3, Blur = 10, Color = Color.FromArgb(11, 0, 0, 0) },
            ]);
        ToolHoverBackground = Oklch.WithAlpha(n.Foreground, 0.07);
        SwatchEdge = Oklch.WithAlpha(n.Foreground, 0.12);
    }
}
