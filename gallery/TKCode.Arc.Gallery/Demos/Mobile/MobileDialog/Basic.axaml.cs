using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MobileDialogBasicDemo : UserControl
{
    public MobileDialogBasicDemo()
    {
        InitializeComponent();
    }

    private void OnDelete(object? sender, RoutedEventArgs e) => DeleteDialog.IsOpen = true;

    private void OnShare(object? sender, RoutedEventArgs e) => ShareDialog.IsOpen = true;
}
