using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ControlCenterNarrowDemo : UserControl
{
    public ControlCenterNarrowDemo()
    {
        InitializeComponent();
        ControlCenterSamples.Apply(Center);
    }
}
