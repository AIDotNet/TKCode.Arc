using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExpandableCardGrowingSidewaysDemo : UserControl
{
    private static readonly string[] Statuses =
    [
        "Shipped · arrives Thursday",
        "Out for delivery · arrives today",
        "Delivered · left at the front door"
    ];

    private int _step;

    public ExpandableCardGrowingSidewaysDemo()
    {
        InitializeComponent();
    }

    private void OnAdvanceClick(object? sender, RoutedEventArgs e)
    {
        _step = (_step + 1) % Statuses.Length;
        Order.Description = Statuses[_step];
    }
}
