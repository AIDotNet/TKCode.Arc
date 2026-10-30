using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CubeMascotPreviewDemo : UserControl
{
    private readonly bool _ready;

    public CubeMascotPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
        Caption.Text = Hero.EffectiveStatus.ToString();
        Hero.PropertyChanged += (_, e) =>
        {
            if (e.Property == CubeMascot.EffectiveStatusProperty)
            {
                Caption.Text = Hero.EffectiveStatus.ToString();
            }
        };
    }

    private void OnStatusChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged also fires while the XAML loads, before the cube exists.
        if (!_ready || sender is not SegmentedControl control)
        {
            return;
        }
        CubeMascotStatus? status = control.SelectedValue is string name && Enum.TryParse<CubeMascotStatus>(name, out var parsed)
            ? parsed
            : null;
        Hero.Status = status;
    }
}
