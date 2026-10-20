using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class DateRangePickerPage
{
    public static Control Create()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var report = new DateRangePicker
        {
            Culture = CultureInfo.GetCultureInfo("en-US"),
            SelectedRange = new DateRange(today.AddDays(-6), today)
        };
        var applied = GalleryUi.Text($"Applied: {report.FormattedValue}", ArcDesignTokenKind.TextSecondary, 13);
        report.SelectedRangeChanged += (_, e) => applied.Text = $"Applied: {report.FormattedValue}";

        var booking = new DateRangePicker
        {
            Label = "Stay",
            IsLabelVisible = true,
            Description = "Check-in from today, up to six months ahead.",
            Culture = CultureInfo.GetCultureInfo("en-US"),
            MinDate = today,
            MaxDate = today.AddMonths(6),
            FirstDayOfWeek = DayOfWeek.Monday,
            Presets =
            [
                new DateRangePreset("This weekend", t => WeekendFrom(t)),
                new DateRangePreset("Next week", t => new DateRange(t.AddDays(7 - (int)t.DayOfWeek + 1), t.AddDays(7 - (int)t.DayOfWeek + 7))),
                new DateRangePreset("Two weeks", t => new DateRange(t, t.AddDays(13)))
            ]
        };

        var compact = new DateRangePicker { MonthCount = DateRangePickerMonths.One, Culture = CultureInfo.GetCultureInfo("en-US") };
        var chinese = new DateRangePicker { Culture = CultureInfo.GetCultureInfo("zh-CN"), FirstDayOfWeek = DayOfWeek.Monday, Placeholder = "选择日期" };

        return GalleryUi.Column(20,
            GalleryUi.Card("Report range",
                "The trigger grows into two months beside the presets. Hover after picking a start to stretch the range; Apply flies the label back into the trigger. Arrows, Home/End and PageUp/PageDown (Shift for a year) move between days.",
                GalleryUi.Column(12, report, applied)),
            GalleryUi.Card("Limits, Monday first and custom presets", "MinDate and MaxDate disable days and month buttons; the preset rail takes any ranges.", booking),
            GalleryUi.Card("One month", "MonthCount=One forces the compact panel: one month under a scrolling preset row (Auto picks it below 712px).", compact),
            GalleryUi.Card("Culture", "Month titles, weekday heads and the range label follow Culture.", chinese));
    }

    private static DateRange WeekendFrom(DateOnly today)
    {
        var saturday = today.AddDays(((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7);
        return new DateRange(saturday, saturday.AddDays(1));
    }
}
