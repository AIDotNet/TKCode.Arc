using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the card looping on its own, held at each stage, or empty.</summary>
public partial class SketchToImagePreviewDemo : UserControl
{
    private readonly bool _ready;

    public SketchToImagePreviewDemo()
    {
        InitializeComponent();
        _ready = true;
    }

    private void OnStageChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the card exists.
        if (!_ready)
        {
            return;
        }
        var value = Stages.SelectedValue as string;
        var empty = value == "empty";
        Card.Scene = empty ? null : SketchToImageDemoData.Alpine;
        Card.Prompt = empty ? null : SketchToImageDemoData.Prompt;
        Card.Status = Enum.TryParse<SketchToImageStatus>(value, out var status) ? status : null;
    }
}
