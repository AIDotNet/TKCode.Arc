using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BlackHoleOriginDemo : UserControl
{
    public BlackHoleOriginDemo()
    {
        InitializeComponent();
        Mailbox.Options = mailbox => new BlackHoleOptions
        {
            // Pull the rows toward the button that deleted them (on undo the button is gone: the rows' centre is used).
            OriginElement = mailbox.DeleteButton.IsEffectivelyVisible ? mailbox.DeleteButton : null,
            Duration = TimeSpan.FromSeconds(2)
        };
    }
}
