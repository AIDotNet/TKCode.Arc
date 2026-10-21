using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProgressChangingLabelDemo : UserControl
{
    private static readonly string[] Phases = ["Preparing export", "Compressing archive", "Uploading archive"];

    private int _phase;

    public ProgressChangingLabelDemo()
    {
        InitializeComponent();
    }

    private void OnNextPhaseClick(object? sender, RoutedEventArgs e)
    {
        _phase = (_phase + 1) % Phases.Length;
        Export.Label = Phases[_phase];
        Export.Value = 20 + _phase * 35;
    }
}
