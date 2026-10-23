using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// How files, pictures and long pastes become <see cref="AiAttachment"/>s, and how an attachment is uploaded (Arc's AI
/// composer: picked, pasted or dropped files; pasted-text chips; uploads in place with a check that turns into a remove
/// button). Pure helpers: no control, no default text. Every word a person may see comes from the caller (the
/// <c>Func</c> parameters); without one the attachment carries no text and the chip shows its own fallback.
/// <para>
/// Call the methods on the UI thread: an <see cref="AiAttachment"/> is an <c>AvaloniaObject</c>. Work that may be slow
/// (reading a picture and decoding its thumbnail) runs on a worker thread and comes back to the caller's thread.
/// </para>
/// </summary>
internal static class AiFileIntake
{
    /// <summary>A paste of at least this many characters becomes a pasted-text chip (Arc: 1200).</summary>
    public const int LongPasteCharacters = 1200;

    /// <summary>A paste of at least this many lines becomes a pasted-text chip (Arc: 12).</summary>
    public const int LongPasteLines = 12;

    /// <summary>The width a thumbnail is decoded to: twice the chip's 38px picture, for sharp high-density screens.</summary>
    public const int ThumbnailPixelWidth = 76;

    /// <summary>How long the check stays after an upload before it turns into the remove button (Arc: about 0.9s).</summary>
    public static readonly TimeSpan DoneHold = TimeSpan.FromMilliseconds(900);

    // Pictures the platform decoder can usually read. SVG is left out (it has no bitmap to decode); HEIC and AVIF are in,
    // and fall back to a file chip where the platform cannot decode them.
    private static readonly HashSet<string> s_imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "gif", "webp", "bmp", "ico", "avif", "heic", "heif", "tif", "tiff"
    };

    private static readonly HashSet<string> s_codeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ts", "tsx", "js", "jsx", "mjs", "cjs", "cs", "fs", "vb", "py", "rb", "go", "rs", "java", "kt", "swift", "c", "h", "cpp",
        "hpp", "m", "php", "sh", "ps1", "sql", "json", "xml", "yaml", "yml", "toml", "html", "css", "scss", "vue", "svelte",
        "axaml", "xaml", "csproj", "lua", "dart", "r", "scala"
    };

    private static readonly HashSet<string> s_sheetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "csv", "tsv", "xls", "xlsx", "xlsm", "ods", "numbers"
    };

    private static readonly HashSet<string> s_archiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "zip", "gz", "tgz", "tar", "rar", "7z", "bz2", "xz"
    };

    // MIME types for Accept patterns such as "image/*" or "application/pdf" (the same table FileDropzone keeps).
    private static readonly Dictionary<string, string> s_mime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["png"] = "image/png", ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg", ["gif"] = "image/gif", ["webp"] = "image/webp",
        ["svg"] = "image/svg+xml", ["avif"] = "image/avif", ["heic"] = "image/heic", ["heif"] = "image/heif", ["bmp"] = "image/bmp",
        ["ico"] = "image/x-icon", ["tif"] = "image/tiff", ["tiff"] = "image/tiff",
        ["mp4"] = "video/mp4", ["mov"] = "video/quicktime", ["webm"] = "video/webm", ["m4v"] = "video/x-m4v", ["avi"] = "video/x-msvideo",
        ["mkv"] = "video/x-matroska",
        ["mp3"] = "audio/mpeg", ["wav"] = "audio/wav", ["ogg"] = "audio/ogg", ["m4a"] = "audio/mp4", ["flac"] = "audio/flac", ["aac"] = "audio/aac",
        ["pdf"] = "application/pdf", ["zip"] = "application/zip", ["gz"] = "application/gzip", ["tar"] = "application/x-tar",
        ["rar"] = "application/vnd.rar", ["7z"] = "application/x-7z-compressed", ["json"] = "application/json", ["xml"] = "application/xml",
        ["csv"] = "text/csv", ["txt"] = "text/plain", ["md"] = "text/markdown", ["html"] = "text/html", ["css"] = "text/css",
        ["js"] = "text/javascript", ["rtf"] = "application/rtf", ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    // Thumbnails decoded here (and only those) are disposed by Release; a thumbnail the app set itself is left alone.
    private static readonly ConditionalWeakTable<AiAttachment, IImage> s_ownedThumbnails = new();

    private static long s_nextId;

    #region Intake

    /// <summary>
    /// Turns picked, pasted or dropped items into attachments, in order. Folders are skipped. The size comes from the
    /// item's properties (null when the provider cannot tell). A file whose type does not match <paramref name="accept"/>
    /// or that is larger than <paramref name="maxFileSize"/> is still returned, with <see cref="AiAttachmentStatus.Error"/>
    /// and the reason from <paramref name="notAcceptedText"/> (given the file name) or <paramref name="tooLargeText"/>
    /// (given the limit); such a chip can be removed and does not block sending. Pictures get a thumbnail (decoded on a
    /// worker thread before the method returns); a picture that cannot be decoded becomes a plain file. Every other
    /// attachment is <see cref="AiAttachmentStatus.Ready"/>: start <see cref="RunUploadAsync"/> right after adding it.
    /// </summary>
    /// <param name="items">The items, as the picker, the clipboard or a drop gave them.</param>
    /// <param name="maxFileSize">The largest accepted file in bytes; null accepts any size.</param>
    /// <param name="accept">Accepted types like an HTML file input (".pdf,image/*,text/csv"); null or empty accepts everything.</param>
    /// <param name="ct">Stops the intake; an <see cref="OperationCanceledException"/> is thrown.</param>
    /// <param name="tooLargeText">The short reason for a file over the limit ("Larger than 30 MB"); null leaves <see cref="AiAttachment.Error"/> empty.</param>
    /// <param name="notAcceptedText">The short reason for a file of a type that is not accepted; null leaves it empty.</param>
    /// <param name="decodeThumbnail">Decodes a picture to a width in pixels; null uses <see cref="Bitmap.DecodeToWidth(Stream, int, BitmapInterpolationMode)"/>.</param>
    public static async Task<IReadOnlyList<AiAttachment>> FromStorageAsync(IEnumerable<IStorageItem> items, long? maxFileSize, string? accept,
        CancellationToken ct, Func<long, string>? tooLargeText = null, Func<string, string>? notAcceptedText = null,
        Func<Stream, int, IImage>? decodeThumbnail = null)
    {
        var accepted = ParseAccept(accept);
        var result = new List<AiAttachment>();
        foreach (var file in items.OfType<IStorageFile>())
        {
            ct.ThrowIfCancellationRequested();
            var size = await ReadSizeAsync(file);
            ct.ThrowIfCancellationRequested();
            var name = file.Name;
            var attachment = new AiAttachment
            {
                Id = NewId(),
                Name = name,
                File = file,
                Size = size,
                Kind = IsImage(name) ? AiAttachmentKind.Image : AiAttachmentKind.File,
                Status = AiAttachmentStatus.Ready
            };
            if (!Matches(name, accepted))
            {
                Refuse(attachment, notAcceptedText?.Invoke(name));
            }
            else if (maxFileSize is { } max && size > max)
            {
                Refuse(attachment, tooLargeText?.Invoke(max));
            }
            result.Add(attachment);
        }
        // Thumbnails decode side by side; a refused picture is not read at all.
        var thumbnails = result.Where(a => a.Kind == AiAttachmentKind.Image && a.Status != AiAttachmentStatus.Error)
                               .Select(a => LoadThumbnailAsync(a, ct, decodeThumbnail))
                               .ToList();
        if (thumbnails.Count > 0)
        {
            await Task.WhenAll(thumbnails);
        }
        return result;
    }

    /// <summary>
    /// A pasted-text attachment for a long paste: <see cref="AiAttachmentKind.PastedText"/>, ready to send, its size the
    /// UTF-8 length of the text and its meta line <paramref name="linesText"/> of the line count ("24 lines").
    /// </summary>
    /// <param name="text">The pasted text, kept whole in <see cref="AiAttachment.PastedText"/>.</param>
    /// <param name="name">The chip's name ("Pasted snippet").</param>
    /// <param name="linesText">The meta line for a number of lines; null leaves <see cref="AiAttachment.Meta"/> empty (the chip then makes one).</param>
    public static AiAttachment FromPastedText(string text, string name, Func<int, string>? linesText = null)
    {
        return new AiAttachment
        {
            Id = NewId(),
            Name = name,
            Kind = AiAttachmentKind.PastedText,
            PastedText = text,
            Size = Encoding.UTF8.GetByteCount(text),
            Status = AiAttachmentStatus.Ready,
            Meta = linesText?.Invoke(CountLines(text))
        };
    }

    /// <summary>
    /// Whether a paste is long enough to become a chip instead of text: at least <paramref name="characters"/> characters
    /// (UTF-16 units, like Arc's <c>length</c>) or at least <paramref name="lines"/> lines. 0 (or less) turns that rule off.
    /// </summary>
    public static bool IsLongPaste(string? text, int characters = LongPasteCharacters, int lines = LongPasteLines)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        return (characters > 0 && text.Length >= characters) || (lines > 0 && CountLines(text) >= lines);
    }

    /// <summary>
    /// The number of lines in <paramref name="text"/>: "\r\n", "\n" and "\r" each end a line, and a line break at the very
    /// end does not start another (a copied block ending in a newline has as many lines as it shows). Empty text has none.
    /// </summary>
    public static int CountLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }
            if ((c == '\r' || c == '\n') && i + 1 < text.Length)
            {
                lines++;
            }
        }
        return lines;
    }

    /// <summary>A new attachment id, unique in this process ("attachment-12").</summary>
    public static string NewId() => "attachment-" + Interlocked.Increment(ref s_nextId).ToString(CultureInfo.InvariantCulture);

    private static async Task<long?> ReadSizeAsync(IStorageFile file)
    {
        try
        {
            var properties = await file.GetBasicPropertiesAsync();
            return properties.Size is { } bytes ? (long)Math.Min(bytes, long.MaxValue) : null;
        }
        catch (Exception)
        {
            // Some providers cannot report properties; the file still goes in, without a size.
            return null;
        }
    }

    private static void Refuse(AiAttachment attachment, string? reason)
    {
        attachment.Status = AiAttachmentStatus.Error;
        attachment.Error = reason;
    }

    #endregion

    #region Thumbnails

    /// <summary>
    /// Reads the attachment's picture and decodes a thumbnail <see cref="ThumbnailPixelWidth"/> pixels wide on a worker
    /// thread, then sets <see cref="AiAttachment.Thumbnail"/>. A picture that cannot be read or decoded becomes a plain
    /// <see cref="AiAttachmentKind.File"/> with no thumbnail. Returns whether a thumbnail was set.
    /// </summary>
    /// <param name="attachment">An attachment with a <see cref="AiAttachment.File"/>.</param>
    /// <param name="ct">Stops the read; an <see cref="OperationCanceledException"/> is thrown and nothing changes.</param>
    /// <param name="decode">Decodes a picture to a width in pixels; null uses <see cref="Bitmap.DecodeToWidth(Stream, int, BitmapInterpolationMode)"/>.</param>
    public static async Task<bool> LoadThumbnailAsync(AiAttachment attachment, CancellationToken ct, Func<Stream, int, IImage>? decode = null)
    {
        if (attachment.File is not { } file)
        {
            return false;
        }
        decode ??= static (stream, width) => Bitmap.DecodeToWidth(stream, width);
        IImage? image;
        try
        {
            // The provider's stream is read here (asynchronously; some providers want their own thread) into memory,
            // since it may not seek; only the decode moves to a worker thread.
            using var buffer = new MemoryStream();
            await using (var stream = await file.OpenReadAsync())
            {
                await stream.CopyToAsync(buffer, ct);
            }
            buffer.Position = 0;
            image = await Task.Run(() => decode(buffer, ThumbnailPixelWidth), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            image = null;
        }
        if (image is null)
        {
            attachment.Thumbnail = null;
            if (attachment.Kind == AiAttachmentKind.Image)
            {
                attachment.Kind = AiAttachmentKind.File;
            }
            return false;
        }
        if (ct.IsCancellationRequested)
        {
            (image as IDisposable)?.Dispose();
            ct.ThrowIfCancellationRequested();
        }
        Release(attachment);
        s_ownedThumbnails.AddOrUpdate(attachment, image);
        attachment.Thumbnail = image;
        return true;
    }

    /// <summary>
    /// Lets go of a thumbnail decoded by <see cref="LoadThumbnailAsync"/> (or <see cref="FromStorageAsync"/>): it is
    /// disposed and <see cref="AiAttachment.Thumbnail"/> cleared. Call it when the attachment leaves the composer. A
    /// thumbnail the app set itself is left as it is.
    /// </summary>
    public static void Release(AiAttachment attachment)
    {
        if (!s_ownedThumbnails.TryGetValue(attachment, out var owned))
        {
            return;
        }
        s_ownedThumbnails.Remove(attachment);
        if (ReferenceEquals(attachment.Thumbnail, owned))
        {
            attachment.Thumbnail = null;
        }
        (owned as IDisposable)?.Dispose();
    }

    #endregion

    #region Upload

    /// <summary>
    /// Uploads an attachment in place. Without a <paramref name="handler"/> the attachment is simply
    /// <see cref="AiAttachmentStatus.Ready"/>. Otherwise it is <see cref="AiAttachmentStatus.Uploading"/> (set before the
    /// first await, progress 0) while the handler runs; the handler reports progress from 0 to 1 on any thread, and the
    /// shown progress never moves back. When it finishes the attachment is <see cref="AiAttachmentStatus.Done"/> (progress
    /// 1) for <paramref name="doneHold"/>, then <see cref="AiAttachmentStatus.Ready"/>. A handler that throws leaves it in
    /// <see cref="AiAttachmentStatus.Error"/> with the exception's message, or <paramref name="failedText"/> when the
    /// message is empty. Cancelling <paramref name="ct"/> stops everything and changes nothing more: the caller decides
    /// (the composer removes the chip). An attachment that is already in error (refused at intake) is not uploaded.
    /// </summary>
    /// <param name="attachment">The attachment to upload.</param>
    /// <param name="handler">The app's upload (the same shape as <c>FileDropzone.UploadHandler</c>, with progress 0 to 1); null means nothing to upload.</param>
    /// <param name="doneHold">How long the check shows before the remove button (<see cref="DoneHold"/>).</param>
    /// <param name="delay">Waits a time span (<see cref="DelayAsync"/>); tests pass their own clock.</param>
    /// <param name="failedText">The short reason when the handler's exception has no message ("Upload failed").</param>
    /// <param name="ct">Cancels the upload.</param>
    public static async Task RunUploadAsync(AiAttachment attachment, Func<AiAttachment, IProgress<double>, CancellationToken, Task>? handler,
        TimeSpan doneHold, Func<TimeSpan, CancellationToken, Task> delay, Func<string> failedText, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || attachment.Status == AiAttachmentStatus.Error)
        {
            return;
        }
        if (handler is null)
        {
            attachment.Error = null;
            attachment.Status = AiAttachmentStatus.Ready;
            return;
        }
        attachment.Error = null;
        attachment.Progress = 0;
        attachment.Status = AiAttachmentStatus.Uploading;
        try
        {
            await handler(attachment, new UploadProgress(attachment, ct), ct);
        }
        catch (Exception exception)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }
            attachment.Error = string.IsNullOrWhiteSpace(exception.Message) ? failedText() : exception.Message;
            attachment.Status = AiAttachmentStatus.Error;
            return;
        }
        if (ct.IsCancellationRequested)
        {
            return;
        }
        attachment.Progress = 1;
        attachment.Status = AiAttachmentStatus.Done;
        try
        {
            await delay(doneHold, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        // Someone else may have moved it on (a retry, an error set by the app) while the check showed.
        if (!ct.IsCancellationRequested && attachment.Status == AiAttachmentStatus.Done)
        {
            attachment.Status = AiAttachmentStatus.Ready;
        }
    }

    /// <summary>The real clock for <see cref="RunUploadAsync"/>: <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
    public static Task DelayAsync(TimeSpan time, CancellationToken ct) => Task.Delay(time, ct);

    /// <summary>Upload progress applied on the UI thread whatever thread reports it; clamped to 0..1, never decreasing, ignored once cancelled or no longer uploading.</summary>
    private sealed class UploadProgress(AiAttachment attachment, CancellationToken ct) : IProgress<double>
    {
        public void Report(double value)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply(value);
            }
            else
            {
                Dispatcher.UIThread.Post(() => Apply(value));
            }
        }

        private void Apply(double value)
        {
            if (ct.IsCancellationRequested || double.IsNaN(value) || attachment.Status != AiAttachmentStatus.Uploading)
            {
                return;
            }
            var clamped = Math.Clamp(value, 0, 1);
            if (clamped > attachment.Progress)
            {
                attachment.Progress = clamped;
            }
        }
    }

    #endregion

    #region Names, types and sizes

    /// <summary>Whether a file name is a picture that gets a thumbnail (png, jpg, gif, webp, bmp, …; not svg).</summary>
    public static bool IsImage(string? fileName) => Extension(fileName) is { } extension && s_imageExtensions.Contains(extension);

    /// <summary>
    /// A byte count the way the chip shows it, in invariant culture with 1024-based units: "12 B", "469 KB" (whole
    /// kilobytes), "3.1 MB" (one decimal below 10), "24 MB", "1.2 GB".
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return Math.Max(0, bytes).ToString(CultureInfo.InvariantCulture) + " B";
        }
        var kilobytes = Math.Round(bytes / 1024.0, MidpointRounding.AwayFromZero);
        if (kilobytes < 1024)
        {
            return kilobytes.ToString(CultureInfo.InvariantCulture) + " KB";
        }
        string[] units = ["MB", "GB", "TB"];
        var value = bytes / (1024.0 * 1024.0);
        var unit = 0;
        while (unit < units.Length - 1 && Rounded(value) >= 1024)
        {
            value /= 1024;
            unit++;
        }
        return Rounded(value).ToString(value < 9.95 ? "0.#" : "0", CultureInfo.InvariantCulture) + " " + units[unit];

        static double Rounded(double v) => v < 9.95 ? Math.Round(v, 1, MidpointRounding.AwayFromZero) : Math.Round(v, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The chip's second line when <see cref="AiAttachment.Meta"/> is not set: the type and the size, "PDF, 469 KB" (the
    /// extension in capitals). Pictures say <paramref name="imageText"/> ("Picture, 240 KB"; the extension when null); a
    /// pasted text says its lines through <paramref name="linesText"/> ("24 lines"; the number alone when null). A part
    /// that is unknown is left out; nothing known gives an empty line.
    /// </summary>
    public static string DefaultMeta(AiAttachment attachment, string? imageText = null, Func<int, string>? linesText = null)
    {
        if (attachment.Kind == AiAttachmentKind.PastedText)
        {
            var lines = CountLines(attachment.PastedText);
            return linesText?.Invoke(lines) ?? lines.ToString(CultureInfo.InvariantCulture);
        }
        var extension = Extension(attachment.Name)?.ToUpperInvariant();
        var type = attachment.Kind == AiAttachmentKind.Image && !string.IsNullOrEmpty(imageText) ? imageText : extension;
        var size = attachment.Size is { } bytes ? FormatBytes(bytes) : null;
        return (type, size) switch
        {
            ({ Length: > 0 }, not null) => type + ", " + size,
            ({ Length: > 0 }, null) => type,
            (_, not null) => size,
            _ => string.Empty
        };
    }

    /// <summary>
    /// The 24 × 24 icon of an attachment without a thumbnail: its own <see cref="AiAttachment.Icon"/>, else by kind and
    /// extension: clipboard for pasted text, picture, code, spreadsheet, archive, and a page of text for anything else (as Arc does).
    /// </summary>
    public static Geometry IconFor(AiAttachment attachment)
    {
        if (attachment.Icon is { } icon)
        {
            return icon;
        }
        if (attachment.Kind == AiAttachmentKind.PastedText)
        {
            return ArcIcons.ClipboardPaste;
        }
        if (attachment.Kind == AiAttachmentKind.Image)
        {
            return ArcIcons.Image;
        }
        var extension = Extension(attachment.Name);
        if (extension is not null && s_codeExtensions.Contains(extension))
        {
            return ArcIcons.FileCode;
        }
        if (extension is not null && s_sheetExtensions.Contains(extension))
        {
            return ArcIcons.FileSpreadsheet;
        }
        if (extension is not null && s_archiveExtensions.Contains(extension))
        {
            return ArcIcons.FileArchive;
        }
        return ArcIcons.FileText;
    }

    /// <summary>The extension without the dot ("pdf"), or null when the name has none.</summary>
    private static string? Extension(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        var dot = name.LastIndexOf('.');
        return dot <= 0 || dot == name.Length - 1 ? null : name[(dot + 1)..];
    }

    #endregion

    #region Accept

    /// <summary>The accept list, lower case, empty entries dropped.</summary>
    public static List<string> ParseAccept(string? accept) =>
        (accept ?? string.Empty).Split(',').Select(value => value.Trim().ToLowerInvariant()).Where(value => value.Length > 0).ToList();

    /// <summary>
    /// Whether a file name matches an accept list the way an HTML file input does: ".ext" matches the name's end,
    /// "type/*" a MIME prefix, anything else the exact MIME type. An empty list accepts everything.
    /// </summary>
    public static bool Matches(string name, IReadOnlyList<string> accepted)
    {
        if (accepted.Count == 0)
        {
            return true;
        }
        var lower = name.ToLowerInvariant();
        var mime = Extension(name) is { } extension && s_mime.TryGetValue(extension, out var known) ? known : string.Empty;
        foreach (var type in accepted)
        {
            if (type.StartsWith('.') ? lower.EndsWith(type, StringComparison.Ordinal)
                : type.EndsWith("/*", StringComparison.Ordinal) ? mime.StartsWith(type[..^1], StringComparison.Ordinal)
                : mime == type)
            {
                return true;
            }
        }
        return false;
    }

    #endregion
}
