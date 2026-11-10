using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BookingPillPreviewDemo : UserControl
{
    public BookingPillPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnSearch(object? sender, BookingPillSearchEventArgs e)
    {
        var where = e.Place ?? "anywhere";
        var when = e.CheckIn is { } from && e.CheckOut is { } to ? $"{from:MMM d} to {to:MMM d}" : "any dates";
        var who = e.Adults + e.Children;
        Readout.Text = $"Searching {where}, {when}, {who} guest{(who == 1 ? "" : "s")}.";
    }
}
