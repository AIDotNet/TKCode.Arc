using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SwipeActionsOwnActionsDemo : UserControl
{
    public SwipeActionsOwnActionsDemo()
    {
        InitializeComponent();
    }

    private void OnActionInvoked(object? sender, SwipeActionInvokedEventArgs e)
    {
        if (!e.Action.KeepsRow)
        {
            Tasks.Items.Remove(e.Row);
        }
    }
}
