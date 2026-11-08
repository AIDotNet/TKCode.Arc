using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CartDrawerPreviewDemo : UserControl
{
    public CartDrawerPreviewDemo()
    {
        InitializeComponent();
        // Sample products with drawn pictures, and the labels with the promo hint ("Use WORKSHOP10 to save 10%").
        Store.ProductsSource = CartDrawerSamples.CreateProducts();
        Store.Labels = CartDrawerSamples.CreateLabels();
    }
}
