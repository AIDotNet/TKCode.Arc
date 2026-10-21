using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ButtonGroupCollapsingLabelsDemo : UserControl
{
    public ButtonGroupCollapsingLabelsDemo()
    {
        InitializeComponent();
    }

    private void OnNarrowClick(object? sender, RoutedEventArgs e)
    {
        Frame.Width = 240;
    }

    private void OnWideClick(object? sender, RoutedEventArgs e)
    {
        Frame.Width = 520;
    }
}
