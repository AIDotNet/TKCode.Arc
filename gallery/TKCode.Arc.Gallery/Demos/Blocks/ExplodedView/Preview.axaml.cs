using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExplodedViewPreviewDemo : UserControl
{
    public ExplodedViewPreviewDemo()
    {
        InitializeComponent();
        // Five layers of an invented running app, drawn with Arc parts (see ExplodedViewDemoData.cs).
        ExplodedViewDemoData.Fill(View);
    }
}
