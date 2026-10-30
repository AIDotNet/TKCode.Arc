using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class GhostMascotPreviewDemo : UserControl
{
    private readonly bool _ready;

    public GhostMascotPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
        Caption.Text = Describe(Hero.EffectiveStatus);
        // Raised by the self-playing ghost each time it moves to the next state.
        Hero.StatusChanged += (_, e) => Caption.Text = Describe(e.Status);
    }

    private static string Describe(GhostMascotStatus status) => status switch
    {
        GhostMascotStatus.Listening => "Listening to you",
        GhostMascotStatus.Thinking => "Thinking",
        GhostMascotStatus.Writing => "Writing a reply",
        GhostMascotStatus.Done => "Done",
        GhostMascotStatus.Error => "Couldn't finish",
        _ => "Ready"
    };

    private void OnStatusChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged also fires while the XAML loads, before the ghosts exist.
        if (!_ready || sender is not SegmentedControl control)
        {
            return;
        }
        GhostMascotStatus? status = control.SelectedValue is string name && Enum.TryParse<GhostMascotStatus>(name, out var parsed)
            ? parsed
            : null;
        // In Auto the ghosts play on their own; with a status they both show it.
        Hero.Status = status;
        Inline.Status = status;
        Caption.Text = Describe(Hero.EffectiveStatus);
    }
}
