using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ResizablePanelsCollapsedAndPersistedDemo : UserControl
{
    private IReadOnlyList<double>? _saved;

    public ResizablePanelsCollapsedAndPersistedDemo()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _saved = Editor.Sizes;

    private void OnRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (_saved is not null)
        {
            Editor.Sizes = _saved;
        }
    }
}
