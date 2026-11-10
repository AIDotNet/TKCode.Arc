using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvatarUploadStatesDemo : UserControl
{
    public AvatarUploadStatesDemo()
    {
        InitializeComponent();
        WithPicture.Source = AvatarUploadSamplePhoto.Create(320, 320);
        Manual.Source = AvatarUploadSamplePhoto.Create(320, 320);
    }

    private void OnProgress(object? sender, RoutedEventArgs e) => Manual.ReportProgress(0.4);

    private void OnComplete(object? sender, RoutedEventArgs e) => Manual.CompleteUpload();

    private void OnFail(object? sender, RoutedEventArgs e) => Manual.FailUpload("The server refused the file.");
}
