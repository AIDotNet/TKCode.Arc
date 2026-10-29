using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CalendarToken : AbstractControlDesignToken
{
    public double HeaderMinHeight { get; set; }
    public Thickness HeaderMargin { get; set; }
    public Thickness NavigationMargin { get; set; }
    public double NavButtonSize { get; set; }
    public Thickness TodayButtonPadding { get; set; }
    public Thickness WeekdaysMargin { get; set; }
    public double NavDisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderMinHeight = 34;
        HeaderMargin = new Thickness(0, 0, 0, 14);
        NavigationMargin = new Thickness(0, 0, -6, 0);
        NavButtonSize = 32;
        TodayButtonPadding = new Thickness(11, 0);
        WeekdaysMargin = new Thickness(0, 0, 0, 6);
        NavDisabledOpacity = 0.36;
    }
}
