using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiSparkPreviewDemo : UserControl
{
    private readonly bool _ready;

    public AiSparkPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
        Caption.Text = Hero.EffectiveStatus.ToString();
        Hero.PropertyChanged += (_, e) =>
        {
            if (e.Property == AiSpark.EffectiveStatusProperty)
            {
                Caption.Text = Hero.EffectiveStatus.ToString();
            }
        };
    }

    private void OnStatusChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged also fires while the XAML loads, before the sparks exist.
        if (!_ready || sender is not SegmentedControl control)
        {
            return;
        }
        AiSparkStatus? status = control.SelectedValue is string name && Enum.TryParse<AiSparkStatus>(name, out var parsed)
            ? parsed
            : null;
        // In Auto the sparks run their own self-play; with a status they all show it.
        Hero.Status = status;
        Pill.Status = status;
        Avatar.Status = status;
    }
}
