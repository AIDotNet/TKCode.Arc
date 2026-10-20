using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class HoverCardProfileToken : AbstractControlDesignToken
{
    public double RowGap { get; set; }
    public double HeadGap { get; set; }
    public double StatColumnGap { get; set; }
    public double StatRowGap { get; set; }
    public Thickness StatsPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowGap = 12;
        HeadGap = 12;
        StatColumnGap = 24;
        StatRowGap = 8;
        StatsPadding = new Thickness(0, 12, 0, 0);
    }
}
