using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MorphLoaderReducedMotionDemo : UserControl
{
    public MorphLoaderReducedMotionDemo()
    {
        InitializeComponent();
    }

    private void OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The event also fires while the XAML loads, before the named fields exist.
        if (Loader is null || Shape is null || Result is null)
        {
            return;
        }
        if (Enum.TryParse<MorphLoaderVariant>(Shape.SelectedValue as string, out var variant))
        {
            Loader.Variant = variant;
        }
        if (Enum.TryParse<MorphLoaderStatus>(Result.SelectedValue as string, out var status))
        {
            Loader.Status = status;
        }
    }
}
