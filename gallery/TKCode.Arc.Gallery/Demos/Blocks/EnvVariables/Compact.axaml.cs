using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EnvVariablesCompactDemo : UserControl
{
    public EnvVariablesCompactDemo()
    {
        InitializeComponent();
        EnvVariablesDemoData.Fill(Block);
    }
}
