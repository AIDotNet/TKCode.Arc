using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CartDrawerNarrowDemo : UserControl
{
    public CartDrawerNarrowDemo()
    {
        InitializeComponent();
        Store.ProductsSource = CartDrawerSamples.CreateProducts();
        Store.Labels = CartDrawerSamples.CreateLabels();
    }
}
