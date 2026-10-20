using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class CalendarPage
{
    public static Control Create()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var picked = GalleryUi.Text("Pick a day", Arc.DesignTokens.ArcDesignTokenKind.TextSecondary);
        var basic = new Calendar { SelectedDate = today, ShowToday = true };
        basic.DateSelected += (_, e) => picked.Text = $"Selected {e.Date:D}";

        var limited = new Calendar
        {
            MinDate = today.AddDays(-3),
            MaxDate = today.AddDays(45),
            DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            FirstDayOfWeek = DayOfWeek.Monday
        };
        return GalleryUi.Column(20,
            GalleryUi.Card("Single date", "Months slide on one strip; the selected disc glides and each number flips as the disc passes. Try arrows, PageUp/PageDown and Shift+PageDown.",
                GalleryUi.Column(12, basic, picked)),
            GalleryUi.Card("Limits and disabled days", "Weekdays only, from three days ago to six weeks ahead; Monday first.", limited));
    }
}
