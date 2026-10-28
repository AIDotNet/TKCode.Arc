using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class InkFluidTuningDemo : UserControl
{
    public InkFluidTuningDemo()
    {
        InitializeComponent();
        Apply();
    }

    private void OnChanged(object? sender, RoutedEventArgs e) => Apply();

    private void Apply()
    {
        // The events also fire while the XAML loads, before every named field exists.
        if (Ink is null || IntensitySlider is null || TintSlider is null || ForceSlider is null || AmbientSwitch is null || QualityPicker is null)
        {
            return;
        }
        Ink.Intensity = IntensitySlider.Value / 100;
        Ink.Tint = TintSlider.Value / 100;
        Ink.Force = ForceSlider.Value / 100;
        Ink.IsAmbient = AmbientSwitch.IsChecked == true;
        Ink.Quality = QualityPicker.SelectedValue switch
        {
            "Low" => InkFluidQuality.Low,
            "High" => InkFluidQuality.High,
            _ => InkFluidQuality.Medium
        };
    }
}
