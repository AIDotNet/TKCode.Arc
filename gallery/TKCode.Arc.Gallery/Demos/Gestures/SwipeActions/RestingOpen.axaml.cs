using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SwipeActionsRestingOpenDemo : UserControl
{
    public SwipeActionsRestingOpenDemo()
    {
        InitializeComponent();
        Export.AttachedToVisualTree += (_, _) => Export.Open(SwipeActionSide.Trailing);
    }

    private void OnActionInvoked(object? sender, SwipeActionInvokedEventArgs e)
    {
        if (!e.Action.KeepsRow)
        {
            Notifications.Items.Remove(e.Row);
        }
    }
}
