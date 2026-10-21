using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AnnouncementBarRememberedDismissalDemo : UserControl
{
    public AnnouncementBarRememberedDismissalDemo()
    {
        InitializeComponent();
    }

    private void OnClosed(object? sender, RoutedEventArgs e) => Log.Text = "Dismissed and remembered.";

    private void OnForgetClick(object? sender, RoutedEventArgs e)
    {
        if (Campaign.DismissalKey is not { } key)
        {
            return;
        }
        AnnouncementBar.ClearDismissal(key, Campaign.Storage);
        // Setting the key again makes the bar read the cleared storage, so it can open.
        Campaign.DismissalKey = null;
        Campaign.DismissalKey = key;
        Campaign.IsOpen = true;
    }
}
