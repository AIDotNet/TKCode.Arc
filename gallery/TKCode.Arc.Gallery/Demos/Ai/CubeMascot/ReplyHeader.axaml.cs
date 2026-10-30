using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CubeMascotReplyHeaderDemo : UserControl
{
    private readonly bool _ready;

    public CubeMascotReplyHeaderDemo()
    {
        InitializeComponent();
        _ready = true;
    }

    private void OnPhaseChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged also fires while the XAML loads, before the cube exists.
        if (!_ready || sender is not SegmentedControl control)
        {
            return;
        }
        var (status, text) = (control.SelectedValue as string) switch
        {
            "streaming" => (CubeMascotStatus.Writing, "Writing"),
            "complete" => (CubeMascotStatus.Done, "Done"),
            "failed" => (CubeMascotStatus.Error, "Couldn't finish"),
            _ => (CubeMascotStatus.Thinking, "Thinking")
        };
        Mascot.Status = status;
        Phase.Text = text;
    }
}
