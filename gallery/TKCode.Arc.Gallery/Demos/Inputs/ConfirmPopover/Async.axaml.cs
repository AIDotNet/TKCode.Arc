using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConfirmPopoverAsyncDemo : UserControl
{
    public ConfirmPopoverAsyncDemo()
    {
        InitializeComponent();
        Confirm.ConfirmHandler = RevokeAsync;
    }

    // Stands in for a request to the server.
    private async Task<bool> RevokeAsync()
    {
        await Task.Delay(1200);
        if (Fail.IsChecked == true)
        {
            Log.Text = "The server did not answer. Try again.";
            return false;
        }
        return true;
    }

    private void OnConfirmed(object? sender, RoutedEventArgs e) => Log.Text = "Key ending in 4F2A was revoked.";
}
