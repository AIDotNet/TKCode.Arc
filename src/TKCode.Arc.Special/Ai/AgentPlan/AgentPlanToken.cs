using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of the <see cref="AgentPlan"/> card (Arc Agent plan, design reference): the widest card, the padding of its
/// header, step list and footer (each tighter under 420px), the header's inner gap and the rule heights. Colors, radii, type and
/// shadows come from the ArcDesign tokens in the theme; the row grid (20 | 1fr | 48, gap 12, padding 8 12) lives in the row.
/// </summary>
[ControlDesignToken]
internal sealed class AgentPlanToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (560); it is as wide as its container below that.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Header padding: 16 20 15 (16 16 15 narrow).</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness NarrowHeaderPadding { get; set; }

    /// <summary>Space around the step list: 6 8 (6 4 narrow).</summary>
    public Thickness ListPadding { get; set; }

    public Thickness NarrowListPadding { get; set; }

    /// <summary>Footer padding: 11 20 12 under the 1px rule (11 16 12 narrow).</summary>
    public Thickness FooterPadding { get; set; }

    public Thickness NarrowFooterPadding { get; set; }

    /// <summary>The goal line starts 32 in, under the heading (the mark's slot 20 plus the 12 gap).</summary>
    public Thickness GoalMargin { get; set; }

    /// <summary>Gap between the header's mark, heading and meta: 12.</summary>
    public double HeaderGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 560;
        HeaderPadding = new Thickness(20, 16, 20, 15);
        NarrowHeaderPadding = new Thickness(16, 16, 16, 15);
        ListPadding = new Thickness(8, 6);
        NarrowListPadding = new Thickness(4, 6);
        FooterPadding = new Thickness(20, 11, 20, 12);
        NarrowFooterPadding = new Thickness(16, 11, 16, 12);
        GoalMargin = new Thickness(32, 2, 0, 0);
        HeaderGap = 12;
    }
}
