using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MobileActionSheetBasicDemo : UserControl
{
    public MobileActionSheetBasicDemo()
    {
        InitializeComponent();
    }

    private void OnShow(object? sender, RoutedEventArgs e) => Options.IsOpen = true;

    private void OnAction(object? sender, Mobile.ActionSheetActionEventArgs e)
        => Result.Text = $"Chose: {e.Action.Content}";
}
