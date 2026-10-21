using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ButtonGroupDocumentActionsDemo : UserControl
{
    private int _copies;

    public ButtonGroupDocumentActionsDemo()
    {
        InitializeComponent();
        Share.ReserveLabels = ["Copied"];
    }

    private async void OnShareClick(object? sender, RoutedEventArgs e)
    {
        var current = ++_copies;
        Share.Label = "Copied";
        Share.Icon = ArcIcons.Check;
        await Task.Delay(1600);
        if (current == _copies)
        {
            Share.Label = "Share";
            Share.Icon = ArcIcons.Share2;
        }
    }

    private void OnActionInvoked(object? sender, ButtonGroupActionEventArgs e)
    {
        Log.Text = $"Action: {e.Item.Label}";
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        Log.Text = $"Menu: {e.Header}";
    }
}
