using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MultiStepFormPreviewDemo : UserControl
{
    public MultiStepFormPreviewDemo()
    {
        InitializeComponent();
        // The host's own line after the last step; Start over is this demo's, the form itself ends on its success block.
        Form.Completed += (_, _) => Status.IsVisible = true;
        StartOver.Click += (_, _) =>
        {
            Status.IsVisible = false;
            Form.Restart();
        };
    }
}
