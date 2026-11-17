using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented attachments for the composer's attachment tests (spec section 10: no Arc sample data); real files and folders
/// in the test output folder opened through a window's storage provider (Avalonia's storage interfaces cannot be faked);
/// and a delay that waits for the test instead of the clock.
/// </summary>
internal static class AiAttachmentSamples
{
    private static readonly string s_root = Path.Combine(AppContext.BaseDirectory, "ai-attachment-fixtures");

    public static AiAttachment Sample(string name, long? size, AiAttachmentStatus status = AiAttachmentStatus.Ready, double progress = 0,
        string? error = null, AiAttachmentKind kind = AiAttachmentKind.File) => new()
    {
        Id = AiFileIntake.NewId(),
        Name = name,
        Size = size,
        Kind = kind,
        Status = status,
        Progress = progress,
        Error = error
    };

    /// <summary>A small two-colour picture standing in for a decoded thumbnail.</summary>
    public static IImage Thumbnail()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#7C8CF8"), 0), new GradientStop(Color.Parse("#F08AB0"), 1) }
        };
        var geometry = new RectangleGeometry(new Avalonia.Rect(0, 0, 76, 76));
        return new DrawingImage(new GeometryDrawing { Brush = brush, Geometry = geometry });
    }

    /// <summary>A fresh folder for one test's files.</summary>
    public static string NewFolder()
    {
        var folder = Path.Combine(s_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>A real file of <paramref name="size"/> bytes (zeros) in <paramref name="folder"/>, opened as an <see cref="IStorageFile"/>.</summary>
    public static async Task<IStorageFile> FileAsync(TopLevel top, string folder, string name, long size)
    {
        var path = Path.Combine(folder, name);
        await using (var stream = System.IO.File.Create(path))
        {
            stream.SetLength(size);
        }
        return await top.StorageProvider.TryGetFileFromPathAsync(path)
               ?? throw new InvalidOperationException("The headless storage provider cannot open files.");
    }

    /// <summary>A real folder inside <paramref name="folder"/>, opened as an <see cref="IStorageFolder"/>.</summary>
    public static async Task<IStorageFolder> FolderAsync(TopLevel top, string folder, string name)
    {
        var path = Path.Combine(folder, name);
        Directory.CreateDirectory(path);
        return await top.StorageProvider.TryGetFolderFromPathAsync(path)
               ?? throw new InvalidOperationException("The headless storage provider cannot open folders.");
    }

    /// <summary>A delay the test releases by hand; records what was asked for.</summary>
    public sealed class ManualDelay
    {
        private TaskCompletionSource? _pending;

        public List<TimeSpan> Requested { get; } = new();

        public bool IsWaiting => _pending is { Task.IsCompleted: false };

        public Task Delay(TimeSpan time, CancellationToken ct)
        {
            Requested.Add(time);
            var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = pending;
            ct.Register(() => pending.TrySetCanceled(ct));
            return pending.Task;
        }

        public void Release() => _pending?.TrySetResult();
    }
}
