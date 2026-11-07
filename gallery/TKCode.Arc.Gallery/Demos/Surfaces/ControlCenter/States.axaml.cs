using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ControlCenterStatesDemo : UserControl
{
    public ControlCenterStatesDemo()
    {
        InitializeComponent();
        ControlCenterSamples.Apply(Busy);
        Busy.StartFocus("recharge");
        ControlCenterSamples.Apply(Quiet);
        Quiet.Volume = 0;
    }
}
