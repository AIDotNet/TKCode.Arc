using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReactionsWrappingDemo : UserControl
{
    public ReactionsWrappingDemo()
    {
        InitializeComponent();
        Bar.Value = [new Reaction("like", 8), new Reaction("love", 3, true), new Reaction("celebrate", 1)];
    }
}
