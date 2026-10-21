using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MultiSelectOneVisibleChipDemo : UserControl
{
    public MultiSelectOneVisibleChipDemo()
    {
        InitializeComponent();
        Owners.Selection.Select(0);
        Owners.Selection.Select(1);
        Owners.Selection.Select(3);
    }
}
