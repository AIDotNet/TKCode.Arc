using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TimeDialPreviewDemo : UserControl
{
    public TimeDialPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnCommitted(object? sender, TimeDialValueCommittedEventArgs e) => Readout.Text = $"Alarm set for {Dial.ValueText}.";
}
