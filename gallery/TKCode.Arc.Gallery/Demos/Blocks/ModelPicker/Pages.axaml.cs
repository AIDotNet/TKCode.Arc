using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ModelPickerPagesDemo : UserControl
{
    public ModelPickerPagesDemo()
    {
        InitializeComponent();
        Picker.Providers.AddRange(ModelPickerDemoData.Providers());
        Picker.Models.AddRange(ModelPickerDemoData.Models());
        Picker.SummaryNote = ModelPickerDemoData.PriceNote;
    }
}
