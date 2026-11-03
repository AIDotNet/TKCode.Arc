using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class PropertyGridToken : AbstractControlDesignToken
{
    /// <summary>Room around the search field.</summary>
    public Thickness SearchMargin { get; set; }

    /// <summary>Inset of the rows inside the scroll area.</summary>
    public Thickness ListPadding { get; set; }

    /// <summary>The description area: inset, height and gap between its title and text.</summary>
    public Thickness DescriptionPadding { get; set; }

    public double DescriptionHeight { get; set; }

    public double DescriptionGap { get; set; }

    /// <summary>Inset of the empty-state text.</summary>
    public Thickness EmptyPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SearchMargin = new Thickness(8, 8, 8, 4);
        ListPadding = new Thickness(4, 4, 4, 8);
        DescriptionPadding = new Thickness(14, 10, 14, 12);
        DescriptionHeight = 76;
        DescriptionGap = 2;
        EmptyPadding = new Thickness(16, 24);
    }
}

[ControlDesignToken]
internal sealed class PropertyGridItemToken : AbstractControlDesignToken
{
    /// <summary>Row height: a small field (36) and 2px above and below.</summary>
    public double RowHeight { get; set; }

    public Thickness RowPadding { get; set; }

    /// <summary>Indent of one nesting level.</summary>
    public double IndentStep { get; set; }

    /// <summary>The chevron column before every label (kept empty when the row does not expand, so labels align).</summary>
    public double ExpanderWidth { get; set; }

    public double ChevronSize { get; set; }

    /// <summary>Space between the label and the editor.</summary>
    public double LabelGap { get; set; }

    /// <summary>Width of the drag area on the label column's edge.</summary>
    public double SplitterWidth { get; set; }

    /// <summary>The add and remove buttons of lists.</summary>
    public double ActionSize { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowHeight = 40;
        RowPadding = new Thickness(4, 2, 4, 2);
        IndentStep = 14;
        ExpanderWidth = 18;
        ChevronSize = 14;
        LabelGap = 10;
        SplitterWidth = 7;
        ActionSize = 28;
        FocusRingMargin = new Thickness(-2);
    }
}

[ControlDesignToken]
internal sealed class PropertyGridCategoryItemToken : AbstractControlDesignToken
{
    public double HeaderHeight { get; set; }

    public Thickness HeaderPadding { get; set; }

    public double ChevronSize { get; set; }

    public double ChevronGap { get; set; }

    /// <summary>Space above every header but the first.</summary>
    public Thickness HeaderMargin { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderHeight = 34;
        HeaderPadding = new Thickness(6, 0, 10, 0);
        ChevronSize = 14;
        ChevronGap = 6;
        HeaderMargin = new Thickness(0, 4, 0, 0);
        FocusRingMargin = new Thickness(-2);
    }
}
