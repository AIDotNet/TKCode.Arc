using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TicketTearPreviewDemo : UserControl
{
    public TicketTearPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnTorn(object? sender, RoutedEventArgs e)
    {
        Status.IsVisible = true;
        NewTicket.IsEnabled = true;
    }

    private void OnNewTicket(object? sender, RoutedEventArgs e)
    {
        Ticket.Reset();
        Status.IsVisible = false;
        NewTicket.IsEnabled = false;
    }
}
