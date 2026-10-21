using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConfirmMorphCountdownDemo : UserControl
{
    public ConfirmMorphCountdownDemo()
    {
        InitializeComponent();
        Discard.UndoAction = () => Task.Delay(500);
    }
}
