using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CardToArticleNarrowDemo : UserControl
{
    public CardToArticleNarrowDemo()
    {
        InitializeComponent();
        var stories = CardToArticleSamples.MoreStories();
        Stories.Items = stories;
    }
}
