using System.IO;

namespace TKCode.Arc;

/// <summary>What a <see cref="PdfSource"/> reads from.</summary>
public enum PdfSourceKind
{
    /// <summary>A local file (<see cref="PdfSource.FilePath"/>).</summary>
    File,

    /// <summary>Bytes in memory (<see cref="PdfSource.Bytes"/>).</summary>
    Bytes,

    /// <summary>A seekable stream (<see cref="PdfSource.OpenStream"/>).</summary>
    Stream,

    /// <summary>An http(s) or avares address (<see cref="PdfSource.Uri"/>).</summary>
    Uri
}

/// <summary>A PDF to open: a file, bytes, a stream or an address. In XAML, write an absolute URI or a path.</summary>
public sealed class PdfSource
{
    private PdfSource(PdfSourceKind kind, string? displayName)
    {
        Kind = kind;
        DisplayName = displayName;
    }

    /// <summary>What this source reads from.</summary>
    public PdfSourceKind Kind { get; }

    /// <summary>The path, when <see cref="Kind"/> is <see cref="PdfSourceKind.File"/>.</summary>
    public string? FilePath { get; private init; }

    /// <summary>The document, when <see cref="Kind"/> is <see cref="PdfSourceKind.Bytes"/>.</summary>
    public ReadOnlyMemory<byte> Bytes { get; private init; }

    /// <summary>Opens the document as a seekable stream, when <see cref="Kind"/> is <see cref="PdfSourceKind.Stream"/>. Each open calls it once; the backend disposes the stream.</summary>
    public Func<Stream>? OpenStream { get; private init; }

    /// <summary>The address, when <see cref="Kind"/> is <see cref="PdfSourceKind.Uri"/>.</summary>
    public Uri? Uri { get; private init; }

    /// <summary>A name for people, used as <see cref="PdfViewer.Title"/> when the document has no title of its own. Files and addresses default to their file name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>A local file.</summary>
    public static PdfSource FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new PdfSource(PdfSourceKind.File, Path.GetFileName(path)) { FilePath = path };
    }

    /// <summary>A document in memory.</summary>
    public static PdfSource FromBytes(ReadOnlyMemory<byte> data) => new(PdfSourceKind.Bytes, null) { Bytes = data };

    /// <summary>A seekable stream that <paramref name="open"/> returns; each open calls it once and the backend disposes the stream.</summary>
    public static PdfSource FromStream(Func<Stream> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        return new PdfSource(PdfSourceKind.Stream, null) { OpenStream = open };
    }

    /// <summary>An address: http(s), or avares (read through the asset loader). A file URI becomes <see cref="FromFile"/>.</summary>
    /// <exception cref="ArgumentException">The URI is relative or has another scheme.</exception>
    public static PdfSource FromUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https" or "avares" or "file"))
        {
            throw new ArgumentException("A PDF address must be an absolute http, https, avares or file URI.", nameof(uri));
        }
        if (uri.IsFile)
        {
            return FromFile(uri.LocalPath);
        }
        var name = Path.GetFileName(uri.AbsolutePath);
        return new PdfSource(PdfSourceKind.Uri, name.Length > 0 ? System.Uri.UnescapeDataString(name) : null) { Uri = uri };
    }

    /// <summary>Parses an absolute http, https, avares or file URI, or else a file path (the XAML form).</summary>
    public static PdfSource Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        // On Unix "/a/b.pdf" parses as a file URI; both roads end at the same path. Any other scheme is not an address we open.
        return System.Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "avares" or "file"
            ? FromUri(uri)
            : FromFile(text);
    }

    /// <summary>The same source with another <see cref="DisplayName"/>.</summary>
    public PdfSource WithDisplayName(string? displayName) => new(Kind, displayName)
    {
        FilePath = FilePath,
        Bytes = Bytes,
        OpenStream = OpenStream,
        Uri = Uri
    };

    /// <inheritdoc />
    public override string ToString() => DisplayName ?? Kind.ToString();
}
