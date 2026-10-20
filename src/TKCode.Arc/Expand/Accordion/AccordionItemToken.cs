using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class AccordionItemToken : AbstractControlDesignToken
{
    public Thickness RowBorderThickness { get; set; }

    /// <summary>Arc md trigger: padding space-2 0, gap space-5.</summary>
    public Thickness TriggerPadding { get; set; }

    public Thickness ChevronMargin { get; set; }

    public double ChevronSize { get; set; }

    public double TriggerLineHeight { get; set; }

    /// <summary>Arc md answer: padding 0 space-8 space-5 0.</summary>
    public Thickness AnswerPadding { get; set; }

    public double AnswerLineHeight { get; set; }

    /// <summary>Arc lg trigger: min-height 76px, padding-block space-5, gap space-8.</summary>
    public double TriggerMinHeightLG { get; set; }

    public Thickness TriggerPaddingLG { get; set; }

    public Thickness ChevronMarginLG { get; set; }

    public double TriggerLineHeightLG { get; set; }

    /// <summary>Arc lg answer: line-height 1.65, max-width 62ch, padding 0 space-12 space-6 0.</summary>
    public Thickness AnswerPaddingLG { get; set; }

    public double AnswerLineHeightLG { get; set; }

    public double AnswerMaxWidthLG { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowBorderThickness = new Thickness(0, 0, 0, 1);
        TriggerPadding = new Thickness(0, 8);
        ChevronMargin = new Thickness(20, 0, 0, 0);
        ChevronSize = 17;
        TriggerLineHeight = 14 * 1.4;
        AnswerPadding = new Thickness(0, 0, 32, 20);
        AnswerLineHeight = 14 * 1.4;
        TriggerMinHeightLG = 76;
        TriggerPaddingLG = new Thickness(0, 20);
        ChevronMarginLG = new Thickness(32, 0, 0, 0);
        TriggerLineHeightLG = 18 * 1.4;
        AnswerPaddingLG = new Thickness(0, 0, 48, 24);
        AnswerLineHeightLG = 16 * 1.65;
        // 62ch at text-base: the "0" advance of the body font is about 0.6em.
        AnswerMaxWidthLG = 62 * 16 * 0.6;
        FocusRingMargin = new Thickness(-4, -2);
        FocusRingRadius = new CornerRadius(10);
    }
}
