using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MobileSheetBasicDemo : UserControl
{
    public MobileSheetBasicDemo()
    {
        InitializeComponent();
    }

    private void OnShare(object? sender, RoutedEventArgs e) => ShareSheet.IsOpen = true;
}
