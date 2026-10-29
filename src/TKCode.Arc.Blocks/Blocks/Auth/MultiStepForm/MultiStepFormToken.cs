using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of the <see cref="MultiStepForm"/> block (measured on Arc's preview; accent-free).</summary>
[ControlDesignToken]
internal sealed class MultiStepFormToken : AbstractControlDesignToken
{
    /// <summary>The card is 540 wide, or narrower when its container is.</summary>
    public double CardWidth { get; set; }

    /// <summary>The progress header's padding: 16 20 (narrow 12 16).</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness NarrowHeaderPadding { get; set; }

    /// <summary>Between the progress list and the count: 16 beside it (narrow 8 above it).</summary>
    public double HeaderGap { get; set; }

    public double NarrowHeaderGap { get; set; }

    /// <summary>Between two progress items: 16 (narrow 12).</summary>
    public double ItemGap { get; set; }

    public double NarrowItemGap { get; set; }

    /// <summary>The body under the header: 24 above the step, 20 at the sides and below (narrow 20 / 16 / 16).</summary>
    public Thickness BodyPadding { get; set; }

    public Thickness NarrowBodyPadding { get; set; }

    /// <summary>The step area is at least 224 high (narrow 240).</summary>
    public double ViewportMinHeight { get; set; }

    public double NarrowViewportMinHeight { get; set; }

    /// <summary>Above the buttons: 24 (narrow 20).</summary>
    public Thickness ActionsMargin { get; set; }

    public Thickness NarrowActionsMargin { get; set; }

    /// <summary>Between Back and the submit button.</summary>
    public double ActionsGap { get; set; }

    /// <summary>The progress marker: a 20px circle.</summary>
    public double MarkerSize { get; set; }

    /// <summary>The marker's number and label size (12) and line height (16.8).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>The step heading: 18 on a 25.2 line.</summary>
    public double LegendFontSize { get; set; }

    public double LegendLineHeight { get; set; }

    /// <summary>The step description: 14 on a 19.6 line, 8 under the heading and 20 above the fields.</summary>
    public Thickness DescriptionMargin { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>The fields sit 20 under the heading when the step has no description.</summary>
    public Thickness ContentMarginNoDescription { get; set; }

    /// <summary>Back and submit buttons: 36 high, padding 0 12, radius 18, 8 between icon and label.</summary>
    public double ButtonHeight { get; set; }

    public Thickness ButtonPadding { get; set; }

    public CornerRadius ButtonRadius { get; set; }

    public double ButtonIconGap { get; set; }

    /// <summary>The keyboard ring around a button: 2 px, 2 px outside, so the ring box is 4 px larger and its radius 22.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    /// <summary>The success check cell (32) and the check inside it (18); the title is 18 on a 19.8 line, −0.54 tracking.</summary>
    public double SuccessIconSize { get; set; }

    public double SuccessGlyphSize { get; set; }

    public double SuccessTitleLineHeight { get; set; }

    public double SuccessTitleTracking { get; set; }

    /// <summary>A disabled Back button is drawn at 45% opacity.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardWidth = 540;
        HeaderPadding = new Thickness(20, 16);
        NarrowHeaderPadding = new Thickness(16, 12);
        HeaderGap = 16;
        NarrowHeaderGap = 8;
        ItemGap = 16;
        NarrowItemGap = 12;
        BodyPadding = new Thickness(20, 24, 20, 20);
        NarrowBodyPadding = new Thickness(16, 20, 16, 16);
        ViewportMinHeight = 224;
        NarrowViewportMinHeight = 240;
        ActionsMargin = new Thickness(0, 24, 0, 0);
        NarrowActionsMargin = new Thickness(0, 20, 0, 0);
        ActionsGap = 12;
        MarkerSize = 20;
        SmallLineHeight = 16.8;
        LegendFontSize = 18;
        LegendLineHeight = 25.2;
        DescriptionMargin = new Thickness(0, 8, 0, 20);
        BodyLineHeight = 19.6;
        ContentMarginNoDescription = new Thickness(0, 20, 0, 0);
        ButtonHeight = 36;
        ButtonPadding = new Thickness(12, 0);
        ButtonRadius = new CornerRadius(18);
        ButtonIconGap = 8;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(22);
        SuccessIconSize = 32;
        SuccessGlyphSize = 18;
        SuccessTitleLineHeight = 19.8;
        SuccessTitleTracking = -0.54;
        DisabledOpacity = 0.45;
    }
}
