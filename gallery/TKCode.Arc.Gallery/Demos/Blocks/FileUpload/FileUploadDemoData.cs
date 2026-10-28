namespace TKCode.Arc.Gallery.Demos;

/// <summary>A simulated uploader and sample files for the File upload demos.</summary>
internal static class FileUploadDemoData
{
    /// <summary>
    /// Pretends to upload: progress in uneven steps over about two seconds. A file whose name contains "fail" fails
    /// halfway on its first attempt and succeeds on Retry.
    /// </summary>
    public static Func<FileUploadItem, IProgress<double>, CancellationToken, Task> CreateUploader()
    {
        var failed = new HashSet<string>();
        return async (item, progress, token) =>
        {
            var random = new Random(item.Name.GetHashCode());
            var percent = 0.0;
            while (percent < 100)
            {
                await Task.Delay(random.Next(120, 320), token);
                percent = Math.Min(100, percent + random.Next(8, 24));
                progress.Report(percent);
                if (percent >= 50 && item.Name.Contains("fail", StringComparison.OrdinalIgnoreCase) && failed.Add(item.Id))
                {
                    throw new InvalidOperationException("The server closed the connection.");
                }
            }
        };
    }

    /// <summary>Files that exist only by name and size, for trying the block without a file picker.</summary>
    public static FileUploadItem[] Samples() =>
    [
        new(null, "quarterly-report.pdf", 2_457_600),
        new(null, "team-offsite.jpg", 846_000),
        new(null, "upload-will-fail.zip", 12_582_912),
    ];
}
