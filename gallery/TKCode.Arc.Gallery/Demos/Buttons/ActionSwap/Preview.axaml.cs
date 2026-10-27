using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionSwapPreviewDemo : UserControl
{
    public ActionSwapPreviewDemo()
    {
        InitializeComponent();
        // "Added" and "Sent" run a request; with the switch on, it fails and the pill shows "Try again".
        Library.AsyncAction = (next, _) => next.Id == "added" ? Request(1100, "Couldn't save the article. Try again") : null;
        Invite.AsyncAction = (next, _) => next.Id == "sent" ? Request(1300, "Couldn't send the invite. Try again") : null;
    }

    private async Task Request(int milliseconds, string failure)
    {
        await Task.Delay(milliseconds);
        if (Offline.IsChecked == true)
        {
            Status.Text = failure;
            throw new InvalidOperationException("offline");
        }
    }

    private void OnFollowChanged(object? sender, ActionSwapValueChangedEventArgs e) =>
        Status.Text = e.Value == "following" ? "You're following Emma Collins" : "You unfollowed Emma Collins";

    private void OnLibraryChanged(object? sender, ActionSwapValueChangedEventArgs e) =>
        Status.Text = e.Value == "added" ? "Saved to your library" : "Removed from your library";

    private void OnInviteChanged(object? sender, ActionSwapValueChangedEventArgs e)
    {
        if (e.Value == "sent")
        {
            Status.Text = "Invite sent to Ava Mitchell";
        }
    }

    private void OnOfflineChanged(object? sender, RoutedEventArgs e) =>
        Status.Text = Offline.IsChecked == true ? "Connection lost: the next request will fail" : "Connection restored";
}
