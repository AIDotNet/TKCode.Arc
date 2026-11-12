using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvatarUploadPreviewDemo : UserControl
{
    public AvatarUploadPreviewDemo()
    {
        InitializeComponent();
        Upload.UploadHandler = UploadAsync;
    }

    // Stands in for sending request.Image to your server, reporting progress and honoring Cancel upload.
    private async Task UploadAsync(AvatarUploadRequest request, IProgress<double> progress, CancellationToken cancel)
    {
        for (var step = 1; step <= 20; step++)
        {
            await Task.Delay(90, cancel);
            progress.Report(step / 20.0);
            if (step == 14 && Fail.IsChecked == true)
            {
                throw new InvalidOperationException("The connection dropped. Try again.");
            }
        }
    }

    private void OnSample(object? sender, RoutedEventArgs e) => Upload.Edit(AvatarUploadSamplePhoto.Create(), "portrait.png");
}
