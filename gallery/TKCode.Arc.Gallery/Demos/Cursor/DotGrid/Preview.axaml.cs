using Avalonia.Controls;
using System.Globalization;

namespace TKCode.Arc.Gallery.Demos;

public partial class DotGridPreviewDemo : UserControl
{
    public DotGridPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnDensityChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The event also fires while the XAML loads, before the named field exists: read the sender.
        if (Field is not null && sender is SegmentedControl { SelectedValue: string value })
        {
            Field.Gap = double.Parse(value, CultureInfo.InvariantCulture);
        }
    }
}
