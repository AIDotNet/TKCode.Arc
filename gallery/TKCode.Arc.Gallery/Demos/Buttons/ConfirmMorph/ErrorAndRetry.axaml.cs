using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConfirmMorphErrorAndRetryDemo : UserControl
{
    private int _attempts;

    public ConfirmMorphErrorAndRetryDemo()
    {
        InitializeComponent();
        // Every other attempt fails, so the error face and Retry show up.
        Delete.ConfirmAction = async () =>
        {
            await Task.Delay(800);
            if (++_attempts % 2 == 1)
            {
                throw new InvalidOperationException("Offline");
            }
        };
    }
}
