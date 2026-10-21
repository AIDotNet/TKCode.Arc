using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TimePickerTimeFieldDemo : UserControl
{
    public TimePickerTimeFieldDemo()
    {
        InitializeComponent();
    }

    private void OnSelectedTimeChanged(object? sender, TimePickerSelectedTimeChangedEventArgs e)
        => Status.Text = $"SelectedTime: {(e.NewTime is { } time ? time.ToString("HH:mm") : "none")}";
}
