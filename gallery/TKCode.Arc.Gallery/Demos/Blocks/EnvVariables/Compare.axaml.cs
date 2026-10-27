using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EnvVariablesCompareDemo : UserControl
{
    public EnvVariablesCompareDemo()
    {
        InitializeComponent();
        EnvVariablesDemoData.Fill(Block);
    }
}
