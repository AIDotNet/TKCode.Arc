using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BackToTopAttachedDemo : UserControl
{
    public BackToTopAttachedDemo()
    {
        InitializeComponent();
        Feed.Now = TimelineDemoData.Now();
        Feed.ItemsSource = TimelineDemoData.History(60, 140);
    }
}
