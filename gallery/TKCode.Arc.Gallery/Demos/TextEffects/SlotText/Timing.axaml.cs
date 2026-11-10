using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SlotTextTimingDemo : UserControl
{
    private int _count = 860;

    public SlotTextTimingDemo()
    {
        InitializeComponent();
        Show();
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        _count += 137;
        Show();
    }

    private void Show()
    {
        Default.Value = _count;
        Straight.Value = _count;
        Slow.Value = _count;
    }
}
