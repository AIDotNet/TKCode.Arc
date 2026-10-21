using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class ColorPickerFormatsAndSavedSwatchesDemo : UserControl
{
    public ColorPickerFormatsAndSavedSwatchesDemo()
    {
        InitializeComponent();
        Brand.Swatches =
        [
            ColorSwatch.Create(Color.Parse("#E5484D")),
            ColorSwatch.Create(Color.Parse("#F76B15")),
            ColorSwatch.Create(Color.Parse("#30A46C")),
            ColorSwatch.Create(Color.Parse("#0090FF")),
            ColorSwatch.Create(Color.Parse("#8E4EC6"))
        ];
    }
}
