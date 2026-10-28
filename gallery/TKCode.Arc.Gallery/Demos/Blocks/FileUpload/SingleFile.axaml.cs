using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class FileUploadSingleFileDemo : UserControl
{
    public FileUploadSingleFileDemo()
    {
        InitializeComponent();
        Upload.Uploader = FileUploadDemoData.CreateUploader();
    }
}
