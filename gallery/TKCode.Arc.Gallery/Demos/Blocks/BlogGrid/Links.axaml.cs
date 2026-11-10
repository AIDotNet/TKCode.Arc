using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BlogGridLinksDemo : UserControl
{
    public BlogGridLinksDemo()
    {
        InitializeComponent();
        Grid.HrefSelector = post => "https://example.com/blog/" + post.Id;
        // Handling the event keeps the demo from opening a browser.
        Grid.PostOpened += (_, e) =>
        {
            e.Handled = true;
            Grid.Description = "Would open " + e.Href;
        };
    }
}
