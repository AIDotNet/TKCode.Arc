using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ModelArenaNarrowDemo : UserControl
{
    public ModelArenaNarrowDemo()
    {
        InitializeComponent();
        Arena.Contestants.AddRange(ModelArenaDemoData.Models());
        Arena.Prompts.AddRange(ModelArenaDemoData.Prompts());
        Arena.Runner = new ModelArenaDemoData.Runner();
    }
}
