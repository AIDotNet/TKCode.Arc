using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TicketTearCheckInDemo : UserControl
{
    public TicketTearCheckInDemo()
    {
        InitializeComponent();
    }

    private void OnTorn(object? sender, RoutedEventArgs e) => Readout.Text = "Boarded.";

    private void OnScanned(object? sender, RoutedEventArgs e) => Ticket.IsTorn = true;

    private void OnNewStub(object? sender, RoutedEventArgs e)
    {
        Ticket.IsTorn = false;
        Readout.Text = "Not boarded yet.";
    }
}
