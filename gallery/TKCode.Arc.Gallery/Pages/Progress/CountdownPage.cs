using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class CountdownPage
{
    public static Control Create()
    {
        var launch = new Countdown { Target = DateTimeOffset.UtcNow + new TimeSpan(3, 7, 42, 18) };
        var deadline = new Countdown { Target = DateTimeOffset.UtcNow + new TimeSpan(0, 5, 12, 40), Variant = CountdownVariant.Compact };
        var hours = new Countdown
        {
            Target = DateTimeOffset.UtcNow + new TimeSpan(1, 2, 30, 0),
            Units = CountdownUnits.Hours | CountdownUnits.Minutes,
            Variant = CountdownVariant.Compact
        };

        var timer = new Countdown { Duration = TimeSpan.FromSeconds(12), Units = CountdownUnits.Minutes | CountdownUnits.Seconds };
        var status = GalleryUi.Text("Counting down 12 seconds.", ArcDesignTokenKind.TextSecondary);
        timer.Completed += (_, _) => status.Text = "Completed fired.";
        var pause = new Button { Content = "Pause", Variant = ButtonVariant.Secondary };
        pause.Click += (_, _) =>
        {
            timer.IsPaused = !timer.IsPaused;
            pause.Content = timer.IsPaused ? "Resume" : "Pause";
        };
        var restart = new Button { Content = "Restart", Variant = ButtonVariant.Ghost };
        restart.Click += (_, _) =>
        {
            timer.Restart();
            timer.IsPaused = false;
            pause.Content = "Pause";
            status.Text = "Counting down 12 seconds.";
        };

        var wallTime = new Countdown
        {
            Target = Countdown.ResolveTarget("2030-01-01T09:00:00", TimeZoneInfo.Utc),
            Variant = CountdownVariant.Compact,
            Label = "Keynote starts in"
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Launch", "Display digits sized to the card; every digit is a wheel that turns the short way round.", launch),
            GalleryUi.Card("Compact", "One line for banners and table cells.", GalleryUi.Row(deadline, hours, wallTime)),
            GalleryUi.Card("Duration with pause", "A duration countdown can pause and resume; at zero the digits morph into a live label with a pulsing dot.",
                GalleryUi.Column(16, timer, GalleryUi.Row(pause, restart), status)));
    }
}
