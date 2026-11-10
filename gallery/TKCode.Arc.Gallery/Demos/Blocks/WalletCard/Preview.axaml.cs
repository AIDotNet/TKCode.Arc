using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WalletCardPreviewDemo : UserControl
{
    public WalletCardPreviewDemo()
    {
        InitializeComponent();
        WalletCardSamples.Apply(Card);
        Card.SetFrozenAsync = WalletCardSamples.SetFrozenAsync;
    }
}
