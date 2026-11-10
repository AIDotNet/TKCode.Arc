using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConfirmPopoverPreviewDemo : UserControl
{
    public ConfirmPopoverPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnConfirmed(object? sender, RoutedEventArgs e)
    {
        Row.Opacity = 0.4;
        Log.Text = "Notes deleted.";
    }

    private void OnCancelled(object? sender, ConfirmPopoverCancelledEventArgs e) =>
        Log.Text = $"Kept the notes ({e.Reason}).";
}
