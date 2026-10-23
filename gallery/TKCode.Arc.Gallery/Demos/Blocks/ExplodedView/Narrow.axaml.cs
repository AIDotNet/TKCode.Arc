using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExplodedViewNarrowDemo : UserControl
{
    public ExplodedViewNarrowDemo()
    {
        InitializeComponent();
        ExplodedViewDemoData.Fill(View);
    }
}
