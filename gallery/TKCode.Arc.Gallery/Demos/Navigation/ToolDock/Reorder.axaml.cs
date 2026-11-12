using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToolDockReorderDemo : UserControl
{
    public ToolDockReorderDemo()
    {
        InitializeComponent();
        ShowOrder();
        Dock.Reordered += (_, _) => ShowOrder();
    }

    private void ShowOrder() =>
        Order.Text = "Order: " + string.Join(", ", Dock.Items.OfType<ToolDockItem>().Select(item => item.Label));
}
