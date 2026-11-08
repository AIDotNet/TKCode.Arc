using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CyclicPanelAnimationDemo : UserControl
{
    public CyclicPanelAnimationDemo()
    {
        InitializeComponent();
        Back.Click += (_, _) => Strip.ScrollBy(-3);
        Forward.Click += (_, _) => Strip.ScrollNext();
    }
}
