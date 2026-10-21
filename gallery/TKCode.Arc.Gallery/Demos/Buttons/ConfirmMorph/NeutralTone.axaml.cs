using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConfirmMorphNeutralToneDemo : UserControl
{
    public ConfirmMorphNeutralToneDemo()
    {
        InitializeComponent();
        Revoke.ConfirmAction = () => Task.Delay(800);
    }
}
