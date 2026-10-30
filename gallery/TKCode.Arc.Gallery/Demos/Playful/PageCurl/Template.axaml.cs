using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PageCurlTemplateDemo : UserControl
{
    public PageCurlTemplateDemo()
    {
        InitializeComponent();
    }

    private void OnSpreadChanged(object? sender, PageCurlSpreadChangedEventArgs e)
    {
        Readout.Text = $"Spread {e.Spread} of {Book.SpreadCount - 1}";
    }
}
