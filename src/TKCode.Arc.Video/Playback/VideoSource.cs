using System.Security.Cryptography;
using Avalonia;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>Video to load: an address (http, https, file or avares), a local file, encoded bytes in memory, a stream, or a simulated clip.</summary>
public abstract class VideoSource
{
    private protected VideoSource()
    {
    }

    /// <summary>Video at an http(s), file or avares address.</summary>
    public static VideoSource FromUri(Uri uri) => new UriVideoSource(uri ?? throw new ArgumentNullException(nameof(uri)));

    /// <summary>Video in a local file.</summary>
    public static VideoSource FromFile(string path) => new UriVideoSource(new Uri(Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)))));

    /// <summary>Encoded video in memory with its media type (<c>video/mp4</c>, <c>video/webm</c>).</summary>
    public static VideoSource FromBytes(ReadOnlyMemory<byte> data, string mediaType = "video/mp4") => new BytesVideoSource(data, mediaType);

    /// <summary>Encoded video read from a stream that <paramref name="open"/> returns; the player disposes the stream.</summary>
    public static VideoSource FromStream(Func<Stream> open, string mediaType = "video/mp4") => new StreamVideoSource(open ?? throw new ArgumentNullException(nameof(open)), mediaType);

    /// <summary>A generated test picture of the given size and length; only the simulated backend plays it.</summary>
    public static VideoSource FromSimulated(PixelSize size, TimeSpan duration) => new SimulatedVideoSource(size, duration);

    /// <summary>Parses <paramref name="text"/> as an absolute URI (http, https, file, avares) or a file path.</summary>
    public static VideoSource Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "file" or "avares")
        {
            return FromUri(uri);
        }
        return FromFile(text);
    }
}

/// <summary>A <see cref="VideoSource"/> at an address.</summary>
public sealed class UriVideoSource : VideoSource
{
    internal UriVideoSource(Uri uri)
    {
        Uri = uri;
    }

    /// <summary>The video's address.</summary>
    public Uri Uri { get; }

    /// <inheritdoc />
    public override string ToString() => Uri.ToString();
}

/// <summary>A <see cref="VideoSource"/> held in memory.</summary>
public sealed class BytesVideoSource : VideoSource
{
    internal BytesVideoSource(ReadOnlyMemory<byte> data, string mediaType)
    {
        Data = data;
        MediaType = mediaType;
    }

    /// <summary>The encoded video.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>The media type of <see cref="Data"/>.</summary>
    public string MediaType { get; }
}

/// <summary>A <see cref="VideoSource"/> read from a stream.</summary>
public sealed class StreamVideoSource : VideoSource
{
    internal StreamVideoSource(Func<Stream> open, string mediaType)
    {
        Open = open;
        MediaType = mediaType;
    }

    /// <summary>Opens the stream; the caller disposes it.</summary>
    public Func<Stream> Open { get; }

    /// <summary>The media type of the stream's content.</summary>
    public string MediaType { get; }
}

/// <summary>A generated test clip for <see cref="SimulatedVideoBackend"/>.</summary>
public sealed class SimulatedVideoSource : VideoSource
{
    internal SimulatedVideoSource(PixelSize size, TimeSpan duration)
    {
        Size = size;
        Duration = duration;
    }

    /// <summary>The picture size.</summary>
    public PixelSize Size { get; }

    /// <summary>The length.</summary>
    public TimeSpan Duration { get; }
}

/// <summary>
/// Turns a <see cref="VideoSource"/> into something a system player opens: an http(s) or file URI. Assets, bytes and streams are
/// written once to a temporary file named by their content hash (reused while the process runs, deleted on exit when possible).
/// Used by desktop backends.
/// </summary>
public static class VideoSourceFiles
{
    private static readonly string s_directory = Path.Combine(Path.GetTempPath(), "tkcode-arc-video", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private static readonly object s_gate = new();
    private static bool s_cleanupHooked;

    /// <summary>Returns an http(s) or file URI for <paramref name="source"/>.</summary>
    /// <exception cref="NotSupportedException">The source is a <see cref="SimulatedVideoSource"/> or has an unsupported scheme.</exception>
    public static async Task<Uri> MaterializeAsync(VideoSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        switch (source)
        {
            case UriVideoSource { Uri.Scheme: "http" or "https" or "file" } uriSource:
                return uriSource.Uri;
            case UriVideoSource { Uri.Scheme: "avares" } asset:
            {
                await using var stream = AssetLoader.Open(asset.Uri);
                return await WriteAsync(stream, Path.GetExtension(asset.Uri.AbsolutePath), cancellationToken).ConfigureAwait(false);
            }
            case BytesVideoSource bytes:
            {
                return await WriteAsync(bytes.Data, ExtensionOf(bytes.MediaType), cancellationToken).ConfigureAwait(false);
            }
            case StreamVideoSource streamSource:
            {
                await using var stream = streamSource.Open();
                return await WriteAsync(stream, ExtensionOf(streamSource.MediaType), cancellationToken).ConfigureAwait(false);
            }
            default:
                throw new NotSupportedException($"A {source.GetType().Name} cannot be opened by a system player.");
        }
    }

    /// <summary>The usual file extension for a video media type (<c>video/mp4</c> → <c>.mp4</c>).</summary>
    public static string ExtensionOf(string mediaType)
    {
        var bare = mediaType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return bare switch
        {
            "video/mp4" or "audio/mp4" => ".mp4",
            "video/quicktime" => ".mov",
            "video/webm" or "audio/webm" => ".webm",
            "video/x-matroska" => ".mkv",
            "video/ogg" => ".ogv",
            "video/x-msvideo" => ".avi",
            "video/mp2t" => ".ts",
            "application/vnd.apple.mpegurl" or "application/x-mpegurl" => ".m3u8",
            _ => ".bin",
        };
    }

    private const int CopyBufferSize = 81920;

    private static async Task<Uri> WriteAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        // Streamed into a temporary file while hashing, so a large stream never sits in memory; the file then takes the name of
        // its hash, or is dropped when a file with that content is already there.
        var temporary = Path.Combine(PrepareDirectory(), Guid.NewGuid().ToString("N") + ".part");
        string hash;
        try
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[CopyBufferSize];
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true))
            {
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hasher.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }
            hash = HashName(hasher.GetHashAndReset());
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
        var path = PathFor(hash, extension);
        if (File.Exists(path))
        {
            TryDelete(temporary);
            return new Uri(path);
        }
        try
        {
            File.Move(temporary, path);
        }
        catch (IOException) when (File.Exists(path))
        {
            TryDelete(temporary);
        }
        return new Uri(path);
    }

    private static async Task<Uri> WriteAsync(ReadOnlyMemory<byte> content, string extension, CancellationToken cancellationToken)
    {
        var path = PathFor(HashName(SHA256.HashData(content.Span)), extension);
        if (File.Exists(path))
        {
            return new Uri(path);
        }
        var temporary = Path.Combine(PrepareDirectory(), Guid.NewGuid().ToString("N") + ".part");
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true))
            {
                await file.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporary, path);
        }
        catch (IOException) when (File.Exists(path))
        {
            TryDelete(temporary);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
        return new Uri(path);
    }

    private static string HashName(byte[] hash) => Convert.ToHexString(hash)[..32].ToLowerInvariant();

    private static string PathFor(string hash, string extension) =>
        Path.Combine(PrepareDirectory(), hash + (string.IsNullOrEmpty(extension) ? ".bin" : extension));

    private static string PrepareDirectory()
    {
        lock (s_gate)
        {
            Directory.CreateDirectory(s_directory);
            HookCleanup();
        }
        return s_directory;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void HookCleanup()
    {
        if (s_cleanupHooked)
        {
            return;
        }
        s_cleanupHooked = true;
        AppDomain.CurrentDomain.ProcessExit += static (_, _) =>
        {
            try
            {
                Directory.Delete(s_directory, recursive: true);
            }
            catch (IOException)
            {
                // A system player may still hold a file open (Windows); the OS temp cleanup removes it later.
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
    }
}
