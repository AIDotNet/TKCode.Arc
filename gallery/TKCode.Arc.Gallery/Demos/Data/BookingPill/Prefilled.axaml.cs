using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BookingPillPrefilledDemo : UserControl
{
    public BookingPillPrefilledDemo()
    {
        InitializeComponent();
        // Dates are DateOnly values; a stay of four nights starting two weeks from today.
        var start = DateOnly.FromDateTime(DateTime.Today).AddDays(14);
        Pill.CheckIn = start;
        Pill.CheckOut = start.AddDays(4);
    }
}
