using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ControlCenterReducedMotionDemo : UserControl
{
    public ControlCenterReducedMotionDemo()
    {
        InitializeComponent();
        ControlCenterSamples.Apply(Center);
    }
}
