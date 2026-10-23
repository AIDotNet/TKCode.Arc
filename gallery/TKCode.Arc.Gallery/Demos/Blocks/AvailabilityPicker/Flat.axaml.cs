using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvailabilityPickerFlatDemo : UserControl
{
    public AvailabilityPickerFlatDemo()
    {
        InitializeComponent();
        var week = AvailabilityPickerDemoData.Create();
        Picker.Attendees.AddRange(week.People);
        Picker.Days.AddRange(week.Days);
        Picker.SelectedDay = week.Days[2];
    }
}
