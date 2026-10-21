using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class DrawerSidesDemo : UserControl
{
    public DrawerSidesDemo()
    {
        InitializeComponent();
    }

    private void OnRightClick(object? sender, RoutedEventArgs e) => Open(DrawerSide.Right);

    private void OnLeftClick(object? sender, RoutedEventArgs e) => Open(DrawerSide.Left);

    private void OnTopClick(object? sender, RoutedEventArgs e) => Open(DrawerSide.Top);

    private void OnBottomClick(object? sender, RoutedEventArgs e) => Open(DrawerSide.Bottom);

    private void OnClosed(object? sender, OverlayClosedEventArgs e) => Log.Text = $"Closed by {e.Reason}";

    private void Open(DrawerSide side)
    {
        FiltersDrawer.Side = side;
        FiltersDrawer.IsOpen = true;
    }
}
