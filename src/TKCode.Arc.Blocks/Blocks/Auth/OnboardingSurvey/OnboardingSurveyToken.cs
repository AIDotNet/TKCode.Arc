using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="OnboardingSurvey"/>.</summary>
[ControlDesignToken]
internal sealed class OnboardingSurveyToken : AbstractControlDesignToken
{
    /// <summary>Space inside the card.</summary>
    public Thickness FrameInset { get; set; }

    /// <summary>Space inside the card below <see cref="NarrowWidth"/>.</summary>
    public Thickness FrameInsetNarrow { get; set; }

    /// <summary>The card's default maximum width.</summary>
    public double PreferredWidth { get; set; }

    /// <summary>Below this width the buttons fill the row and the padding tightens.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Space under the progress bar.</summary>
    public Thickness ProgressMargin { get; set; }

    /// <summary>Line height of question titles.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Line height of body text.</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Space between a question and its description.</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Space between a question's text and its answer controls.</summary>
    public Thickness BodyMargin { get; set; }

    /// <summary>Gap between checkbox options.</summary>
    public double OptionGap { get; set; }

    /// <summary>Space above the buttons.</summary>
    public Thickness ActionsMargin { get; set; }

    /// <summary>Height of the text answer box.</summary>
    public double TextHeight { get; set; }

    /// <summary>Diameter of the mark on the thank-you.</summary>
    public double ResultMark { get; set; }

    /// <summary>Size of the drawn check inside the mark.</summary>
    public double ResultGlyph { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FrameInset = new Thickness(28);
        FrameInsetNarrow = new Thickness(20, 22);
        PreferredWidth = 540;
        NarrowWidth = 440;
        ProgressMargin = new Thickness(0, 10, 0, 22);
        TitleLineHeight = 28;
        BodyLineHeight = 20;
        DescriptionMargin = new Thickness(0, 6, 0, 0);
        BodyMargin = new Thickness(0, 18, 0, 0);
        OptionGap = 12;
        ActionsMargin = new Thickness(0, 26, 0, 0);
        TextHeight = 112;
        ResultMark = 56;
        ResultGlyph = 28;
    }
}
