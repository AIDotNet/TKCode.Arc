using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the simulated stream, live, connecting and reconnecting, and the empty card.</summary>
public partial class LiveVisitorsPreviewDemo : UserControl
{
    private readonly LiveVisitorsSimulation? _simulation;
    private readonly bool _ready;

    public LiveVisitorsPreviewDemo()
    {
        InitializeComponent();
        _simulation = Card.Simulation;
        _ready = true;
    }

    private void OnStateChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the card exists.
        if (!_ready)
        {
            return;
        }
        var state = State.SelectedValue as string;
        // Empty: no data and no simulation, so the card holds its placeholders and says there is no data.
        Card.Simulation = state == "empty" ? null : _simulation;
        Card.Connection = state switch
        {
            "connecting" => LiveVisitorsConnection.Connecting,
            "reconnecting" => LiveVisitorsConnection.Reconnecting,
            _ => LiveVisitorsConnection.Live,
        };
    }
}
