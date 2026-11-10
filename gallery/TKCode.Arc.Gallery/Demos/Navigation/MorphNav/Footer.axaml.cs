using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MorphNavFooterDemo : UserControl
{
    public MorphNavFooterDemo()
    {
        InitializeComponent();
    }

    private void OnLinkInvoked(object? sender, MorphNavLinkEventArgs e) => Status.Text = $"Opened {e.Link.Text}";

    private void OnBook(object? sender, RoutedEventArgs e)
    {
        Status.Text = "Booking a call";
        Nav.Close();
    }
}
