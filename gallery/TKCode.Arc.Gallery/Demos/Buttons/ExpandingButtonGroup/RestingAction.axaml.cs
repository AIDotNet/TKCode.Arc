using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExpandingButtonGroupRestingActionDemo : UserControl
{
    public ExpandingButtonGroupRestingActionDemo()
    {
        InitializeComponent();
        // Returning false declines the result, so Publish skips the confirmation.
        Publish.AsyncAction = async () =>
        {
            await Task.Delay(500);
            return false;
        };
    }
}
