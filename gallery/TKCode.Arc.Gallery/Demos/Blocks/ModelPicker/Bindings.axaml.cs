using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ModelPickerBindingsDemo : UserControl
{
    public ModelPickerBindingsDemo()
    {
        InitializeComponent();
        Picker.Providers.AddRange(ModelPickerDemoData.Providers());
        Picker.Models.AddRange(ModelPickerDemoData.Models());
        Show("Start");
    }

    private void Show(string what) =>
        Log.Text = $"{what}: {Picker.SelectedModel?.Name ?? "no model"}, effort {(Picker.Effort is { } effort ? ModelEffortNames.Name(effort) : "none")}";

    // The events fire after a choice in the menu, a snap to a level the model offers, or a change made in code.
    private void OnModelChanged(object? sender, ModelPickerModelChangedEventArgs e) => Show("Model changed");

    private void OnEffortChanged(object? sender, ModelPickerEffortChangedEventArgs e) =>
        Show(e.IsSnap ? "Effort adjusted to fit the model" : "Effort changed");

    private void OnUseSwift(object? sender, RoutedEventArgs e) => Picker.SelectedModelId = "aurora-swift-3";

    // The model offers Low, Medium and High: a level it does not offer moves to the nearest one.
    private void OnMaxEffort(object? sender, RoutedEventArgs e) => Picker.Effort = ModelEffort.Max;
}
