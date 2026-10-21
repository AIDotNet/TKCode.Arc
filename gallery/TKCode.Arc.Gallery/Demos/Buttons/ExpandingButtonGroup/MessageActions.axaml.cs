using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExpandingButtonGroupMessageActionsDemo : UserControl
{
    public ExpandingButtonGroupMessageActionsDemo()
    {
        InitializeComponent();
        Archive.AsyncAction = async () =>
        {
            await Task.Delay(600);
            return true;
        };
    }

    private void OnActionInvoked(object? sender, ExpandingButtonGroupActionEventArgs e)
    {
        Log.Text = $"Action: {e.Item.Label}";
    }
}
