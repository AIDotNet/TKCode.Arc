using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ApiKeys"/>, measured from the Developer group's key manager (design reference only, ADR 0027),
/// and the accent-independent tints it derives from the neutrals. Colors that follow the accent, the type ramp and the
/// fonts come from ArcDesign tokens in the theme. Paddings are Avalonia thicknesses (left, top, right, bottom); the compact
/// ones apply below <see cref="CompactWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class ApiKeysToken : AbstractControlDesignToken
{
    /// <summary>Below this width of the block the scopes column and the creation date leave the rows (700).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Below this width the side paddings tighten from 20 to 16 and the details stack (540).</summary>
    public double CompactWidth { get; set; }

    /// <summary>At or below this width of the open panel the form uses one column (560).</summary>
    public double StackedFormWidth { get; set; }

    /// <summary>Padding of the header and toolbar (20, 16) and its compact form (16, 16).</summary>
    public Thickness TopPadding { get; set; }

    public Thickness CompactTopPadding { get; set; }

    /// <summary>The description's widest line (520) and the gap under the title (2).</summary>
    public double DescriptionMaxWidth { get; set; }

    public double TitleGap { get; set; }

    /// <summary>The title: 16 on a 17.6 line, letter spacing −0.16.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    /// <summary>Between the description and the toolbar (16).</summary>
    public double ToolbarGap { get; set; }

    /// <summary>Row padding (20, 12) and its compact form (16, 12); 20 between the columns.</summary>
    public Thickness RowPadding { get; set; }

    public Thickness CompactRowPadding { get; set; }

    public double ColumnGap { get; set; }

    /// <summary>The dates column is at least this wide (128).</summary>
    public double DatesMinWidth { get; set; }

    /// <summary>A row's and an open row's tint: the muted surface mixed 80% into the surface.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The scope chips: 22 high, 8 inside, 4 apart.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipGap { get; set; }

    /// <summary>The strike drawn through a revoked key's name and mask (1.5 thick).</summary>
    public double StrikeThickness { get; set; }

    /// <summary>The details' padding (20, 0, 20, 16) and its compact form; 16 between facts and the revoke line.</summary>
    public Thickness DetailPadding { get; set; }

    public Thickness CompactDetailPadding { get; set; }

    public double DetailGap { get; set; }

    /// <summary>Between facts across (24) and down (12).</summary>
    public double FactColumnGap { get; set; }

    public double FactRowGap { get; set; }

    /// <summary>Padding of the empty state (24, 40).</summary>
    public Thickness EmptyPadding { get; set; }

    /// <summary>Between the form's groups (16) and the hit area overhang of a scope checkbox (13).</summary>
    public double FormGap { get; set; }

    public double ScopeHitOffset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        NarrowWidth = 700;
        CompactWidth = 540;
        StackedFormWidth = 560;
        TopPadding = new Thickness(20, 16);
        CompactTopPadding = new Thickness(16, 16);
        DescriptionMaxWidth = 520;
        TitleGap = 2;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.16;
        ToolbarGap = 16;
        RowPadding = new Thickness(20, 12);
        CompactRowPadding = new Thickness(16, 12);
        ColumnGap = 20;
        DatesMinWidth = 128;
        RowHoverBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.8);
        ChipHeight = 22;
        ChipPadding = new Thickness(8, 0);
        ChipGap = 4;
        StrikeThickness = 1.5;
        DetailPadding = new Thickness(20, 0, 20, 16);
        CompactDetailPadding = new Thickness(16, 0, 16, 16);
        DetailGap = 16;
        FactColumnGap = 24;
        FactRowGap = 12;
        EmptyPadding = new Thickness(24, 40);
        FormGap = 16;
        ScopeHitOffset = 13;
    }
}
