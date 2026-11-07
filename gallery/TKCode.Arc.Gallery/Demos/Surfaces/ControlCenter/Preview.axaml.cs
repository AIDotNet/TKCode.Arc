using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ControlCenterPreviewDemo : UserControl
{
    public ControlCenterPreviewDemo()
    {
        InitializeComponent();
        ControlCenterSamples.Apply(Center);
    }
}
