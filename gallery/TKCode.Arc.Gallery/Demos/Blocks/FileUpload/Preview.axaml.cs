using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class FileUploadPreviewDemo : UserControl
{
    public FileUploadPreviewDemo()
    {
        InitializeComponent();
        Upload.Uploader = FileUploadDemoData.CreateUploader();
    }

    private void OnAddSamples(object? sender, RoutedEventArgs e) => Upload.AddFiles(FileUploadDemoData.Samples());
}
