using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: one card for the highlighted model of a small catalog of real models.</summary>
public partial class ModelCardPreviewDemo : UserControl
{
    private readonly bool _ready;

    public ModelCardPreviewDemo()
    {
        InitializeComponent();
        Picker.ItemsSource = ModelCardDemoData.Frontier.Select(m => new SegmentedOption(m.Id, m.Name)).ToList();
        Picker.SelectedIndex = 0;
        _ready = true;
    }

    private void OnPicked(object? sender, SelectionChangedEventArgs e)
    {
        // The segmented control may select while the XAML loads, before the card is set up.
        if (_ready && Picker.SelectedValue is string id)
        {
            Card.Model = ModelCardDemoData.Frontier.First(m => m.Id == id);
        }
    }
}
