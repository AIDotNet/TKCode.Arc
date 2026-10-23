using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LayoutMorphNarrowDemo : UserControl
{
    public LayoutMorphNarrowDemo()
    {
        InitializeComponent();
        var items = LayoutMorphDemoData.CreateItems();
        Morph.Items.AddRange(items);
        Morph.DefaultStats.AddRange(LayoutMorphDemoData.CreateTotals(items));
    }
}
