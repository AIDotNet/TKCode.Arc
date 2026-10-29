using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace TKCode.Arc;

/// <summary>
/// Where a picture comes from: an address (<see cref="UriImageSource"/>: http(s), <c>avares://</c>, <c>file://</c> or
/// <c>data:</c>), a file the user picked (<see cref="StorageFileImageSource"/>) or a bitmap you already hold
/// (<see cref="BitmapImageSource"/>). <see cref="Primitives.ArcImage"/> loads it; in XAML a URL or an absolute file path
/// converts to a source, and in code <see cref="Parse"/> / <see cref="TryParse"/> do the same.
/// </summary>
/// <remarks>
/// Sources compare by reference, so assigning a new source for the same address asks for the picture again (a failed
/// address is retried); a picture that is already in <see cref="Primitives.ArcImageCache"/> still shows at once.
/// </remarks>
[TypeConverter(typeof(ImageSourceConverter))]
public abstract class ImageSource
{
    private protected ImageSource()
    {
    }

    /// <summary>
    /// The key under which <see cref="Primitives.ArcImageCache"/> keeps the decoded picture, or null when it is not cached
    /// (a bitmap source is already decoded).
    /// </summary>
    public abstract string? CacheKey { get; }

    /// <summary>Whether <paramref name="other"/> names the same picture (same address, same file or same bitmap).</summary>
    internal abstract bool IsSamePicture(ImageSource other);

    /// <summary>A source for a bitmap that is already decoded; nothing is loaded and nothing is cached.</summary>
    public static ImageSource FromBitmap(Bitmap bitmap) => new BitmapImageSource(bitmap);

    /// <summary>A source for a file the user picked (or dropped); it is read when it is shown.</summary>
    public static ImageSource FromStorageFile(IStorageFile file) => new StorageFileImageSource(file);

    /// <summary>A source for an absolute address: http(s), <c>avares://</c>, <c>file://</c> or <c>data:</c>.</summary>
    public static ImageSource FromUri(Uri uri) => new UriImageSource(uri);

    /// <summary>
    /// Reads an absolute URL (http, https, <c>avares</c>, <c>file</c>, <c>data</c>) or an absolute file path.
    /// </summary>
    /// <exception cref="FormatException">The text is not such an address.</exception>
    public static ImageSource Parse(string text)
    {
        if (TryParse(text, out var source))
        {
            return source;
        }
        throw new FormatException($"'{text}' is not an image address (an absolute http(s), avares, file or data URL, or an absolute file path).");
    }

    /// <summary>Like <see cref="Parse"/>, but returns false instead of throwing.</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out ImageSource? source)
    {
        source = null;
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && UriImageSource.IsSupported(uri))
        {
            // On Unix "/photos/a.png" parses as a file URI too; both end up as file:// here.
            source = new UriImageSource(uri);
            return true;
        }
        if (Path.IsPathRooted(value) && Uri.TryCreate(Path.GetFullPath(value), UriKind.Absolute, out var file) && file.IsFile)
        {
            source = new UriImageSource(file);
            return true;
        }
        return false;
    }

    /// <summary>The address as text (a URL, a file path or a short description).</summary>
    public abstract override string ToString();
}

/// <summary>A picture at an absolute address: http(s), <c>avares://</c>, <c>file://</c> or <c>data:</c>.</summary>
public sealed class UriImageSource : ImageSource
{
    /// <summary>Creates a source for <paramref name="uri"/>.</summary>
    /// <exception cref="ArgumentException">The address is relative or its scheme is not supported.</exception>
    public UriImageSource(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!IsSupported(uri))
        {
            throw new ArgumentException($"'{uri}' is not an absolute http(s), avares, file or data address.", nameof(uri));
        }
        Uri = uri;
    }

    /// <summary>The address of the picture.</summary>
    public Uri Uri { get; }

    /// <inheritdoc />
    public override string CacheKey => Uri.OriginalString;

    internal static bool IsSupported(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile ||
                              uri.Scheme == "avares" || uri.Scheme == "data");

    internal override bool IsSamePicture(ImageSource other) =>
        other is UriImageSource uri && string.Equals(uri.Uri.OriginalString, Uri.OriginalString, StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => Uri.OriginalString;
}

/// <summary>A picture in a file from the storage provider (a picked, dropped or pasted file); read when it is shown.</summary>
public sealed class StorageFileImageSource : ImageSource
{
    /// <summary>Creates a source for <paramref name="file"/>.</summary>
    public StorageFileImageSource(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        File = file;
    }

    /// <summary>The file holding the picture.</summary>
    public IStorageFile File { get; }

    /// <inheritdoc />
    /// <remarks>Files with an absolute path are cached by it; others (some browser files) are not cached.</remarks>
    public override string? CacheKey => File.Path is { IsAbsoluteUri: true } path ? "storage:" + path.AbsoluteUri : null;

    internal override bool IsSamePicture(ImageSource other) => other is StorageFileImageSource file && ReferenceEquals(file.File, File);

    /// <inheritdoc />
    public override string ToString() => File.Name;
}

/// <summary>A bitmap that is already decoded; it shows at once and is never cached or disposed by Arc.</summary>
public sealed class BitmapImageSource : ImageSource
{
    /// <summary>Creates a source for <paramref name="bitmap"/>.</summary>
    public BitmapImageSource(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Bitmap = bitmap;
    }

    /// <summary>The picture.</summary>
    public Bitmap Bitmap { get; }

    /// <inheritdoc />
    public override string? CacheKey => null;

    internal override bool IsSamePicture(ImageSource other) => other is BitmapImageSource bitmap && ReferenceEquals(bitmap.Bitmap, Bitmap);

    /// <inheritdoc />
    public override string ToString() => $"Bitmap {Bitmap.PixelSize.Width}×{Bitmap.PixelSize.Height}";
}

/// <summary>Converts XAML text (a URL or an absolute file path) to an <see cref="ImageSource"/>.</summary>
public sealed class ImageSourceConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || sourceType == typeof(Uri) || typeof(Bitmap).IsAssignableFrom(sourceType) ||
        base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value switch
        {
            string text => ImageSource.Parse(text),
            Uri uri => new UriImageSource(uri),
            Bitmap bitmap => new BitmapImageSource(bitmap),
            _ => base.ConvertFrom(context, culture, value)
        };
}
