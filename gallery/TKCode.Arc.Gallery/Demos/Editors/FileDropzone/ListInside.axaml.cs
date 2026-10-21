using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class FileDropzoneListInsideDemo : UserControl
{
    private int _uploads;

    public FileDropzoneListInsideDemo()
    {
        InitializeComponent();
        Zone.UploadHandler = SimulateUploadAsync;
        Zone.Items.Add(new FileDropzoneItem("brand-guidelines.pdf", 3_400_000, FileDropzoneStatus.Uploaded));
    }

    // A fake upload: progress in steps over about two seconds; every third file fails halfway.
    private async Task SimulateUploadAsync(FileDropzoneItem item, IProgress<double> progress, CancellationToken token)
    {
        var fails = ++_uploads % 3 == 0;
        for (var percent = 0; percent <= 100; percent += 8)
        {
            await Task.Delay(140, token);
            progress.Report(percent);
            if (fails && percent >= 48)
            {
                throw new InvalidOperationException("The server closed the connection");
            }
        }
        progress.Report(100);
    }
}
