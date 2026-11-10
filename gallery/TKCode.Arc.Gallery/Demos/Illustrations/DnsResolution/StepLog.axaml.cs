using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DnsResolutionStepLogDemo : UserControl
{
    private readonly List<string> _steps = new();

    public DnsResolutionStepLogDemo()
    {
        InitializeComponent();
    }

    private void OnStepChanged(object? sender, DnsResolutionStepChangedEventArgs e)
    {
        if (e.Step == DnsResolutionStep.Query)
        {
            _steps.Clear();
        }
        _steps.Add(e.Step.ToString());
        Log.Text = string.Join(" → ", _steps);
    }
}
