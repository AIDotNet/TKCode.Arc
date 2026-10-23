using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvailabilityPickerTrickyDataDemo : UserControl
{
    public AvailabilityPickerTrickyDataDemo()
    {
        InitializeComponent();
        var week = AvailabilityPickerDemoData.CreateTricky();
        Picker.Attendees.AddRange(week.People);
        Picker.Days.AddRange(week.Days);
    }
}
