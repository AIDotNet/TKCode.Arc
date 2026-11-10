using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ComparisonTablePreviewDemo : UserControl
{
    public ComparisonTablePreviewDemo()
    {
        InitializeComponent();
        Table.Cta = new ComparisonCta { Label = "Start free trial", DoneLabel = "Trial started" };
    }
}
