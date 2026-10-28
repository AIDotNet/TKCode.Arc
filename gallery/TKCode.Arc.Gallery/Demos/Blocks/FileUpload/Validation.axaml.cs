using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class FileUploadValidationDemo : UserControl
{
    public FileUploadValidationDemo()
    {
        InitializeComponent();
    }

    private void OnAddSamples(object? sender, RoutedEventArgs e) =>
        Upload.AddFiles(
        [
            new FileUploadItem(null, "contacts.csv", 38_912),
            new FileUploadItem(null, "contacts-backup.zip", 204_800),
            new FileUploadItem(null, "contacts.pdf", 3_145_728),
        ]);
}
