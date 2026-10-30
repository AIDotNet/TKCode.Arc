using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the self-playing simulation, a slow voice service, a failing one and the empty player.</summary>
public partial class SpeechPlayerPreviewDemo : UserControl
{
    private readonly SpeechSimulation? _simulation;
    private readonly bool _ready;

    public SpeechPlayerPreviewDemo()
    {
        InitializeComponent();
        _simulation = Player.Simulation;
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the player is ready.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "sample");
        }
    }

    private void Show(string scenario)
    {
        if (scenario == "empty")
        {
            // No text and no simulation: the action row waits with Read aloud disabled.
            Player.Synthesize = null;
            Player.Simulation = null;
            return;
        }
        Player.Simulation = _simulation;
        // A new service resets the player to its action row; Open is what Read aloud does.
        Player.Synthesize = scenario switch
        {
            "slow" => request => SpeechSimulation.SynthesizeAsync(request, new SpeechSimulationOptions { Delay = TimeSpan.FromSeconds(3) }),
            "error" => request => SpeechSimulation.SynthesizeAsync(request, new SpeechSimulationOptions { Fail = true }),
            _ => null
        };
        // Both stand-ins are simulations: keep the note that no audio plays.
        Player.IsSimulated = true;
        Player.Open();
    }
}
