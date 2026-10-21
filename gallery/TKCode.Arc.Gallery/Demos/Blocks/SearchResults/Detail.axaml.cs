using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsDetailDemo : UserControl
{
    public SearchResultsDetailDemo()
    {
        InitializeComponent();
        Search.ItemInvoked += (_, e) => Status.Text = $"Opened “{e.Item.Title}”. Escape, the close button or a click outside closes the detail.";
        Search.DetailActionInvoked += (_, e) =>
        {
            Status.Text = e.Action == SearchResultAction.CopyLink
                ? $"Copied {e.Item.Url}."
                : $"“Open” pressed for “{e.Item.Title}”: a real app would navigate here.";
            if (e.Action == SearchResultAction.Open)
            {
                Search.CloseDetail();
            }
        };
    }
}
