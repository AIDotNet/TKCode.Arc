using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PropertyGridControlsDemo : UserControl
{
    private static readonly HashSet<string> s_shown =
        ["Variant", "Size", "IsLoading", "IsPressScaleEnabled", "IsEnabled", "Opacity", "MinWidth", "HorizontalAlignment"];

    public PropertyGridControlsDemo()
    {
        InitializeComponent();
        // An Avalonia object needs no registration: its styled and direct properties are listed, grouped by owner type.
        Properties.PropertyFilter = property => s_shown.Contains(property.Name);
        Properties.SelectedObject = Preview;
    }
}
