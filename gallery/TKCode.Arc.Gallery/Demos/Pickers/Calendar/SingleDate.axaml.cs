using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CalendarSingleDateDemo : UserControl
{
    public CalendarSingleDateDemo()
    {
        InitializeComponent();
        Month.SelectedDate = DateOnly.FromDateTime(DateTime.Today);
    }

    private void OnDateSelected(object? sender, CalendarDateSelectedEventArgs e)
        => Picked.Text = $"Selected {e.Date:D}";
}
