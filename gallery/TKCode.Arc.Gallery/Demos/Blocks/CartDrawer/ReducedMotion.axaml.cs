using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CartDrawerReducedMotionDemo : UserControl
{
    public CartDrawerReducedMotionDemo()
    {
        InitializeComponent();
        Store.ProductsSource = CartDrawerSamples.CreateProducts();
        Store.Labels = CartDrawerSamples.CreateLabels();
    }
}
