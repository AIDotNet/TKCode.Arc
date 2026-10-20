namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterPickersA()
    {
        Register("DatePicker", DatePickerPage.Create);
        Register("DateRangePicker", DateRangePickerPage.Create);
        Register("TimePicker", TimePickerPage.Create);
        Register("ColorPicker", ColorPickerPage.Create);
    }
}
