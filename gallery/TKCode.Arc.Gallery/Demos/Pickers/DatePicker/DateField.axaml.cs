using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DatePickerDateFieldDemo : UserControl
{
    public DatePickerDateFieldDemo()
    {
        InitializeComponent();
        Start.SelectedDate = DateOnly.FromDateTime(DateTime.Today);
        Show(Start.SelectedDate);
    }

    private void OnSelectedDateChanged(object? sender, DatePickerSelectedDateChangedEventArgs e) => Show(e.NewDate);

    private void Show(DateOnly? date)
        => Picked.Text = $"SelectedDate: {(date is { } d ? d.ToString("yyyy-MM-dd") : "none")}";
}
