using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class GlassCardTiltSignalDemo : UserControl
{
    public GlassCardTiltSignalDemo()
    {
        InitializeComponent();
        TiltX.ValueChanged += (_, _) => Apply();
        TiltY.ValueChanged += (_, _) => Apply();
        Apply();
    }

    private void Apply() => Card.Tilt = new Vector(TiltX.Value, TiltY.Value);
}
