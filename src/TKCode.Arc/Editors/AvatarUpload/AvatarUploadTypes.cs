using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace TKCode.Arc;

/// <summary>What an <see cref="AvatarUpload"/> is doing.</summary>
public enum AvatarUploadState
{
    /// <summary>No picture: the field invites a drop or a pick.</summary>
    Empty,

    /// <summary>A picture is being framed in the circle.</summary>
    Editing,

    /// <summary>The framed picture is being uploaded.</summary>
    Uploading,

    /// <summary>The field shows its picture.</summary>
    Done,

    /// <summary>The upload failed; it can be retried or cancelled.</summary>
    Failed
}

/// <summary>Why an <see cref="AvatarUpload"/> refused a file.</summary>
public enum AvatarUploadRejection
{
    /// <summary>The file is not a PNG, JPEG, WebP, GIF or BMP image.</summary>
    Type,

    /// <summary>The file is larger than <see cref="AvatarUpload.MaxFileSize"/>.</summary>
    Size,

    /// <summary>The file could not be read as an image.</summary>
    Unreadable
}

/// <summary>The image format an <see cref="AvatarUpload"/> saves its picture in (<see cref="AvatarUpload.OutputFormat"/>).</summary>
public enum AvatarImageFormat
{
    /// <summary>A PNG, and a picture whose type is unknown, are saved as PNG; a picture of any other type is saved as JPEG.</summary>
    Auto,

    /// <summary>Always PNG, which keeps transparency.</summary>
    Png,

    /// <summary>Always JPEG at <see cref="AvatarUpload.JpegQuality"/>; transparent areas become white.</summary>
    Jpeg
}

/// <summary>A framed picture: the square image to upload and the part of the original it was cut from.</summary>
public sealed class AvatarUploadRequest
{
    private byte[]? _bytes;
    private readonly string? _contentType;

    public AvatarUploadRequest(Bitmap image, PixelRect sourceRect, string? fileName)
    {
        Image = image;
        SourceRect = sourceRect;
        FileName = fileName;
    }

    internal AvatarUploadRequest(Bitmap image, PixelRect sourceRect, string? fileName, byte[] bytes, string contentType)
        : this(image, sourceRect, fileName)
    {
        _bytes = bytes;
        _contentType = contentType;
    }

    /// <summary>
    /// The picture encoded as <see cref="ContentType"/>, ready to send. A picture saved by the field is encoded in the
    /// <see cref="AvatarUpload.OutputFormat"/>; a request built by hand is encoded as PNG the first time this is read.
    /// </summary>
    public byte[] Bytes => _bytes ??= EncodePng();

    /// <summary><c>image/png</c> or <c>image/jpeg</c>: the format of <see cref="Bytes"/>.</summary>
    public string ContentType => _contentType ?? "image/png";

    /// <summary>A name for the stored picture: the original file's name with the extension of <see cref="ContentType"/> (<c>avatar</c> when there was none).</summary>
    public string SuggestedFileName => AvatarImageTypes.SavedName(FileName, ContentType);

    /// <summary>A read-only stream over <see cref="Bytes"/>, for an upload that takes a stream.</summary>
    public Stream OpenStream() => new MemoryStream(Bytes, writable: false);

    private byte[] EncodePng()
    {
        using var stream = new MemoryStream();
        Image.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    /// <summary>The square picture, <see cref="AvatarUpload.OutputSize"/> pixels on each side. Show it in a circle.</summary>
    public Bitmap Image { get; }

    /// <summary>The square of the original picture, in its pixels, that the image was cut from.</summary>
    public PixelRect SourceRect { get; }

    /// <summary>The name of the file the picture came from, when it came from a file.</summary>
    public string? FileName { get; }
}

/// <summary>Arguments of <see cref="AvatarUpload.Cropped"/>.</summary>
public class AvatarUploadCroppedEventArgs : RoutedEventArgs
{
    public AvatarUploadCroppedEventArgs(RoutedEvent routedEvent, AvatarUploadRequest request)
        : base(routedEvent)
    {
        Request = request;
    }

    /// <summary>The framed picture.</summary>
    public AvatarUploadRequest Request { get; }
}

/// <summary>Arguments of <see cref="AvatarUpload.FileRejected"/>.</summary>
public class AvatarUploadRejectedEventArgs : RoutedEventArgs
{
    public AvatarUploadRejectedEventArgs(RoutedEvent routedEvent, string fileName, AvatarUploadRejection reason)
        : base(routedEvent)
    {
        FileName = fileName;
        Reason = reason;
    }

    /// <summary>The refused file's name.</summary>
    public string FileName { get; }

    /// <summary>Why it was refused.</summary>
    public AvatarUploadRejection Reason { get; }
}
