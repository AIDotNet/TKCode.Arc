using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace TKCode.Arc;

/// <summary>Where <see cref="FileDropzone"/> lists its files.</summary>
public enum FileDropzoneListPlacement
{
    /// <summary>Under the drop target (Arc's default).</summary>
    Below,

    /// <summary>Inside the target's edge.</summary>
    Inside
}

/// <summary>Upload state of a <see cref="FileDropzoneItem"/>.</summary>
public enum FileDropzoneStatus
{
    /// <summary>A plain selection: nothing is uploading.</summary>
    None,

    /// <summary>Uploading; <see cref="FileDropzoneItem.Progress"/> goes from 0 to 100.</summary>
    Uploading,

    /// <summary>Uploaded.</summary>
    Uploaded,

    /// <summary>Failed; <see cref="FileDropzoneItem.Error"/> holds the reason.</summary>
    Failed
}

/// <summary>
/// One row of a <see cref="FileDropzone"/>. Every property notifies, so an app can drive an upload itself by setting
/// <see cref="Status"/> and <see cref="Progress"/> (or through <see cref="FileDropzone.ReportProgress"/> and friends).
/// </summary>
public sealed class FileDropzoneItem : INotifyPropertyChanged
{
    private static int s_next;
    private FileDropzoneStatus _status;
    private double _progress;
    private string? _error;
    private bool _isRetryable = true;
    private Bitmap? _preview;

    /// <summary>A row for a file that is not on this device (rows uploaded earlier, Arc's <c>defaultItems</c>).</summary>
    public FileDropzoneItem(string name, long size, FileDropzoneStatus status = FileDropzoneStatus.None)
    {
        Id = $"file-{Interlocked.Increment(ref s_next)}";
        Name = name;
        Size = size;
        _status = status;
        _progress = status == FileDropzoneStatus.Uploaded ? 100 : 0;
    }

    internal FileDropzoneItem(IStorageFile file, string name, long size, DateTimeOffset? modified) : this(name, size)
    {
        File = file;
        Modified = modified;
    }

    /// <summary>A stable identifier.</summary>
    public string Id { get; init; }

    /// <summary>The file name shown in the row.</summary>
    public string Name { get; }

    /// <summary>The size in bytes.</summary>
    public long Size { get; }

    /// <summary>The file the visitor added; null for rows created by the app.</summary>
    public IStorageFile? File { get; }

    internal DateTimeOffset? Modified { get; }

    /// <summary>True when the preview bitmap was made by the dropzone (it releases it with the row).</summary>
    internal bool OwnsPreview { get; set; }

    /// <summary>Upload state.</summary>
    public FileDropzoneStatus Status { get => _status; set => Set(ref _status, value); }

    /// <summary>Upload progress from 0 to 100.</summary>
    public double Progress { get => _progress; set => Set(ref _progress, Math.Clamp(value, 0, 100)); }

    /// <summary>Why the upload failed (shown in the row).</summary>
    public string? Error { get => _error; set => Set(ref _error, value); }

    /// <summary>False hides Retry for a failure that retrying cannot fix (such as a file that is too large).</summary>
    public bool IsRetryable { get => _isRetryable; set => Set(ref _isRetryable, value); }

    /// <summary>A thumbnail; image files added by the visitor get one automatically.</summary>
    public Bitmap? Preview { get => _preview; set => Set(ref _preview, value); }

    /// <summary>Raised when any property of this row changes.</summary>
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Arc's size label: bytes, whole KB, or MB with one decimal under 10.</summary>
    public static string FormatSize(long bytes)
    {
        const double mb = 1024 * 1024;
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        if (bytes < mb)
        {
            return $"{Math.Round(bytes / 1024.0, MidpointRounding.AwayFromZero).ToString(System.Globalization.CultureInfo.InvariantCulture)} KB";
        }
        var value = bytes / mb;
        var text = value >= 10
            ? Math.Round(value, MidpointRounding.AwayFromZero).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        return $"{text} MB";
    }
}

/// <summary>Arguments of <see cref="FileDropzone.FilesChanged"/>: the visitor's files after an add or a remove.</summary>
public sealed class FileDropzoneFilesChangedEventArgs : RoutedEventArgs
{
    internal FileDropzoneFilesChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<IStorageFile> files) : base(routedEvent)
    {
        Files = files;
    }

    /// <summary>The files of the rows the visitor added (rows created by the app have none).</summary>
    public IReadOnlyList<IStorageFile> Files { get; }
}

/// <summary>Arguments of <see cref="FileDropzone.FilesRejected"/>.</summary>
public sealed class FileDropzoneRejectedEventArgs : RoutedEventArgs
{
    internal FileDropzoneRejectedEventArgs(RoutedEvent routedEvent, string message, IReadOnlyList<string> names) : base(routedEvent)
    {
        Message = message;
        Names = names;
    }

    /// <summary>The message shown under the target (type not accepted, or too many files).</summary>
    public string Message { get; }

    /// <summary>The names of the files that were not added.</summary>
    public IReadOnlyList<string> Names { get; }
}
