using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The card as the detail pane of a picker, with Use model.</summary>
public partial class ModelCardPickerDemo : UserControl
{
    private readonly bool _ready;
    private string _selected = ModelCardDemoData.OpenWeights[1].Id;

    public ModelCardPickerDemo()
    {
        InitializeComponent();
        Picker.ItemsSource = ModelCardDemoData.OpenWeights.Select(m => new SegmentedOption(m.Id, m.Name)).ToList();
        Picker.SelectedIndex = 0;
        Card.SelectRequested += (_, e) =>
        {
            _selected = e.Model.Id;
            Show(e.Model);
        };
        Show(ModelCardDemoData.OpenWeights[0]);
        _ready = true;
    }

    private void OnPicked(object? sender, SelectionChangedEventArgs e)
    {
        // The segmented control may select while the XAML loads, before the card is set up.
        if (_ready && Picker.SelectedValue is string id)
        {
            Show(ModelCardDemoData.OpenWeights.First(m => m.Id == id));
        }
    }

    private void Show(ModelCardModel model)
    {
        Card.Model = model;
        Card.IsSelected = model.Id == _selected;
        Current.Text = "Default model: " + ModelCardDemoData.OpenWeights.First(m => m.Id == _selected).Name;
    }
}
