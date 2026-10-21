using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DatePickerCulturesAndFormatsDemo : UserControl
{
    public DatePickerCulturesAndFormatsDemo()
    {
        InitializeComponent();
        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var picker in Fields.Children.OfType<DatePicker>())
        {
            picker.SelectedDate = today;
        }
    }
}
