using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="TransferBox"/>: two list panels and the move actions between them.</summary>
[ControlDesignToken]
internal sealed class TransferBoxToken : AbstractControlDesignToken
{
    /// <summary>Height the box takes when none is set (both panels fill it).</summary>
    public double DefaultHeight { get; set; }

    /// <summary>Narrowest a list panel gets.</summary>
    public double PanelMinWidth { get; set; }

    /// <summary>Header row: the 44px select-all hit area sits 4px from the panel edge; the count keeps 14px on the right.</summary>
    public Thickness HeaderPadding { get; set; }

    public double HeaderMinHeight { get; set; }

    /// <summary>Space on both sides of the header title (between the select-all box and the item count).</summary>
    public Thickness HeaderTitleMargin { get; set; }

    /// <summary>Space around the search field under the header.</summary>
    public Thickness SearchMargin { get; set; }

    /// <summary>Inset of the rows inside the panel (the row radius is the panel radius minus this inset).</summary>
    public Thickness ListPadding { get; set; }

    public double RowMinHeight { get; set; }

    public Thickness RowPadding { get; set; }

    /// <summary>Gap under each row.</summary>
    public Thickness RowMargin { get; set; }

    /// <summary>Space between the row content and the selected check.</summary>
    public Thickness RowCheckMargin { get; set; }

    public double CheckGlyphSize { get; set; }

    /// <summary>The keyboard focus ring of a row sits on its edge (rows scroll, so an outer ring would be clipped).</summary>
    public Thickness RowFocusRingMargin { get; set; }

    /// <summary>Space between the panels and the move buttons.</summary>
    public Thickness ActionsMargin { get; set; }

    public double ActionsGap { get; set; }

    /// <summary>Width and height of a square move button (Arc small control size).</summary>
    public double MoveButtonExtent { get; set; }

    public double MoveGlyphSize { get; set; }

    public Thickness EmptyPadding { get; set; }

    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DefaultHeight = 320;
        PanelMinWidth = 180;
        HeaderPadding = new Thickness(4, 0, 14, 0);
        HeaderMinHeight = 48;
        HeaderTitleMargin = new Thickness(8, 0);
        SearchMargin = new Thickness(8, 8, 8, 0);
        ListPadding = new Thickness(6);
        RowMinHeight = 36;
        RowPadding = new Thickness(12, 6, 10, 6);
        RowMargin = new Thickness(0, 0, 0, 2);
        RowCheckMargin = new Thickness(8, 0, 0, 0);
        CheckGlyphSize = 16;
        RowFocusRingMargin = new Thickness(0);
        ActionsMargin = new Thickness(12, 0);
        ActionsGap = 8;
        MoveButtonExtent = 36;
        MoveGlyphSize = 16;
        EmptyPadding = new Thickness(16);
        DisabledOpacity = 0.5;
    }
}
