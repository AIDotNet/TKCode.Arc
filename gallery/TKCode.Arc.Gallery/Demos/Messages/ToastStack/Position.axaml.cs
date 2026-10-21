using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToastStackPositionDemo : UserControl
{
    public ToastStackPositionDemo()
    {
        InitializeComponent();
    }

    private void OnBottomLeftClick(object? sender, RoutedEventArgs e) => MoveTo(ToastStackPosition.BottomLeft, "Bottom left");

    private void OnBottomCenterClick(object? sender, RoutedEventArgs e) => MoveTo(ToastStackPosition.BottomCenter, "Bottom center");

    private void OnBottomRightClick(object? sender, RoutedEventArgs e) => MoveTo(ToastStackPosition.BottomRight, "Bottom right");

    // The stack is empty until a toast arrives, so each move also shows one to see where the stack hangs.
    private void MoveTo(ToastStackPosition position, string label)
    {
        Stack.Position = position;
        Stack.Show(new ToastOptions { Kind = ToastKind.Info, Title = label });
    }
}
