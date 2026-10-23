using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LayoutMorphFlatDemo : UserControl
{
    public LayoutMorphFlatDemo()
    {
        InitializeComponent();
        var items = LayoutMorphDemoData.CreateItems();
        Morph.Items.AddRange(items);
        Morph.DefaultStats.AddRange(LayoutMorphDemoData.CreateTotals(items));
    }
}
