using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class DialogSizesDemo : UserControl
{
    public DialogSizesDemo()
    {
        InitializeComponent();
    }

    private void OnSmallClick(object? sender, RoutedEventArgs e) => Open(ControlSize.Small);

    private void OnMediumClick(object? sender, RoutedEventArgs e) => Open(ControlSize.Medium);

    private void OnLargeClick(object? sender, RoutedEventArgs e) => Open(ControlSize.Large);

    private void Open(ControlSize size)
    {
        LimitsDialog.Size = size;
        LimitsDialog.IsOpen = true;
    }
}
