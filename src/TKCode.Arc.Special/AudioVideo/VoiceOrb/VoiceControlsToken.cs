using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="VoiceControls"/> (Arc Voice orb, design reference).</summary>
[ControlDesignToken]
internal sealed class VoiceControlsToken : AbstractControlDesignToken
{
    /// <summary>The pill's height inside its 1px border (48; 50 with the border).</summary>
    public double PillInnerHeight { get; set; }

    /// <summary>The round buttons' size.</summary>
    public double RoundButtonSize { get; set; }

    /// <summary>The gap between the live controls.</summary>
    public double LiveGap { get; set; }

    /// <summary>The live group's padding.</summary>
    public Thickness LivePadding { get; set; }

    /// <summary>The start button's padding (16 before the icon, 20 after the text).</summary>
    public Thickness StartPadding { get; set; }

    /// <summary>The gap between the start icon and its text.</summary>
    public double StartGap { get; set; }

    /// <summary>The timer's padding.</summary>
    public Thickness ClockPadding { get; set; }

    /// <summary>The glyphs' size.</summary>
    public double GlyphSize { get; set; }

    /// <summary>The end button's danger fill at rest (12 % light, 16 % dark).</summary>
    public double EndFillOpacity { get; set; }

    /// <summary>The end button's danger fill under the pointer.</summary>
    public double EndFillHoverOpacity { get; set; }

    /// <summary>The focus ring's margin around a round button (2px outside its edge, inside the pill).</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>The focus ring's margin on the start button, which fills the pill: 2px inside its edge so the pill does not clip it.</summary>
    public Thickness StartFocusRingMargin { get; set; }

    /// <summary>The focus ring's corner radius around a round button.</summary>
    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PillInnerHeight = 48;
        RoundButtonSize = 40;
        LiveGap = 4;
        LivePadding = new Thickness(4, 0);
        StartPadding = new Thickness(16, 0, 20, 0);
        StartGap = 8;
        ClockPadding = new Thickness(8, 0);
        GlyphSize = 18;
        EndFillOpacity = isDarkMode ? 0.16 : 0.12;
        EndFillHoverOpacity = isDarkMode ? 0.24 : 0.18;
        FocusRingMargin = new Thickness(-4);
        StartFocusRingMargin = new Thickness(2);
        FocusRingRadius = new CornerRadius(9999);
    }
}
