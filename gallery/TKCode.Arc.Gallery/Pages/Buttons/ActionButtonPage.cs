using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ActionButtonPage
{
    public static Control Create()
    {
        var save = new ActionButton { AsyncAction = () => Task.Delay(1200) };
        var publish = new ActionButton
        {
            Label = "Publish post",
            PendingLabel = "Publishing",
            SuccessLabel = "Published",
            AsyncAction = () => Task.Delay(1500)
        };
        var invite = new ActionButton
        {
            Label = "Send invite",
            PendingLabel = "Sending",
            SuccessLabel = "Sent",
            AsyncAction = () => Task.Delay(800)
        };

        var message = GalleryUi.Text("", ArcDesignTokenKind.TextSecondary, 13);
        var failing = new ActionButton
        {
            Label = "Sync now",
            PendingLabel = "Syncing",
            SuccessLabel = "Synced",
            AsyncAction = async () =>
            {
                await Task.Delay(900);
                throw new InvalidOperationException("The server did not answer.");
            }
        };
        failing.ActionFailed += (_, e) => message.Text = $"Back to idle: {e.Exception.Message}";

        var sticky = new ActionButton
        {
            Label = "Save settings",
            SuccessLabel = "Settings saved",
            ResetAfter = TimeSpan.Zero,
            AsyncAction = () => Task.Delay(700)
        };
        var reset = new Button { Content = "Reset", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        reset.Click += (_, _) => sticky.Reset();

        return GalleryUi.Column(20,
            GalleryUi.Card("Default", "Click: the arrow leaves, a spinner turns while the work runs, the label morphs letter by letter to Saved, a check draws in, and the button returns after 2.4s.", GalleryUi.Row(save)),
            GalleryUi.Card("Labels", "Only the changed run of letters is replaced; shared letters glide and the width follows on a spring.", GalleryUi.Row(publish, invite)),
            GalleryUi.Card("Failure", "A failed action returns to idle and raises ActionFailed.", GalleryUi.Column(12, GalleryUi.Row(failing), message)),
            GalleryUi.Card("Keep the result", "ResetAfter = 0 keeps the success state until Reset().", GalleryUi.Row(sticky, reset)),
            GalleryUi.Card("Disabled", "Pending keeps focus; only a disabled button dims.", GalleryUi.Row(new ActionButton { Label = "Save", IsEnabled = false })));
    }
}
