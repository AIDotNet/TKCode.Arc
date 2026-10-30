using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class GhostMascotCanvasDemo : UserControl
{
    public GhostMascotCanvasDemo()
    {
        InitializeComponent();
        // Subscribed after InitializeComponent: the switch's initial IsChecked would otherwise fire before the ghost exists.
        GeneratingSwitch.IsCheckedChanged += (_, _) =>
            Ghost.Status = GeneratingSwitch.IsChecked == true ? GhostMascotStatus.Thinking : GhostMascotStatus.Done;
    }
}
