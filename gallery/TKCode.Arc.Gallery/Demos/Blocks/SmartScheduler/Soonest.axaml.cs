using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SmartSchedulerSoonestDemo : UserControl
{
    public SmartSchedulerSoonestDemo()
    {
        InitializeComponent();
        Scheduler.SlotBooked += (_, e) => Log.Text = $"Booked {e.Start:ddd HH:mm}–{e.End:HH:mm} with {e.People.Count} people";
    }
}
