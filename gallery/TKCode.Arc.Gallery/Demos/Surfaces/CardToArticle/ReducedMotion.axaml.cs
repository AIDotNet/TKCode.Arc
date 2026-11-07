using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CardToArticleReducedMotionDemo : UserControl
{
    public CardToArticleReducedMotionDemo()
    {
        InitializeComponent();
        Stories.Items = CardToArticleSamples.Stories();
    }
}
