using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CardToArticlePreviewDemo : UserControl
{
    public CardToArticlePreviewDemo()
    {
        InitializeComponent();
        Stories.Items = CardToArticleSamples.Stories();
    }
}
