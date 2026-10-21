using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DateRangePickerReportDemo : UserControl
{
    public DateRangePickerReportDemo()
    {
        InitializeComponent();
        var today = DateOnly.FromDateTime(DateTime.Today);
        Picker.SelectedRange = new DateRange(today.AddDays(-6), today);
        ShowDays(Picker.SelectedRange);
    }

    // Raised on Apply only: the selection is a draft until then.
    private void OnApplied(object? sender, DateRangePickerSelectedRangeChangedEventArgs e) => ShowDays(e.NewRange);

    private void ShowDays(DateRange? range)
        => Status.Text = range is { } days ? $"{days.Days} days selected" : "No range";
}
