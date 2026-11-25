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

    /// <summary>Card variant: padding of the trigger list inside the strip; no bottom padding, so the cards stand on the baseline.</summary>
    public Thickness CardListPadding { get; set; }

    /// <summary>Card variant: corners of the muted strip (rounded on top only).</summary>
    public CornerRadius CardShellRadius { get; set; }

    /// <summary>Card variant: thickness of the baseline under the strip that the selected card breaks.</summary>
    public double CardBaselineThickness { get; set; }

    /// <summary>Card variant: corners of the selected card (rounded on top only, open at the bottom).</summary>
    public CornerRadius CardIndicatorRadius { get; set; }

    /// <summary>Card variant: the scroll buttons cover the cards (below the strip's top padding) and stop above the baseline.</summary>
    public Thickness CardScrollButtonMargin { get; set; }

    /// <summary>Side of the square add button (the trigger height).</summary>
    public double AddButtonSize { get; set; }

    /// <summary>Space around the add button at the end of the list.</summary>
    public Thickness AddButtonMargin { get; set; }

    public CornerRadius AddButtonRadius { get; set; }

    /// <summary>Card variant: side of the square add button.</summary>
    public double CardAddButtonSize { get; set; }

    /// <summary>Card variant: space around the add button (its top margin centres it on the cards).</summary>
    public Thickness CardAddButtonMargin { get; set; }

    /// <summary>Card variant: corners of the add button.</summary>
    public CornerRadius CardAddButtonRadius { get; set; }

    /// <summary>Size of the add button's plus glyph.</summary>
    public double AddGlyphSize { get; set; }

    /// <summary>Margin of the add button's focus ring (outside the button).</summary>
    public Thickness AddFocusRingMargin { get; set; }

    /// <summary>Corners of the add button's focus ring.</summary>
    public CornerRadius AddFocusRingRadius { get; set; }

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
        CardListPadding = new Thickness(6, 6, 6, 0);
        CardShellRadius = new CornerRadius(12, 12, 0, 0);
        CardBaselineThickness = 1;
        CardIndicatorRadius = new CornerRadius(10, 10, 0, 0);
        CardScrollButtonMargin = new Thickness(0, 6, 0, 1);
        AddButtonSize = 36;
        AddButtonMargin = new Thickness(0, 0, 4, 0);
        AddButtonRadius = new CornerRadius(15);
        CardAddButtonSize = 28;
        CardAddButtonMargin = new Thickness(0, 6, 6, 1);
        CardAddButtonRadius = new CornerRadius(8);
        AddGlyphSize = 16;
        AddFocusRingMargin = new Thickness(-2);
        AddFocusRingRadius = new CornerRadius(10);
    }

    public override void ApplyCompact(bool isDarkMode)
    {
        base.ApplyCompact(isDarkMode);
        ListPadding = new Thickness(3);
        ContentMargin = new Thickness(0, 10, 0, 0);
        ContentLineHeight = 13 * 1.38;
        ScrollButtonWidth = 28;
        ScrollGlyphSize = 15;
        PillRadius = new CornerRadius(8);
        ContentFocusRingRadius = new CornerRadius(14);
        CardListPadding = new Thickness(4, 4, 4, 0);
        AddButtonSize = 28;
        CardAddButtonSize = 24;
        AddGlyphSize = 14;
    }
}
