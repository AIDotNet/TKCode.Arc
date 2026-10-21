using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ResizablePanelsDefaultDemo : UserControl
{
    public ResizablePanelsDefaultDemo()
    {
        InitializeComponent();
    }

    private void OnLayoutChanged(object? sender, ResizablePanelsLayoutChangedEventArgs e)
        => Status.Text = "Layout: " + string.Join(" / ", e.Sizes.Select(s => $"{s:0}%"));
}
