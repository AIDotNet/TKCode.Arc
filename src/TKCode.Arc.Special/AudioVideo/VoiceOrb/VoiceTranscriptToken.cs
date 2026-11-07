namespace TKCode.Arc;

/// <summary>Geometry and type of <see cref="VoiceTranscript"/> (Arc Voice orb, design reference).</summary>
[ControlDesignToken]
internal sealed class VoiceTranscriptToken : AbstractControlDesignToken
{
    /// <summary>The widest a caption gets (30rem).</summary>
    public double CaptionMaxWidth { get; set; }

    /// <summary>The line height of assistant and user lines; the box is two of them tall.</summary>
    public double CaptionLineHeight { get; set; }

    /// <summary>The font size of assistant and user lines.</summary>
    public double CaptionFontSize { get; set; }

    /// <summary>The font size of system lines.</summary>
    public double SystemFontSize { get; set; }

    /// <summary>The line height of system lines.</summary>
    public double SystemLineHeight { get; set; }

    /// <summary>The letter spacing of every line.</summary>
    public double CaptionLetterSpacing { get; set; }

    /// <summary>Where the top fade ends, as a share of the box height (the first line's upper half fades out).</summary>
    public double FadeStop { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CaptionMaxWidth = 480;
        CaptionLineHeight = 22.4;
        CaptionFontSize = 16;
        SystemFontSize = 14;
        SystemLineHeight = 19.6;
        CaptionLetterSpacing = -0.16;
        FadeStop = 0.4;
    }
}
