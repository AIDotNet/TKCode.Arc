using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class VoiceOrbReducedMotionDemo : UserControl
{
    private readonly VoiceOrbSession _session = new(VoiceOrbSamples.WeekendTrip);
    private IDisposable? _link;

    public VoiceOrbReducedMotionDemo()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => _link ??= _session.Attach(Orb, Transcript, Controls);
        DetachedFromVisualTree += (_, _) =>
        {
            _session.End();
            _link?.Dispose();
            _link = null;
        };
    }
}
