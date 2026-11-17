using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Geometry of <see cref="TextEditMenu"/>: a row of 44px actions with 14px side padding that sits 8px above the
/// field; the hairlines between actions are the ArcDesign <c>EdgeThicknessLeft</c>.
/// </summary>
[ControlDesignToken]
internal sealed class TextEditMenuToken : AbstractControlDesignToken
{
    /// <summary>Height of an action, the touch target: 44.</summary>
    public double ItemHeight { get; set; }

    /// <summary>Padding inside an action: 14 at the sides.</summary>
    public Thickness ItemPadding { get; set; }

    /// <summary>Gap between the menu and the top of the field when the menu opens at a press: 8.</summary>
    public double AnchorGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ItemHeight = 44;
        ItemPadding = new Thickness(14, 0);
        AnchorGap = 8;
    }
}
