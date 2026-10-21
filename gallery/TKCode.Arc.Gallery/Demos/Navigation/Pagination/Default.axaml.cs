using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PaginationDefaultDemo : UserControl
{
    public PaginationDefaultDemo()
    {
        InitializeComponent();
    }

    private void OnPageChanged(object? sender, PaginationPageChangedEventArgs e)
        => Status.Text = $"Page {e.NewPage} of {Pager.PageCount}";
}
