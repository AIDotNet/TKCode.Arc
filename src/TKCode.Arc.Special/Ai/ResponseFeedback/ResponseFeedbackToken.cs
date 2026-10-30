using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ResponseFeedback"/>: the card's radius and its padding (16px below 480px of width, 24 above), the
/// space between the prompt, the reply and the row, the prompt bubble, the model's mark and the reply's type. Colors and
/// fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ResponseFeedbackToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card's padding from 480px of width up.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The card's padding below 480px of width.</summary>
    public Thickness CompactCardPadding { get; set; }

    /// <summary>The space between the prompt, the reply and the action row.</summary>
    public double SectionGap { get; set; }

    /// <summary>The prompt bubble: its padding, radius and the space it leaves on the left.</summary>
    public Thickness PromptPadding { get; set; }

    public CornerRadius PromptCornerRadius { get; set; }

    public Thickness PromptInset { get; set; }

    /// <summary>The model's mark and the gap to its name.</summary>
    public double MarkSize { get; set; }

    public double ModelGap { get; set; }

    /// <summary>The space between the model line and the reply.</summary>
    public double ModelBodyGap { get; set; }

    /// <summary>The reply's line height.</summary>
    public double BodyLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(24);
        CompactCardPadding = new Thickness(16);
        SectionGap = 16;
        PromptPadding = new Thickness(14, 9);
        PromptCornerRadius = new CornerRadius(18);
        PromptInset = new Thickness(48, 0, 0, 0);
        MarkSize = 16;
        ModelGap = 8;
        ModelBodyGap = 10;
        BodyLineHeight = 22;
    }
}
