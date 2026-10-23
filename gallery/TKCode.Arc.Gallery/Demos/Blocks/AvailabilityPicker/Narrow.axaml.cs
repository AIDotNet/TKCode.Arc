using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvailabilityPickerNarrowDemo : UserControl
{
    public AvailabilityPickerNarrowDemo()
    {
        InitializeComponent();
        var week = AvailabilityPickerDemoData.Create();
        Picker.Attendees.AddRange(week.People);
        Picker.Days.AddRange(week.Days);
    }
}
