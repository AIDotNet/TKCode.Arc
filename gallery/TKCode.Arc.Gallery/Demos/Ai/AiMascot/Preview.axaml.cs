using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiMascotPreviewDemo : UserControl
{
    private readonly bool _ready;

    public AiMascotPreviewDemo()
    {
        InitializeComponent();
        // The caption follows the state on screen, whether the tour or the picker set it.
        Mascot.PropertyChanged += (_, e) =>
        {
            if (e.Property == AiMascot.AccessibleLabelProperty)
            {
                Caption.Text = Mascot.AccessibleLabel;
            }
        };
        _ready = true;
    }

    private void OnStatusChosen(object? sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged also fires while the XAML loads, before the named fields exist.
        if (!_ready || sender is not SegmentedControl { SelectedValue: string value })
        {
            return;
        }
        Mascot.Status = Enum.TryParse<AiMascotStatus>(value, out var status) ? status : null;
    }
}
