// Ported from Arc's free file-upload block (MIT, Copyright (c) 2026 Elia Kuratli): file-upload.tsx (FileUploadItem).
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TKCode.Arc;

/// <summary>Where a <see cref="FileUploadItem"/>'s upload stands.</summary>
public enum FileUploadStatus
{
    /// <summary>Not uploaded: no <see cref="FileUpload.Uploader"/>, or the file was rejected.</summary>
    None,

    /// <summary>The uploader is running; <see cref="FileUploadItem.Progress"/> reports how far it got.</summary>
    Uploading,

    /// <summary>The uploader finished.</summary>
    Done,

    /// <summary>The uploader threw; the row offers Retry.</summary>
    Failed,
}

/// <summary>
/// A file in a <see cref="FileUpload"/> list: the picked or dropped file, its name and size, the validation error when it
/// was rejected, and the state of its upload.
/// </summary>
public sealed class FileUploadItem : AvaloniaObject
{
    private static long s_nextId;

    /// <summary>Why the file was rejected (wrong type or too large); null for an accepted file.</summary>
    public static readonly DirectProperty<FileUploadItem, string?> ErrorProperty =
        AvaloniaProperty.RegisterDirect<FileUploadItem, string?>(nameof(Error), o => o.Error, (o, v) => o.Error = v);

    /// <summary>Where the upload stands.</summary>
    public static readonly DirectProperty<FileUploadItem, FileUploadStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<FileUploadItem, FileUploadStatus>(nameof(Status), o => o.Status);

    /// <summary>How far the upload got, 0 to 100; it never goes back while the upload runs.</summary>
    public static readonly DirectProperty<FileUploadItem, double> ProgressProperty =
        AvaloniaProperty.RegisterDirect<FileUploadItem, double>(nameof(Progress), o => o.Progress);

    private string? _error;
    private FileUploadStatus _status;
    private double _progress;

    /// <summary>Creates an item for <paramref name="file"/> (null for a file that exists only by name and size, as in samples).</summary>
    public FileUploadItem(IStorageFile? file, string name, long size, DateTimeOffset? modified = null)
    {
        File = file;
        Name = name;
        Size = Math.Max(0, size);
        Modified = modified;
        Id = $"{name}-{modified?.ToUnixTimeMilliseconds() ?? 0}-{Interlocked.Increment(ref s_nextId)}";
    }

    /// <summary>A key unique to this item.</summary>
    public string Id { get; }

    /// <summary>The file, or null for an item made from a name and size only.</summary>
    public IStorageFile? File { get; }

    /// <summary>The file name shown in the row.</summary>
    public string Name { get; }

    /// <summary>The size in bytes.</summary>
    public long Size { get; }

    /// <summary>When the file was last changed, when the platform reports it.</summary>
    public DateTimeOffset? Modified { get; }

    /// <inheritdoc cref="ErrorProperty"/>
    public string? Error { get => _error; set => SetAndRaise(ErrorProperty, ref _error, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public FileUploadStatus Status { get => _status; internal set => SetAndRaise(StatusProperty, ref _status, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress { get => _progress; internal set => SetAndRaise(ProgressProperty, ref _progress, value); }

    /// <summary>The cancellation of the running upload, if any.</summary>
    internal CancellationTokenSource? Upload { get; set; }
}

/// <summary>Carries the file list after files were added or removed.</summary>
public sealed class FileUploadFilesChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<FileUploadItem> files) : RoutedEventArgs(routedEvent)
{
    /// <summary>The new list.</summary>
    public IReadOnlyList<FileUploadItem> Files { get; } = files;
}
