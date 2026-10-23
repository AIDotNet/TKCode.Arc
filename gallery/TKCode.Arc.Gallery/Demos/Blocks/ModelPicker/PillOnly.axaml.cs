using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ModelPickerPillOnlyDemo : UserControl
{
    public ModelPickerPillOnlyDemo()
    {
        InitializeComponent();
        Picker.Providers.AddRange(ModelPickerDemoData.Providers());
        Picker.Models.AddRange(ModelPickerDemoData.Models());
    }
}
