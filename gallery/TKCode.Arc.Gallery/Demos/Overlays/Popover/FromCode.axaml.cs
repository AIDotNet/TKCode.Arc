using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class PopoverFromCodeDemo : UserControl
{
    private readonly Popover _popover = new()
    {
        Content = "Opened from code with ShowAt(anchor).",
        Alignment = PopoverAlignment.Center
    };

    public PopoverFromCodeDemo()
    {
        InitializeComponent();
    }

    private void OnShowClick(object? sender, RoutedEventArgs e) => _popover.ShowAt(Anchor);
}
