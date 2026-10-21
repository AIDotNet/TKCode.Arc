using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class FileDropzoneAppDrivenDemo : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(180) };

    public FileDropzoneAppDrivenDemo()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void OnFilesChanged(object? sender, FileDropzoneFilesChangedEventArgs e) => _timer.Start();

    // The app is the uploader: it moves every pending row forward and finishes it at 100.
    private void OnTick(object? sender, EventArgs e)
    {
        var busy = false;
        foreach (var item in Zone.Items.ToList())
        {
            if (item.Status is FileDropzoneStatus.None or FileDropzoneStatus.Uploading)
            {
                busy = true;
                var next = item.Progress + 7;
                if (next >= 100)
                {
                    Zone.CompleteUpload(item);
                }
                else
                {
                    Zone.ReportProgress(item, next);
                }
            }
        }
        if (!busy)
        {
            _timer.Stop();
        }
    }
}
