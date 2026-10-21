using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionButtonDefaultDemo : UserControl
{
    public ActionButtonDefaultDemo()
    {
        InitializeComponent();
        Save.AsyncAction = () => Task.Delay(1200);
    }
}
