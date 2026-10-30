using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TKCode.Arc.Gallery.Demos;

public partial class DesignToCodeDropZoneDemo : UserControl
{
    private static readonly FilePickerFileType s_images = new("Screenshots")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"],
        MimeTypes = ["image/png", "image/jpeg", "image/webp"],
    };

    public DesignToCodeDropZoneDemo()
    {
        InitializeComponent();
    }

    private async void OnChooseFile(object? sender, RoutedEventArgs e)
    {
        // Handled: the card does not fall back to its simulation's design.
        e.Handled = true;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            Card.UseSample();
            return;
        }
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a UI screenshot",
            AllowMultiple = false,
            FileTypeFilter = [s_images],
        });
        if (files.Count > 0)
        {
            await Card.LoadFileAsync(files[0]);
        }
    }
}
