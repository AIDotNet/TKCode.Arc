using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: a new reader, a signed-in reader, a key generator that fails and the empty card.</summary>
public partial class InstallSnippetPreviewDemo : UserControl
{
    private readonly InstallSnippetSimulation? _simulation;
    private readonly bool _ready;

    public InstallSnippetPreviewDemo()
    {
        InitializeComponent();
        _simulation = Snippet.Simulation;
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (!_ready)
        {
            return;
        }
        Snippet.ResetProgress();
        var scenario = Scenario.SelectedValue as string;
        // Empty: no frameworks and no simulation, so the steps show their titles and lines over empty snippets.
        Snippet.Simulation = scenario == "empty" ? null : _simulation;
        switch (scenario)
        {
            case "signed-in":
                Snippet.KeyGenerator = null;
                Snippet.ApiKey = InstallSnippetDemoData.SignedInKey;
                break;
            case "fails":
                Snippet.ApiKey = null;
                Snippet.KeyGenerator = async token =>
                {
                    await Task.Delay(700, token);
                    throw new InvalidOperationException("Could not reach the key service. Check your connection.");
                };
                break;
            default:
                Snippet.ApiKey = null;
                Snippet.KeyGenerator = null;
                break;
        }
    }
}
