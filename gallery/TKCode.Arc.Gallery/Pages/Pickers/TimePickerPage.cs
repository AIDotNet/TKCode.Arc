using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class TimePickerPage
{
    public static Control Create()
    {
        var meeting = new TimePicker
        {
            Label = "Meeting time",
            Description = "Times every 15 minutes, shown on the clock of your locale.",
            Culture = CultureInfo.GetCultureInfo("en-US"),
            Width = 240,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var value = GalleryUi.Text("SelectedTime: 09:00", ArcDesignTokenKind.TextSecondary, 13);
        meeting.SelectedTimeChanged += (_, e) => value.Text = $"SelectedTime: {(e.NewTime is { } t ? t.ToString("HH:mm") : "none")}";

        var steps = GalleryUi.Column(16,
            new TimePicker { Label = "Every 5 minutes", MinuteStep = 5, Width = 240, HorizontalAlignment = HorizontalAlignment.Left },
            new TimePicker { Label = "Every 30 minutes, 24-hour", MinuteStep = 30, ClockFormat = TimePickerClockFormat.TwentyFourHour, SelectedTime = new TimeOnly(18, 30), Width = 240, HorizontalAlignment = HorizontalAlignment.Left },
            new TimePicker { Label = "Reminder", SelectedTime = null, Width = 240, HorizontalAlignment = HorizontalAlignment.Left });

        var states = GalleryUi.Column(16,
            new TimePicker { Label = "Small", Size = ControlSize.Small, Width = 240, HorizontalAlignment = HorizontalAlignment.Left },
            new TimePicker { Label = "Check-in", ErrorMessage = "Check-in opens at 3:00 PM.", SelectedTime = new TimeOnly(11, 0), Width = 240, HorizontalAlignment = HorizontalAlignment.Left },
            new TimePicker { Label = "Locked", IsEnabled = false, Width = 240, HorizontalAlignment = HorizontalAlignment.Left });

        return GalleryUi.Column(20,
            GalleryUi.Card("Time field",
                "Arrow keys open the list and move between times (wrapping at both ends), Enter or Space chooses, Escape closes. The list opens centred on the chosen time.",
                GalleryUi.Column(12, meeting, value)),
            GalleryUi.Card("Steps and clocks", "MinuteStep sets the options; ClockFormat overrides the culture's 12- or 24-hour clock. Without a value the placeholder shows.", steps),
            GalleryUi.Card("Sizes and states", "Field sizes, an error message and the disabled state come from the field foundation.", states));
    }
}
