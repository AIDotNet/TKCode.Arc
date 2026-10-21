using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionButtonLabelsDemo : UserControl
{
    public ActionButtonLabelsDemo()
    {
        InitializeComponent();
        Publish.AsyncAction = () => Task.Delay(1500);
        Invite.AsyncAction = () => Task.Delay(800);
    }
}
