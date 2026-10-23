using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExplodedViewReducedMotionDemo : UserControl
{
    public ExplodedViewReducedMotionDemo()
    {
        InitializeComponent();
        ExplodedViewDemoData.Fill(View);
        View.Description = null;
    }
}
