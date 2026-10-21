using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class FloatingButtonGroupMutedTrayDemo : UserControl
{
    public FloatingButtonGroupMutedTrayDemo()
    {
        InitializeComponent();
        Share.ReserveLabels = ["Copied"];
    }

    private async void OnShareClick(object? sender, RoutedEventArgs e)
    {
        Share.Label = "Copied";
        await Task.Delay(1600);
        Share.Label = "Share";
    }

    private void OnPresentClick(object? sender, RoutedEventArgs e)
    {
        Present.IsChecked = Present.IsChecked != true;
    }

    private void OnActionInvoked(object? sender, FloatingButtonGroupActionEventArgs e)
    {
        Log.Text = $"Action: {e.Item.Label}";
    }
}
