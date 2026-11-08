using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TicketTearPreviewDemo : UserControl
{
    public TicketTearPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnNewStub(object? sender, RoutedEventArgs e) => Ticket.Reset();
}
