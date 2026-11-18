using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent inks of <see cref="CelebrateButton"/> (Arc Celebrate button, design reference): a
/// 40 px capsule with 20 px side padding, a 13 px medium label with an 8 px gap to its 14 px spinner or 16 px check, and
/// the confetti's reach around the button (150 px beside, 190 px above, 110 px below).
/// The fill and label colors follow the accent and come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CelebrateButtonToken : AbstractControlDesignToken
{
    /// <summary>The button's height (40).</summary>
    public double ButtonHeight { get; set; }

    /// <summary>The space left and right of the label (0 20).</summary>
    public Thickness ButtonPadding { get; set; }

    /// <summary>The label's size (13).</summary>
    public double LabelFontSize { get; set; }

    /// <summary>The label's letter spacing (-0.13, -0.01 em).</summary>
    public double LabelLetterSpacing { get; set; }

    /// <summary>The label's weight (medium).</summary>
    public FontWeight LabelFontWeight { get; set; }

    /// <summary>The gap between the spinner or check and the text (8).</summary>
    public double IconGap { get; set; }

    /// <summary>The pending spinner's size (14) and ring width (1).</summary>
    public double SpinnerSize { get; set; }

    public double SpinnerThickness { get; set; }

    /// <summary>The success check's size (16).</summary>
    public double CheckSize { get; set; }

    /// <summary>How far the confetti may fly left and right of the button (150), above it (190) and below it (110).</summary>
    public double ConfettiReachSide { get; set; }

    public double ConfettiReachTop { get; set; }

    public double ConfettiReachBottom { get; set; }

    /// <summary>The sunny yellow mixed into the default confetti (#F8CC38 in both appearances).</summary>
    public Color ConfettiYellow { get; set; }

    /// <summary>The ring around the focused button, outside its edge (-4) with a capsule radius.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    /// <summary>The control's opacity while disabled (0.52, as Button).</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ButtonHeight = 40;
        ButtonPadding = new Thickness(20, 0);
        LabelFontSize = 13;
        LabelLetterSpacing = -0.13;
        LabelFontWeight = FontWeight.Medium;
        IconGap = 8;
        SpinnerSize = 14;
        SpinnerThickness = 1;
        CheckSize = 16;
        ConfettiReachSide = 150;
        ConfettiReachTop = 190;
        ConfettiReachBottom = 110;
        ConfettiYellow = Color.FromRgb(0xF8, 0xCC, 0x38);
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(999);
        DisabledOpacity = 0.52;
    }
}
