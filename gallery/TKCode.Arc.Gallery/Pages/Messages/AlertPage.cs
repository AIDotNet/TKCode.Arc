using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class AlertPage
{
    public static Control Create()
    {
        var tones = GalleryUi.Column(12,
            new Alert { Title = "Your card expires next month", Content = "Update your payment method to keep your plan active." },
            new Alert { Tone = AlertTone.Success, Title = "Domain verified", Content = "Emails from acme.dev will now be delivered." },
            new Alert { Tone = AlertTone.Warning, Title = "You're close to your storage limit", Content = "Free up space or upgrade before uploads pause." },
            new Alert { Tone = AlertTone.Danger, Title = "Payment failed", Content = "We couldn't charge the card ending in 4242." });

        // One instance that morphs between states instead of being replaced.
        var status = new Alert { Title = "Ready to sync", Content = "Changes are saved on this device." };
        var save = new Button { Content = "Save", Variant = ButtonVariant.Secondary };
        save.Click += async (_, _) =>
        {
            status.Tone = AlertTone.Info;
            status.Title = "Saving changes";
            status.Content = "Uploading 3 files to the workspace.";
            await Task.Delay(1400);
            status.Tone = AlertTone.Success;
            status.Title = "All changes saved";
            status.Content = "Everyone in the workspace now sees the latest version, including the comments you left on the draft.";
        };
        var fail = new Button { Content = "Fail", Variant = ButtonVariant.Secondary };
        fail.Click += (_, _) =>
        {
            status.Tone = AlertTone.Danger;
            status.Title = "Couldn't save";
            status.Content = "Check your connection and try again.";
        };

        var dismissible = new Alert
        {
            Tone = AlertTone.Warning,
            Title = "Scheduled maintenance on Sunday",
            Content = "The dashboard is read-only from 02:00 to 03:00 UTC.",
            IsDismissible = true
        };
        var below = GalleryUi.Text("Content below closes the gap when the alert collapses.", DesignTokens.ArcDesignTokenKind.TextSecondary);
        var restore = new Button { Content = "Show again", Variant = ButtonVariant.Ghost };
        restore.Click += (_, _) => dismissible.IsOpen = true;

        var rich = new Alert
        {
            Title = "A new version is available",
            Content = GalleryUi.Row(new Button { Content = "Reload", Size = ControlSize.Small, Variant = ButtonVariant.Secondary })
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Tones", "Info, success, warning and danger pick the icon and its color; danger is announced assertively.", tones),
            GalleryUi.Card("Morph in place", "Keep one alert and change its tone, title and description: the icon pops, new text rises in and the height springs.",
                GalleryUi.Column(16, status, GalleryUi.Row(save, fail))),
            GalleryUi.Card("Dismiss", "The dismiss button collapses the alert while it fades, then raises Dismissed.",
                GalleryUi.Column(12, dismissible, below, GalleryUi.Row(restore))),
            GalleryUi.Card("Custom content", "Any non-text content is shown under the title.", rich));
    }
}
