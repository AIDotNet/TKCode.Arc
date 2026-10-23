using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ModelPickerPreviewDemo : UserControl
{
    public ModelPickerPreviewDemo()
    {
        InitializeComponent();
        Picker.Providers.AddRange(ModelPickerDemoData.Providers());
        Picker.Models.AddRange(ModelPickerDemoData.Models());
        Picker.SummaryNote = ModelPickerDemoData.PriceNote;
    }
}
