using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SheetStackDialogsDemo : UserControl
{
    public SheetStackDialogsDemo()
    {
        InitializeComponent();
    }

    private void OnKeep(object? sender, RoutedEventArgs e) => Stack.Pop();

    private void OnRelease(object? sender, RoutedEventArgs e) => Stack.Close();
}
