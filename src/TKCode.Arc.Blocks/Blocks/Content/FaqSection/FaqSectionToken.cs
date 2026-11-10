using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of the <see cref="FaqSection"/> block (faq-section.module.css).</summary>
[ControlDesignToken]
internal sealed class FaqSectionToken : AbstractControlDesignToken
{
    /// <summary>The accordion and search layouts are at most 720 wide.</summary>
    public double StackMaxWidth { get; set; }

    /// <summary>The two-column layout is at most 1120 wide.</summary>
    public double ColumnsMaxWidth { get; set; }

    /// <summary>A question's trigger is at least 64 high.</summary>
    public double TriggerMinHeight { get; set; }

    /// <summary>The trigger's padding: 16 above and below, 4 at the sides.</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>The answer's padding: 40 on the right, under the turning icon, and 20 below.</summary>
    public Thickness AnswerPadding { get; set; }

    /// <summary>The answer's padding when the block is at most 480 wide.</summary>
    public Thickness AnswerPaddingNarrow { get; set; }

    /// <summary>The padding of the excerpt that shows a match inside a closed answer.</summary>
    public Thickness HintPadding { get; set; }

    /// <summary>A category button is 40 high.</summary>
    public double CategoryHeight { get; set; }



    /// <summary>The contact row's padding.</summary>
    public Thickness ContactPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        StackMaxWidth = 720;
        ColumnsMaxWidth = 1120;
        TriggerMinHeight = 64;
        TriggerPadding = new Thickness(4, 16);
        AnswerPadding = new Thickness(4, 0, 40, 20);
        AnswerPaddingNarrow = new Thickness(4, 0, 8, 20);
        HintPadding = new Thickness(4, 0, 40, 16);
        CategoryHeight = 40;
        ContactPadding = new Thickness(20, 16);
    }
}
