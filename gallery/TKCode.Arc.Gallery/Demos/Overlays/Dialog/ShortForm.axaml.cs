using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DialogShortFormDemo : UserControl
{
    public DialogShortFormDemo()
    {
        InitializeComponent();
    }

    private void OnClosed(object? sender, OverlayClosedEventArgs e)
    {
        if (e.Result is true)
        {
            Status.Text = $"Project: {ProjectName.Text}";
        }
    }
}
