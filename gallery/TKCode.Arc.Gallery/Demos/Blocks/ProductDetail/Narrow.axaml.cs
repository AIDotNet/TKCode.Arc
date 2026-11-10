using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProductDetailNarrowDemo : UserControl
{
    public ProductDetailNarrowDemo()
    {
        InitializeComponent();
        ProductDetailSamples.Apply(Detail);
    }
}
