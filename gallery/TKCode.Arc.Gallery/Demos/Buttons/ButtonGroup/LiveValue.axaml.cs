using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ButtonGroupLiveValueDemo : UserControl
{
    private int _zoom = 100;

    public ButtonGroupLiveValueDemo()
    {
        InitializeComponent();
    }

    private void OnZoomOutClick(object? sender, RoutedEventArgs e)
    {
        SetZoom(_zoom - 25);
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e)
    {
        SetZoom(_zoom + 25);
    }

    private void OnZoomResetClick(object? sender, RoutedEventArgs e)
    {
        SetZoom(100);
    }

    private void SetZoom(int value)
    {
        _zoom = Math.Clamp(value, 25, 400);
        ZoomValue.Value = $"{_zoom}%";
    }
}
