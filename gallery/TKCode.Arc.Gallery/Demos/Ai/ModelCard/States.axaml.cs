using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The card ready, loading, failed and without a model.</summary>
public partial class ModelCardStatesDemo : UserControl
{
    private readonly bool _ready;

    public ModelCardStatesDemo()
    {
        InitializeComponent();
        Card.RetryRequested += OnRetry;
        _ready = true;
    }

    private void OnStateChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The segmented control applies SelectedValue while the XAML loads, before the constructor has run.
        if (_ready)
        {
            Show(State.SelectedValue as string ?? "ready");
        }
    }

    private void Show(string state)
    {
        Card.IsLoading = state == "loading";
        Card.ErrorText = state == "error" ? "The model catalog did not answer. Check your connection and try again." : null;
        Card.Model = state == "empty" ? null : ModelCardDemoData.Opus;
    }

    private void OnRetry(object? sender, RoutedEventArgs e) => State.SelectedValue = "ready";
}
