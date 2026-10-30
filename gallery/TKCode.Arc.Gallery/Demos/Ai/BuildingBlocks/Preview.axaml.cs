using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the simulated build, and the three statuses an app sets.</summary>
public partial class BuildingBlocksPreviewDemo : UserControl
{
    private readonly bool _ready;

    public BuildingBlocksPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (!_ready)
        {
            return;
        }
        Blocks.Status = (Scenario.SelectedValue as string) switch
        {
            "idle" => BuildingBlocksStatus.Idle,
            "building" => BuildingBlocksStatus.Building,
            "done" => BuildingBlocksStatus.Done,
            _ => null
        };
    }
}
