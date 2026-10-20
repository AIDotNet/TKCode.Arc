using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class DatePickerPage
{
    public static Control Create()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = new DatePicker
        {
            Label = "Start date",
            Description = "Type a date or press Alt+Down to open the calendar.",
            Culture = CultureInfo.GetCultureInfo("en-US"),
            SelectedDate = today,
            Width = 280,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var picked = GalleryUi.Text($"SelectedDate: {start.SelectedDate:yyyy-MM-dd}", ArcDesignTokenKind.TextSecondary, 13);
        start.SelectedDateChanged += (_, e) => picked.Text = $"SelectedDate: {(e.NewDate is { } d ? d.ToString("yyyy-MM-dd") : "none")}";

        var due = new DatePicker
        {
            Label = "Due date",
            Description = "Weekdays in the next eight weeks.",
            Culture = CultureInfo.GetCultureInfo("en-US"),
            MinDate = today,
            MaxDate = today.AddDays(56),
            DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            ShowToday = true,
            Width = 280,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var sizes = GalleryUi.Column(16,
            new DatePicker { Label = "Small", Size = ControlSize.Small, SelectedDate = today, Width = 260, HorizontalAlignment = HorizontalAlignment.Left },
            new DatePicker { Label = "Medium", SelectedDate = today, Width = 260, HorizontalAlignment = HorizontalAlignment.Left },
            new DatePicker { Label = "Large", Size = ControlSize.Large, SelectedDate = today, Width = 260, HorizontalAlignment = HorizontalAlignment.Left });

        var cultures = GalleryUi.Column(16,
            new DatePicker { Label = "Deutsch", Culture = CultureInfo.GetCultureInfo("de-DE"), FirstDayOfWeek = DayOfWeek.Monday, SelectedDate = today, Width = 260, HorizontalAlignment = HorizontalAlignment.Left },
            new DatePicker { Label = "中文", Culture = CultureInfo.GetCultureInfo("zh-CN"), FirstDayOfWeek = DayOfWeek.Monday, SelectedDate = today, Width = 260, HorizontalAlignment = HorizontalAlignment.Left },
            new DatePicker { Label = "ISO format", DisplayFormat = "yyyy-MM-dd", SelectedDate = today, Width = 260, HorizontalAlignment = HorizontalAlignment.Left });

        var states = GalleryUi.Column(16,
            new DatePicker { Label = "Invoice date", ErrorMessage = "Pick a date before the due date.", SelectedDate = today, Width = 280, HorizontalAlignment = HorizontalAlignment.Left },
            new DatePicker { Label = "Locked", IsEnabled = false, SelectedDate = today, Width = 280, HorizontalAlignment = HorizontalAlignment.Left });

        return GalleryUi.Column(20,
            GalleryUi.Card("Date field",
                "Click the field or press Alt+Down to open the calendar; arrows move between days, Enter picks, Escape returns to the field. Changed parts of the date roll in time's direction.",
                GalleryUi.Column(12, start, picked)),
            GalleryUi.Card("Limits and disabled days", "MinDate, MaxDate and DisabledDates reach the calendar and reject typed dates (the field shakes and reverts).", due),
            GalleryUi.Card("Sizes", "Small 36, Medium 44 (Arc), Large 50 from the field foundation.", sizes),
            GalleryUi.Card("Cultures and formats", "Formatting, parsing, month names and weekday initials follow Culture; DisplayFormat takes a .NET date pattern.", cultures),
            GalleryUi.Card("Invalid and disabled", "An error message turns the border red; a disabled field fades to half opacity.", states));
    }
}
