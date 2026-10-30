using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the switcher live, loading a workspace, after a failed switch, and empty.</summary>
public partial class TeamSwitcherPreviewDemo : UserControl
{
    private readonly TeamSwitcherSimulation? _simulation;
    private readonly bool _ready;

    public TeamSwitcherPreviewDemo()
    {
        InitializeComponent();
        _simulation = Switcher.Simulation;
        Switcher.ValueChanged += (_, e) => Status.Text = "Active workspace: " + e.Workspace.Name;
        Switcher.SwitchFailed += (_, e) => Status.Text = "Stayed on the current workspace: " + e.Error.Message;
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "live");
        }
    }

    private void Show(string scenario)
    {
        Switcher.SwitchHandler = null;
        // Empty: no workspaces and no simulation, so the trigger says there is no workspace.
        Switcher.Simulation = scenario == "empty" ? null : _simulation;
        Switcher.Value = scenario == "empty" ? null : "northwind";
        switch (scenario)
        {
            case "switching":
                // A slow load: the trigger shows Contoso with a shimmering "Switching…" and a spinner.
                Switcher.SwitchHandler = (_, token) => Task.Delay(TimeSpan.FromSeconds(3), token);
                _ = Switcher.SwitchToAsync("contoso");
                break;
            case "failed":
                // The load throws once: the trigger goes back to Northwind and shows the message.
                Switcher.SwitchHandler = (workspace, _) =>
                {
                    Switcher.SwitchHandler = null;
                    return Task.FromException(new InvalidOperationException("Your access to " + workspace.Name + " has expired."));
                };
                _ = Switcher.SwitchToAsync("fabrikam");
                break;
        }
    }
}
