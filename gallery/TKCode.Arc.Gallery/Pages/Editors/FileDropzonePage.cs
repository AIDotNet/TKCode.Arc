using Avalonia.Controls;
using Avalonia.Threading;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Pages;

public static class FileDropzonePage
{
    public static Control Create() => GalleryUi.Column(20,
        GalleryUi.Card("Upload",
            "Drop files from your system, click to choose, or paste a screenshot while the pointer is over the target. Each file uploads with its own progress; every third one fails so Retry shows.",
            Frame(new FileDropzone { UploadHandler = SimulatedUpload(), MaxFiles = 6 })),
        GalleryUi.Card("Images up to 2 MB",
            "Accept takes extensions and MIME patterns; a refused type or too many files shakes the target, and a large file fails without Retry.",
            Frame(new FileDropzone
            {
                Accept = "image/*",
                MaxFileSize = 2 * 1024 * 1024,
                MaxFiles = 3,
                Label = "Add images",
                Description = "PNG, JPG or WebP, up to 2 MB each"
            })),
        GalleryUi.Card("List inside, folding prompt",
            "Rows can live inside the target's edge; with CompactAt the prompt folds to a slim bar once the list fills up. Rows present at load render without an entrance.",
            Frame(Inside())),
        GalleryUi.Card("Single file",
            "Without AllowMultiple a new file replaces the current one.",
            Frame(new FileDropzone { AllowMultiple = false, Label = "Add a contract", Accept = ".pdf" })),
        GalleryUi.Card("App-driven progress",
            "Without an upload handler the app drives each row: ReportProgress, CompleteUpload and FailUpload (or set the item's Status and Progress).",
            Frame(AppDriven())));

    private static Border Frame(Control content) => new() { MaxWidth = 560, Child = content };

    /// <summary>A fake upload: progress in steps over about two seconds; every third file fails halfway.</summary>
    private static Func<FileDropzoneItem, IProgress<double>, CancellationToken, Task> SimulatedUpload()
    {
        var count = 0;
        return async (item, progress, token) =>
        {
            var fails = ++count % 3 == 0;
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
        };
    }

    private static Control Inside()
    {
        var zone = new FileDropzone { ListPlacement = FileDropzoneListPlacement.Inside, CompactAt = 2, UploadHandler = SimulatedUpload() };
        zone.Items.Add(new FileDropzoneItem("brand-guidelines.pdf", 3_400_000, FileDropzoneStatus.Uploaded));
        return zone;
    }

    private static Control AppDriven()
    {
        var zone = new FileDropzone { Label = "Queue files", Description = "The app uploads them in its own queue" };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += (_, _) =>
        {
            var busy = false;
            foreach (var item in zone.Items.ToList())
            {
                if (item.Status is FileDropzoneStatus.None or FileDropzoneStatus.Uploading)
                {
                    busy = true;
                    var next = item.Progress + 7;
                    if (next >= 100)
                    {
                        zone.CompleteUpload(item);
                    }
                    else
                    {
                        zone.ReportProgress(item, next);
                    }
                }
            }
            if (!busy)
            {
                timer.Stop();
            }
        };
        zone.FilesChanged += (_, _) => timer.Start();
        zone.DetachedFromVisualTree += (_, _) => timer.Stop();
        return zone;
    }
}
