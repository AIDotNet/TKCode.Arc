using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExplodedViewPageScrollDemo : UserControl
{
    public ExplodedViewPageScrollDemo()
    {
        InitializeComponent();
        ExplodedViewDemoData.Fill(View);
        View.Description = null;
    }
}
