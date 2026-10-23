using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvailabilityPickerPreviewDemo : UserControl
{
    public AvailabilityPickerPreviewDemo()
    {
        InitializeComponent();
        var week = AvailabilityPickerDemoData.Create();
        Picker.Attendees.AddRange(week.People);
        Picker.Days.AddRange(week.Days);
        // Start on Monday's second free time, so the check sits in the right-hand column.
        Picker.SelectedSlot = week.Days[0].Slots[1];
    }
}
