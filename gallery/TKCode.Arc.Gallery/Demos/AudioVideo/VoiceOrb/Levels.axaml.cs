using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class VoiceOrbLevelsDemo : UserControl
{
    public VoiceOrbLevelsDemo()
    {
        InitializeComponent();
    }

    private void OnStateChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The event also fires while the XAML loads, before the named field exists: read the sender.
        if (Orb is null || sender is not SegmentedControl control)
        {
            return;
        }
        Orb.State = control.SelectedValue switch
        {
            "idle" => VoiceOrbState.Idle,
            "thinking" => VoiceOrbState.Thinking,
            "speaking" => VoiceOrbState.Speaking,
            _ => VoiceOrbState.Listening
        };
    }
}
