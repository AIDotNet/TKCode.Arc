using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EnvVariablesPreviewDemo : UserControl
{
    public EnvVariablesPreviewDemo()
    {
        InitializeComponent();
        // No RedeployAction: the block simulates a deployment, so the banner can be tried here.
        EnvVariablesDemoData.Fill(Block);
    }
}
