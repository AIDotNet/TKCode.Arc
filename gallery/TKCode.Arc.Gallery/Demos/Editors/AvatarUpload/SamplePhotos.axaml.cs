using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvatarUploadSamplePhotosDemo : UserControl
{
    public AvatarUploadSamplePhotosDemo()
    {
        InitializeComponent();
        Upload.UploadHandler = UploadAsync;
        for (var i = 0; i < AvatarUploadSamplePhoto.People.Length; i++)
        {
            var name = AvatarUploadSamplePhoto.People[i].Name;
            var photo = AvatarUploadSamplePhoto.Create(i, 320, 320);
            var button = new Button
            {
                Variant = ButtonVariant.Ghost,
                Padding = new Avalonia.Thickness(4),
                Content = new Avatar { Size = AvatarSize.Large, DisplayName = name, Source = ImageSource.FromBitmap(photo) }
            };
            AutomationProperties.SetName(button, "Use " + name + " as the current photo");
            button.Click += (_, _) => Upload.Source = photo;
            Samples.Children.Add(button);
        }
    }

    // Stands in for sending request.Bytes to your server.
    private static async Task UploadAsync(AvatarUploadRequest request, IProgress<double> progress, CancellationToken cancel)
    {
        for (var step = 1; step <= 12; step++)
        {
            await Task.Delay(80, cancel);
            progress.Report(step / 12.0);
        }
    }
}
