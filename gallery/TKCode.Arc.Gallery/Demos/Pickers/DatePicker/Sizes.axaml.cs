using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DatePickerSizesDemo : UserControl
{
    public DatePickerSizesDemo()
    {
        InitializeComponent();
        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var picker in Fields.Children.OfType<DatePicker>())
        {
            picker.SelectedDate = today;
        }
    }
}
