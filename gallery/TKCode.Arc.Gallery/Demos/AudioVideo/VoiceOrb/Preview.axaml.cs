using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class VoiceOrbPreviewDemo : UserControl
{
    private readonly VoiceOrbSession _session = new(VoiceOrbSamples.WeekendTrip);
    private IDisposable? _link;

    public VoiceOrbPreviewDemo()
    {
        InitializeComponent();
        _session.Changed += (_, _) => UpdateLabels();
        AttachedToVisualTree += (_, _) =>
        {
            _link ??= _session.Attach(Orb, Transcript, Controls);
            UpdateLabels();
        };
        // The demo owns the conversation: stop its clock when the page goes away.
        DetachedFromVisualTree += (_, _) =>
        {
            _session.End();
            _link?.Dispose();
            _link = null;
        };
    }

    // The orb button does what the current state invites: start, send now, cancel the answer, or interrupt.
    private void OnOrbTap(object? sender, RoutedEventArgs e) => _session.Tap();

    private void UpdateLabels()
    {
        Status.Content = _session.IsMuted && _session.State == VoiceOrbState.Listening
            ? "Muted"
            : _session.State switch
            {
                VoiceOrbState.Listening => "Listening",
                VoiceOrbState.Thinking => "Thinking",
                VoiceOrbState.Speaking => "Speaking",
                _ => "Ready"
            };
        AutomationProperties.SetName(OrbButton, _session.State switch
        {
            VoiceOrbState.Listening => "Send now",
            VoiceOrbState.Thinking => "Cancel and keep talking",
            VoiceOrbState.Speaking => "Interrupt",
            _ => "Start voice chat"
        });
    }
}
