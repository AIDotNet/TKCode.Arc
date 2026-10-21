using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConfirmMorphHoldDemo : UserControl
{
    public ConfirmMorphHoldDemo()
    {
        InitializeComponent();
        Remove.ConfirmAction = () => Task.Delay(700);
    }
}
