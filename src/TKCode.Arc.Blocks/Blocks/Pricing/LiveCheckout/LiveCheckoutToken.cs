using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and motion constants of <see cref="LiveCheckout"/> (Arc LiveCheckout, design reference; values measured on its
/// preview). Colors come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class LiveCheckoutToken : AbstractControlDesignToken
{
    /// <summary>Block width from which the form and the summary sit side by side.</summary>
    public double TwoColumnWidth { get; set; }

    /// <summary>Block width below which plans, wallets and billing fields stack in one column.</summary>
    public double StackWidth { get; set; }

    /// <summary>Gap between the form and the summary column.</summary>
    public double ColumnGap { get; set; }

    /// <summary>Width of the summary column (and of the centred receipt in two-column layouts).</summary>
    public double SummaryWidth { get; set; }

    /// <summary>Gap between the folded summary and the form in one column.</summary>
    public double NarrowGap { get; set; }

    /// <summary>Distance the sticky summary keeps from the top of the scrolled view.</summary>
    public double StickyTop { get; set; }

    /// <summary>Gap between form sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>Gap inside a section (heading to content).</summary>
    public double SectionInnerGap { get; set; }

    /// <summary>Gap between plan cards, wallet buttons and grid fields.</summary>
    public double TileGap { get; set; }

    /// <summary>Gap between billing and card fields.</summary>
    public double FieldGap { get; set; }

    /// <summary>Gap between a field's label row and its input.</summary>
    public double LabelGap { get; set; }


    /// <summary>Plan card padding.</summary>
    public Thickness PlanPadding { get; set; }

    /// <summary>Seat card and add-on row padding.</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>Summary padding in two columns.</summary>
    public Thickness SummaryPadding { get; set; }

    /// <summary>Summary padding in one column.</summary>
    public Thickness NarrowSummaryPadding { get; set; }

    /// <summary>Wallet and input height.</summary>
    public double ControlHeight { get; set; }

    /// <summary>Pay button height.</summary>
    public double PayHeight { get; set; }


    /// <summary>How far the form moves left while it leaves after a payment.</summary>
    public double FormExitOffset { get; set; }

    /// <summary>How far the receipt heading rises into place.</summary>
    public double HeadRise { get; set; }

    /// <summary>How far the receipt details rise into place.</summary>
    public double MetaRise { get; set; }

    /// <summary>Delay before the receipt heading rises.</summary>
    public TimeSpan HeadRiseDelay { get; set; }

    /// <summary>Delay before the receipt details rise.</summary>
    public TimeSpan MetaRiseDelay { get; set; }

    /// <summary>Delay before the receipt check mark draws.</summary>
    public TimeSpan CheckDelay { get; set; }

    /// <summary>How long the receipt check mark takes to draw.</summary>
    public TimeSpan CheckDuration { get; set; }

    /// <summary>Focus ring margin around buttons and plan cards (outside, 2px offset).</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TwoColumnWidth = 760;
        StackWidth = 520;
        ColumnGap = 32;
        SummaryWidth = 380;
        NarrowGap = 20;
        StickyTop = 24;
        SectionGap = 32;
        SectionInnerGap = 16;
        TileGap = 8;
        FieldGap = 12;
        LabelGap = 6;
        PlanPadding = new Thickness(16);
        RowPadding = new Thickness(16, 12);
        SummaryPadding = new Thickness(24);
        NarrowSummaryPadding = new Thickness(20, 16);
        ControlHeight = 44;
        PayHeight = 50;
        FormExitOffset = 24;
        HeadRise = 8;
        MetaRise = 12;
        HeadRiseDelay = TimeSpan.FromMilliseconds(75);
        MetaRiseDelay = TimeSpan.FromMilliseconds(176);
        CheckDelay = TimeSpan.FromMilliseconds(320);
        CheckDuration = TimeSpan.FromMilliseconds(470);
        FocusRingMargin = new Thickness(-4);
    }
}
